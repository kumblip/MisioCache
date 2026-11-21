using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MisioCache.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MisioCache.Controllers;

// Read-only discovery. Package installation and scheduled task start use
// Jellyfin's native ADMIN-PROTECTED /Packages and /ScheduledTasks API.
[ApiController]
[Route("MisioCache/api/updates")]
[Authorize]
public sealed class MisioCacheUpdateController : ControllerBase
{
    private readonly MisioCacheUpdateService _updates;
    private readonly MisioCacheNativeCatalogRefreshService _nativeCatalog;
    public MisioCacheUpdateController(MisioCacheUpdateService updates,
        MisioCacheNativeCatalogRefreshService nativeCatalog)
    {
        _updates = updates;
        _nativeCatalog = nativeCatalog;
    }

    // Explicit admin request: update Jellyfin's own repository configuration
    // to a verified, immutable GitHub revision. Never install plugins here.
    [HttpPost("native-catalog/refresh")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<IActionResult> RefreshNativeCatalog(CancellationToken cancel)
    {
        if (Plugin.Instance is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        try { return Ok(await _nativeCatalog.RefreshAsync(cancel).ConfigureAwait(false)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    [HttpGet("status")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<IActionResult> Status([FromQuery] bool force=false, CancellationToken cancel=default)
    {
        if (Plugin.Instance is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        try { return Ok(await _updates.StatusAsync(force, cancel).ConfigureAwait(false)); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }
}
