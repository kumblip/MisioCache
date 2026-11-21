using Jellyfin.Plugin.MisioCache.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MisioCache.Controllers;

/// <summary>Administrator-only aggregated diagnostics, never raw media traffic.</summary>
[ApiController]
[Route("MisioCache/api/diagnostics")]
[Authorize(Policy = Policies.RequiresElevation)]
public sealed class MisioCacheDiagnosticsController : ControllerBase
{
    private readonly MisioCacheHttpObservationStore _store;
    public MisioCacheDiagnosticsController(MisioCacheHttpObservationStore store) => _store = store;

    [HttpGet("http")]
    public IActionResult GetTraffic()
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(_store.Snapshot(Plugin.Instance?.Configuration.EnablePassiveHttpDiagnostics == true,
            Plugin.Instance?.Configuration.EnableResponseBodyByteMeter == true));
    }

    [HttpPost("http/reset")]
    public IActionResult ClearTraffic()
    {
        _store.Clear();
        Response.Headers.CacheControl = "private, no-store";
        return Ok(_store.Snapshot(Plugin.Instance?.Configuration.EnablePassiveHttpDiagnostics == true,
            Plugin.Instance?.Configuration.EnableResponseBodyByteMeter == true));
    }
}
