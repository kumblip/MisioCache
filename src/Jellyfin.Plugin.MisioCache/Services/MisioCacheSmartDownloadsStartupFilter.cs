using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>
/// Adds only MisioCache's optional client-side Smart Downloads listener to the
/// Jellyfin web shell, without modifying Jellyfin's installed web files.
/// The hook never touches HTTP requests for media or Jellyfin player APIs.
/// Compatible with the independently installed MisioFlix HTML injection: each
/// filter buffers and forwards the entire index, retaining other plugins' tags.
/// </summary>
public sealed class MisioCacheSmartDownloadsStartupFilter : IStartupFilter
{
    private const string Marker = "data-misiocache=\"smart-downloads\"";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, downstream) =>
            {
                if (!HttpMethods.IsGet(context.Request.Method) || !IsIndex(context.Request.Path.Value))
                {
                    await downstream().ConfigureAwait(false);
                    return;
                }

                // Only the small web entrypoint is buffered; video streaming is
                // NEVER intercepted. Preserve other plugins' generated HTML.
                context.Request.Headers.Remove("Accept-Encoding");
                var originalBody = context.Response.Body;
                await using var temporaryBody = new MemoryStream();
                context.Response.Body = temporaryBody;
                try
                {
                    await downstream().ConfigureAwait(false);
                }
                catch
                {
                    context.Response.Body = originalBody;
                    throw;
                }
                context.Response.Body = originalBody;
                temporaryBody.Position = 0;

                if (context.Response.StatusCode != StatusCodes.Status200OK ||
                    !(context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    context.Response.Headers.ContainsKey("Content-Encoding"))
                {
                    await temporaryBody.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
                    return;
                }

                var html = Encoding.UTF8.GetString(temporaryBody.ToArray());
                var position = html.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (position < 0 || html.Contains(Marker, StringComparison.Ordinal))
                {
                    await temporaryBody.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
                    return;
                }

                var version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
                var script = "<script data-misiocache=\"smart-downloads\" defer " +
                    "src=\"../MisioCache/assets/smart-downloads.js?v=" + version + "\"></script>";
                html = html.Insert(position, script);

                var output = Encoding.UTF8.GetBytes(html);
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.Remove("Last-Modified");
                context.Response.Headers.Remove("Content-MD5");
                context.Response.Headers.CacheControl = "no-store";
                context.Response.ContentLength = output.Length;
                await originalBody.WriteAsync(output, context.RequestAborted).ConfigureAwait(false);
            });
            next(app);
        };
    }

    private static bool IsIndex(string? path)
    {
        return path is not null &&
            (path.Equals("/web", StringComparison.OrdinalIgnoreCase) ||
             path.Equals("/web/", StringComparison.OrdinalIgnoreCase) ||
             path.Equals("/web/index.html", StringComparison.OrdinalIgnoreCase));
    }
}
