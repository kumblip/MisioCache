using System;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MisioCache.Controllers;

/// <summary>Serves static, non-sensitive JavaScript embedded in the plugin DLL.</summary>
[ApiController]
[Route("MisioCache/assets")]
public sealed class MisioCacheClientAssetsController : ControllerBase
{
    [HttpGet("smart-downloads.js")]
    [AllowAnonymous]
    public IActionResult SmartDownloads()
    {
        const string resource = "Jellyfin.Plugin.MisioCache.Web.smart-downloads.js";
        var stream = typeof(Plugin).Assembly.GetManifestResourceStream(resource);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "text/javascript; charset=utf-8");
    }
}
