/* MisioCache Smart Downloads — optional per-account/per-device native download queue.
 * This script is served by MisioCache. It does not modify Jellyfin's APK,
 * MisioFlix code, media HTTP responses, or the player.
 * The native download API does not report progress/completion/local inventory.
 */
(function () {
    'use strict';
    if (window.MisioCacheSmartDownloads?.__clientVersion === 2) return;

    const GUID = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
    const GB = 1000000000;
    const PROCESSING = new Set();
    const QUEUED = new Set(); // only within the current WebView session
    let suppressHook = 0;
    let hookAnnounced = false;

    function idOf(value) {
        const valueString = String(value ?? '');
        return GUID.test(valueString) ? valueString.replace(/-/g, '').toLowerCase() : '';
    }
    function nativeSupported() {
        try {
            const shell = window.NativeShell;
            return !!(shell && shell.AppHost &&
                typeof shell.AppHost.supports === 'function' && shell.AppHost.supports('filedownload') &&
                typeof shell.downloadFiles === 'function' && typeof shell.openDownloadManager === 'function');
        } catch (_) { return false; }
    }
    function currentDeviceKey() {
        try {
            const host = window.NativeShell?.AppHost;
            return host && typeof host.deviceId === 'function' ? String(host.deviceId()) : 'android-device';
        } catch (_) { return 'android-device'; }
    }
    function countOf(raw) {
        const value = String(raw ?? '').trim();
        if (!/^[1-9]\d*$/.test(value)) return null;
        const number = Number(value);
        return Number.isSafeInteger(number) ? number : null;
    }
    function budgetOf(raw) {
        const value = String(raw ?? '').trim().replace(',', '.');
        if (!/^(?:\d+(?:\.\d+)?|\.\d+)$/.test(value)) return null;
        const number = Number(value);
        return Number.isFinite(number) && number > 0 && Number.isSafeInteger(Math.ceil(number * GB))
            ? number : null;
    }
    // Backward-compatible, device-local settings are migrated once to the
    // server registry. After that, MisioCache's authenticated API is the
    // source of truth for BOTH its panel and MisioFlix's presentation popup.
    function legacyPreferences(key) {
        try {
            const saved = JSON.parse(window.localStorage.getItem(key) || 'null');
            if (!saved || typeof saved !== 'object') return null;
            const legacy = saved.budgetGB === undefined && saved.budgetGiB !== undefined
                ? Number(saved.budgetGiB) * 1073741824 / GB : saved.budgetGB;
            const count = countOf(saved.count);
            const budgetGB = budgetOf(legacy);
            return count !== null && budgetGB !== null
                ? { enabled: saved.enabled === true, count, budgetGB } : null;
        } catch (_) { return null; }
    }
    function api() { return window.ApiClient || window.ConnectionManager?.getApiClient?.() || null; }
    async function request(path, method = 'GET', body) {
        const client = api();
        if (!client || typeof client.getUrl !== 'function') throw Error('Brak aktywnego API Jellyfin.');
        const token = typeof client.accessToken === 'function' ? client.accessToken() : client.accessToken;
        if (!token) throw Error('Brak tokenu Jellyfin — ustawienia nie zostaną zapisane.');
        const headers = { Authorization: 'MediaBrowser Token="' + String(token).replace(/["\r\n]/g, '') + '"' };
        if (body !== undefined) headers['Content-Type'] = 'application/json';
        const result = await (window.fetch || fetch)(client.getUrl('MisioCache/api/smart-downloads/' + path), {
            method, headers, cache: 'no-store', ...(body !== undefined ? { body: JSON.stringify(body) } : {})
        });
        if (!result.ok) throw Error('MisioCache API HTTP ' + result.status);
        return result.status === 204 ? null : result.json();
    }
    async function getSettings() {
        const client = api();
        if (!client || typeof client.getCurrentUser !== 'function') throw Error('Brak użytkownika Jellyfin.');
        const user = await client.getCurrentUser();
        if (!idOf(user?.Id)) throw Error('Brak konta Jellyfin.');
        const key = accountKey(client, String(user.Id));
        const response = await request('current?nativeSupported=' + (nativeSupported() ? 'true' : 'false'));
        let record = response?.record || response?.Record;
        if (!record) throw Error('Nieprawidłowa odpowiedź MisioCache.');
        // Never overwrite an existing server record from an old stale device cache.
        if ((response.isNew || response.IsNew) && nativeSupported()) {
            const legacy = legacyPreferences(key);
            if (legacy) {
                const migrated = await request('current', 'POST', { ...legacy, nativeSupported: true });
                record = migrated.record || migrated.Record;
            }
        }
        // Jellyfin may serialize controller records as PascalCase or camelCase.
        const field = name => record?.[name] ?? record?.[name[0].toUpperCase() + name.slice(1)];
        const resolved = {
            enabled: field('enabled') === true,
            count: Number(field('count')),
            budgetGB: Number(field('budgetGB')),
            userName: String(field('userName') || ''),
            deviceName: String(field('deviceName') || ''),
            lastAction: String(field('lastAction') || ''),
            lastHookSeen: field('lastHookSeen') || null,
            preferenceKey: key, nativeSupported: nativeSupported()
        };
        try {
            window.localStorage.setItem(key, JSON.stringify({
                enabled: resolved.enabled, count: resolved.count, budgetGB: resolved.budgetGB
            }));
        } catch (_) { /* server registry remains authoritative */ }
        return resolved;
    }
    async function saveSettings(values) {
        const count = countOf(values?.count);
        const budgetGB = budgetOf(values?.budgetGB);
        if (count === null || budgetGB === null) throw Error('Niepoprawna liczba odcinków lub limit GB.');
        if (values?.enabled === true && !nativeSupported()) throw Error('Brak menedżera pobierania Androida.');
        const updated = await request('current', 'POST', {
            enabled: values?.enabled === true, count, budgetGB, nativeSupported: nativeSupported()
        });
        const result = await getSettings();
        window.dispatchEvent(new CustomEvent('misiocache-smart-downloads-changed', { detail: { serverSaved: true } }));
        return result;
    }
    async function listDevices() { const data = await request('devices'); return data?.devices || data?.Devices || []; }
    async function manageDevice(payload) { return await request('devices/manage', 'POST', payload); }
    async function reportStatus(message) {
        try { await request('current/status', 'POST', { message: String(message).slice(0, 400) }); }
        catch (_) { /* never interrupt native download */ }
    }
    function accountKey(api, userId) {
        const server = typeof api.serverId === 'function' ? String(api.serverId()) : window.location.origin;
        return 'misio.cache.smart.v1:' + server + ':' + userId + ':' + currentDeviceKey();
    }
    function status(key, message) {
        if (!key) return;
        try {
            window.localStorage.setItem('misio.cache.smart.last.v1:' + key, JSON.stringify({
                time: Date.now(), message
            }));
        } catch (_) { /* do not interfere with official downloads */ }
        void reportStatus(message);
        try {
            window.dispatchEvent(new CustomEvent('misiocache-smart-downloads-status', {
                detail: { key, message }
            }));
        } catch (_) { /* status event is optional */ }
    }
    function extractItemId(download) {
        if (typeof download === 'string') return idOf(download);
        if (!download || typeof download !== 'object') return '';
        for (const item of [download.itemId, download.ItemId, download.id, download.Id, download.item?.Id, download.Item?.Id]) {
            const id = idOf(item);
            if (id) return id;
        }
        const uri = String(download.url || download.Url || '');
        const match = uri.match(/(?:^|\/)Items\/([0-9a-f-]{32,36})\/Download(?:[?#]|$)/i);
        return match ? idOf(match[1]) : '';
    }
    function episodeSize(item) {
        const media = Array.isArray(item.MediaSources) ? item.MediaSources : [];
        const sizes = media.map(source => Number(source.Size))
            .filter(value => Number.isSafeInteger(value) && value > 0);
        const bytes = sizes.length ? Math.max(...sizes) : Number(item.Size);
        return Number.isSafeInteger(bytes) && bytes > 0 ? bytes : null;
    }
    function sortedEpisodes(items) {
        const ordinal = (item, name) => item[name] !== undefined && item[name] !== null &&
            Number.isInteger(Number(item[name])) ? Number(item[name]) : 999999;
        return [...items].filter(item => item?.Type === 'Episode' && idOf(item.Id) &&
            item.LocationType !== 'Virtual' && !item.IsMissing)
            .sort((a, b) => ordinal(a, 'ParentIndexNumber') - ordinal(b, 'ParentIndexNumber') ||
                ordinal(a, 'IndexNumber') - ordinal(b, 'IndexNumber') ||
                String(a.SortName || a.Name || '').localeCompare(String(b.SortName || b.Name || ''), 'pl'));
    }
    async function queryItems(api, userId, query) {
        if (typeof api.getItems === 'function') return await api.getItems(userId, query);
        if (typeof api.getJSON === 'function' && typeof api.getUrl === 'function')
            return await api.getJSON(api.getUrl('Users/' + encodeURIComponent(userId) + '/Items', query));
        throw Error('Brak zgodnego API Jellyfin.');
    }
    function nativeOriginal(name) {
        const method = window.NativeShell?.[name];
        return typeof method === 'function' ? (method.__misioCacheOriginal || method) : null;
    }
    function downloadManual(entries) {
        // Called by MisioCache's own "Pobierz wybrany + kolejne" panel.
        // Never treat an explicitly constructed batch as a new auto-queue trigger.
        if (!nativeSupported() || !Array.isArray(entries) || !entries.length) return false;
        const method = nativeOriginal('downloadFiles');
        if (!method) return false;
        suppressHook++;
        try { return method.call(window.NativeShell, entries); }
        finally { suppressHook--; }
    }
    async function enqueueNext(clickedId) {
        const currentId = idOf(clickedId);
        if (!currentId || !nativeSupported() || PROCESSING.has(currentId)) return;
        PROCESSING.add(currentId);
        let key = null;
        try {
            const api = window.ApiClient || window.ConnectionManager?.getApiClient?.();
            if (!api || typeof api.getCurrentUser !== 'function') return;
            const account = await api.getCurrentUser();
            const userId = String(account?.Id || '');
            if (!idOf(userId)) return;
            key = accountKey(api, userId);
            const config = await getSettings();
            if (config.preferenceKey !== key) return;
            const opts = config.enabled === true ? {
                count: countOf(config.count), budgetGB: budgetOf(config.budgetGB)
            } : null;
            if (!opts || opts.count === null || opts.budgetGB === null) {
                status(key, 'Standardowe Pobierz: Smart Downloads jest wyłączone lub ma nieprawidłowe ustawienia.');
                return;
            }

            const currentResponse = await queryItems(api, userId, {
                Ids: currentId, Fields: 'MediaSources', Limit: 1
            });
            const current = (currentResponse?.Items || []).find(item => idOf(item.Id) === currentId);
            if (current?.Type !== 'Episode' || !idOf(current.SeriesId)) {
                status(key, 'Nie znaleziono odcinka i serialu. Pobrano tylko wybrany odcinek.');
                return;
            }
            const response = await queryItems(api, userId, {
                ParentId: String(current.SeriesId), Recursive: true,
                IncludeItemTypes: 'Episode', Fields: 'MediaSources', Limit: 10000
            });
            const episodes = sortedEpisodes(response?.Items || []);
            const index = episodes.findIndex(item => idOf(item.Id) === currentId);
            if (index < 0) {
                status(key, 'Nie znaleziono odcinka w kolejności serialu.');
                return;
            }
            const next = episodes.slice(index + 1, index + 1 + opts.count);
            if (!next.length) {
                status(key, 'To ostatni dostępny odcinek, nie dodano kolejnych.');
                return;
            }
            // Budget means the full newly requested batch: original + extras.
            // The original Jellyfin action has already been sent; this check
            // can only veto the additional episodes, never cancel the original.
            let total = episodeSize(current);
            if (total === null) {
                status(key, 'Rozmiar wybranego odcinka jest nieznany. Nie dodano kolejnych dla ochrony miejsca.');
                return;
            }
            for (const episode of next) {
                const size = episodeSize(episode);
                if (size === null || !Number.isSafeInteger(total + size)) {
                    status(key, 'Brak wiarygodnych rozmiarów plików. Nie dodano kolejnych.');
                    return;
                }
                total += size;
                if (total > opts.budgetGB * GB) {
                    status(key, 'Łączna paczka przekracza limit ' + opts.budgetGB + ' GB. Pobrano tylko wybrany.');
                    return;
                }
            }
            // Revalidate account, server, phone and preferences after network calls.
            const verified = await api.getCurrentUser();
            if (idOf(verified?.Id) !== idOf(userId) || accountKey(api, userId) !== key || !nativeSupported()) return;
            const latest = await getSettings();
            if (!latest.enabled || latest.count !== opts.count || latest.budgetGB !== opts.budgetGB || latest.preferenceKey !== key) return;

            const additional = next.filter(item => !QUEUED.has(key + ':' + idOf(item.Id)));
            if (!additional.length) {
                status(key, 'Te odcinki zostały już zlecone w tej sesji.');
                return;
            }
            const entries = additional.map(item => ({ itemId: String(item.Id) }));
            const result = downloadManual(entries);
            if (result === false) throw Error('Android odmówił przyjęcia kolejki.');
            for (const item of additional) QUEUED.add(key + ':' + idOf(item.Id));
            status(key, 'Zlecono ' + entries.length + ' kolejnych odcinków (' +
                (total / GB).toFixed(2) + ' GB wraz z wybranym). Sprawdź „Pobrane”.');
            try {
                window.dispatchEvent(new CustomEvent('misiocache-smart-downloads-queued', {
                    detail: { count: entries.length, bytes: total }
                }));
            } catch (_) { /* UI hook optional */ }
        } catch (error) {
            if (key) status(key, 'Nie udało się zlecić następnych: ' + String(error?.message || error));
            // Standard Android download is intentionally never rolled back.
        } finally {
            PROCESSING.delete(currentId);
        }
    }
    function install() {
        const shell = window.NativeShell;
        if (!nativeSupported()) return false;
        for (const name of ['downloadFile', 'downloadFiles']) {
            const existing = shell[name];
            if (typeof existing !== 'function') continue;
            if (existing.__misioCacheVersion === 2) continue;
            const original = existing.__misioCacheOriginal || existing;
            function wrapped(arg) {
                // Preserve native return value, exceptions and argument shape.
                const result = original.apply(this, arguments);
                if (suppressHook) return result;
                const entries = name === 'downloadFiles' ? arg : [arg];
                if (Array.isArray(entries) && entries.length === 1) {
                    const itemId = extractItemId(entries[0]);
                    if (itemId) void enqueueNext(itemId);
                    else void reportStatus('Wykryto „Pobierz”, ale nie udało się ustalić ID odcinka.');
                }
                return result;
            }
            wrapped.__misioCacheOriginal = original;
            wrapped.__misioCacheVersion = 2;
            shell[name] = wrapped;
        }
        if (!hookAnnounced) {
            hookAnnounced = true;
            void getSettings().then(() => request('current/hook', 'POST'))
                .catch(() => { hookAnnounced = false; });
        }
        return true;
    }

    // Public API for the MisioCache download page. It calls the official native
    // method but suppresses auto-expansion of explicitly selected batches.
    window.MisioCacheSmartDownloads = Object.freeze({
        __clientVersion: 2, downloadManual, refreshHook: install,
        getSettings, saveSettings, listDevices, manageDevice, reportStatus
    });

    // The Android bridge may be installed after the Jellyfin web entrypoint.
    // Poll only during initialisation; also re-attach on route changes.
    let attempts = 0;
    const retry = window.setInterval(() => {
        if (install() || ++attempts >= 120) window.clearInterval(retry);
    }, 500);
    install();
    window.addEventListener('hashchange', install);
    window.addEventListener('pageshow', install);
    window.addEventListener('focus', install);
    // Older MisioFlix popup versions may still write localStorage directly.
    // Bridge their explicit save event to the server without embedding
    // downloading logic in MisioFlix. The new popup uses saveSettings().
    window.addEventListener('misiocache-smart-downloads-changed', event => {
        if (event?.detail?.serverSaved) return;
        void (async () => {
            try {
                const client = api();
                const user = await client?.getCurrentUser?.();
                if (!idOf(user?.Id)) return;
                const saved = legacyPreferences(accountKey(client, String(user.Id)));
                if (saved) await saveSettings(saved);
            } catch (_) { /* cannot accept unverified opt-in */ }
        })();
    });
})();
