using System;
using System.Linq;
using System.Security.Claims;
using Jellyfin.Plugin.MisioCache.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MisioCache.Controllers;

/// <summary>
/// Server-owned settings for the *authenticated* account and device. Request
/// bodies cannot choose a different account or device. Admin-only device list.
/// </summary>
[ApiController]
[Authorize]
[Route("MisioCache/api/smart-downloads")]
public sealed class MisioCacheSmartDevicesController : ControllerBase
{
    private readonly MisioCacheSmartDeviceStore _devices;
    public MisioCacheSmartDevicesController(MisioCacheSmartDeviceStore devices) => _devices = devices;

    private string Claim(string name) => User.Claims.FirstOrDefault(c => c.Type.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
    private bool Session(out Guid userId, out string deviceId)
    {
        // Jellyfin 12.4 CustomAuthenticationHandler issues these validated claims.
        userId = Guid.TryParse(Claim("Jellyfin-UserId"), out var id) ? id : Guid.Empty;
        deviceId = Claim("Jellyfin-DeviceId").Trim();
        return userId != Guid.Empty && deviceId.Length > 0 && deviceId.Length <= 256;
    }
    private object CurrentResult(bool native, bool isNew, Guid user, string device)
    {
        var item = _devices.Current(user, device, Claim(ClaimTypes.Name), Claim("Jellyfin-Device"), Claim("Jellyfin-Client"), native);
        return new { record = item.Record, isNew = isNew && item.IsNew };
    }

    [HttpGet("current")]
    public IActionResult Current([FromQuery] bool nativeSupported = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!Session(out var user, out var device)) return Unauthorized();
        return Ok(CurrentResult(nativeSupported, true, user, device));
    }

    [HttpPost("current")]
    public IActionResult Update([FromBody] MisioCacheSmartDeviceStore.SettingsRequest body)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!Session(out var user, out var device)) return Unauthorized();
        if (body is null || !MisioCacheSmartDeviceStore.Valid(body.Count, body.BudgetGB)) return BadRequest(new { error = "Podaj dodatnią liczbę odcinków i limit GB." });
        return Ok(new { record = _devices.UpdateCurrent(user, device, Claim(ClaimTypes.Name), Claim("Jellyfin-Device"), Claim("Jellyfin-Client"), body), isNew = false });
    }

    [HttpPost("current/status")]
    public IActionResult Status([FromBody] MisioCacheSmartDeviceStore.StatusRequest body)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!Session(out var user, out var device)) return Unauthorized();
        if (body is null || string.IsNullOrWhiteSpace(body.Message) || body.Message.Length > 400) return BadRequest();
        var updated = _devices.RecordStatus(user, device, body.Message);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("current/hook")]
    public IActionResult Hook()
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!Session(out var user, out var device)) return Unauthorized();
        _devices.Heartbeat(user, device);
        return NoContent();
    }

    [HttpGet("devices")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public IActionResult Devices()
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new { devices = _devices.List() });
    }

    [HttpPost("devices/manage")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public IActionResult Manage([FromBody] MisioCacheSmartDeviceStore.AdminSettingsRequest body)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (body is null || body.UserId == Guid.Empty || string.IsNullOrWhiteSpace(body.DeviceId) ||
            !MisioCacheSmartDeviceStore.Valid(body.Count, body.BudgetGB)) return BadRequest(new { error = "Błędna konfiguracja urządzenia." });
        var updated = _devices.UpdateAdmin(body);
        return updated is null ? NotFound() : Ok(updated);
    }
}
