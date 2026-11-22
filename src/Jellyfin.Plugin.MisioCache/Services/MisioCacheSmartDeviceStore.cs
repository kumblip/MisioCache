using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>
/// A small persistent, per-account/per-Jellyfin-device opt-in registry. It does
/// NOT track video traffic, downloaded media or device file inventory.
/// All callers are authorised by MisioCacheSmartDevicesController.
/// </summary>
public sealed class MisioCacheSmartDeviceStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly List<SmartDeviceRecord> _devices;

    public MisioCacheSmartDeviceStore(IApplicationPaths paths)
    {
        // Stable Jellyfin data path; independent of plugin construction order.
        // Persisted on the NAS, not in the Android WebView.
        var dataPath = Path.Combine(paths.DataPath, "MisioCache");
        Directory.CreateDirectory(dataPath);
        _path = Path.Combine(dataPath, "smart-downloads-devices-v1.json");
        try
        {
            var saved = File.Exists(_path) ? JsonSerializer.Deserialize<List<SmartDeviceRecord>>(File.ReadAllText(_path), _json) : null;
            _devices = saved?.Where(x => x.UserId != Guid.Empty && !string.IsNullOrWhiteSpace(x.DeviceId)).ToList() ?? new();
        }
        catch (JsonException) { _devices = new(); }
    }

    public sealed class SmartDeviceRecord
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Client { get; set; } = "";
        public bool NativeSupported { get; set; }
        public bool Enabled { get; set; }
        public long Count { get; set; } = 2;
        public double BudgetGB { get; set; } = 5;
        public string LastAction { get; set; } = "Nie odnotowano pobierania.";
        public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastActionAt { get; set; }
        public DateTimeOffset? LastHookSeen { get; set; }
    }

    public sealed record SettingsRequest(bool Enabled, long Count, double BudgetGB, bool NativeSupported = false);
    public sealed record StatusRequest(string Message);
    public sealed record AdminSettingsRequest(Guid UserId, string DeviceId, bool Enabled, long Count, double BudgetGB);

    private static SmartDeviceRecord Clone(SmartDeviceRecord source) => new()
    {
        UserId = source.UserId, UserName = source.UserName, DeviceId = source.DeviceId,
        DeviceName = source.DeviceName, Client = source.Client,
        NativeSupported = source.NativeSupported, Enabled = source.Enabled,
        Count = source.Count, BudgetGB = source.BudgetGB, LastAction = source.LastAction,
        LastSeen = source.LastSeen, LastActionAt = source.LastActionAt, LastHookSeen = source.LastHookSeen
    };

    public static bool Valid(long count, double budgetGB) => count > 0 &&
        double.IsFinite(budgetGB) && budgetGB > 0 &&
        Math.Ceiling(budgetGB * 1_000_000_000d) <= 9_007_199_254_740_991d;

    private SmartDeviceRecord FindOrCreate(Guid user, string device, string userName, string deviceName, string client, bool native)
    {
        var found = _devices.FirstOrDefault(x => x.UserId == user && string.Equals(x.DeviceId, device, StringComparison.Ordinal));
        if (found is null)
        {
            found = new SmartDeviceRecord { UserId = user, DeviceId = device };
            _devices.Add(found);
        }
        found.UserName = Limit(userName, 90);
        found.DeviceName = Limit(deviceName, 90);
        found.Client = Limit(client, 90);
        found.NativeSupported = native;
        found.LastSeen = DateTimeOffset.UtcNow;
        return found;
    }

    private static string Limit(string? value, int max) => (value ?? string.Empty).Trim()[..Math.Min(max, (value ?? string.Empty).Trim().Length)];

    public (SmartDeviceRecord Record, bool IsNew) Current(Guid user, string device, string userName, string deviceName, string client, bool native)
    {
        lock (_gate)
        {
            var previous = _devices.FirstOrDefault(x => x.UserId == user && x.DeviceId == device);
            var isNew = previous is null;
            var changed = isNew || DateTimeOffset.UtcNow - previous!.LastSeen > TimeSpan.FromMinutes(1) ||
                previous.DeviceName != Limit(deviceName, 90) || previous.Client != Limit(client, 90) ||
                previous.NativeSupported != native;
            var record = FindOrCreate(user, device, userName, deviceName, client, native);
            // Record registration immediately; avoid writing to the NAS on
            // every repeated settings GET during a single download action.
            if (changed) Save();
            return (Clone(record), isNew);
        }
    }

    public SmartDeviceRecord UpdateCurrent(Guid user, string device, string userName, string deviceName, string client, SettingsRequest input)
    {
        if (!Valid(input.Count, input.BudgetGB)) throw new ArgumentException("Liczba odcinków i limit GB muszą być dodatnie.");
        lock (_gate)
        {
            var record = FindOrCreate(user, device, userName, deviceName, client, input.NativeSupported);
            record.Enabled = input.Enabled;
            record.Count = input.Count;
            record.BudgetGB = input.BudgetGB;
            Save();
            return Clone(record);
        }
    }

    public SmartDeviceRecord? UpdateAdmin(AdminSettingsRequest input)
    {
        if (!Valid(input.Count, input.BudgetGB)) throw new ArgumentException("Liczba odcinków i limit GB muszą być dodatnie.");
        lock (_gate)
        {
            // Admin can edit only devices already registered by their owners.
            var record = _devices.FirstOrDefault(x => x.UserId == input.UserId && x.DeviceId == input.DeviceId);
            if (record is null) return null;
            record.Enabled = input.Enabled;
            record.Count = input.Count;
            record.BudgetGB = input.BudgetGB;
            Save();
            return Clone(record);
        }
    }

    public SmartDeviceRecord? RecordStatus(Guid user, string device, string message)
    {
        lock (_gate)
        {
            var record = _devices.FirstOrDefault(x => x.UserId == user && x.DeviceId == device);
            if (record is null) return null;
            record.LastAction = Limit(message, 400);
            record.LastActionAt = DateTimeOffset.UtcNow;
            record.LastSeen = DateTimeOffset.UtcNow;
            Save();
            return Clone(record);
        }
    }

    public void Heartbeat(Guid user, string device)
    {
        lock (_gate)
        {
            var item = _devices.FirstOrDefault(x => x.UserId == user && x.DeviceId == device);
            if (item is null) return;
            item.LastHookSeen = DateTimeOffset.UtcNow;
            item.LastSeen = DateTimeOffset.UtcNow;
            Save();
        }
    }

    public IReadOnlyList<SmartDeviceRecord> List() { lock (_gate) return _devices.OrderByDescending(x => x.LastSeen).Select(Clone).ToArray(); }

    private void Save()
    {
        // Same-volume rename: a crash must not leave a partially written JSON file.
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_devices, _json));
            File.Move(temp, _path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
