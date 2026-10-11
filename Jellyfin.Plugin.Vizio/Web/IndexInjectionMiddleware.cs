using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Vizio.Web;

/// <summary>
/// Injects vizio.js into jellyfin-web's index.html, only for SmartCast browsers.
/// Works on the response in memory, so nothing in the web directory is modified.
/// </summary>
public partial class IndexInjectionMiddleware
{
    private static readonly Lazy<string> Script = new(LoadScript);

    private readonly RequestDelegate _next;

    public IndexInjectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldInject(context.Request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Need an uncompressed, full (non-304) response to rewrite
        context.Request.Headers.AcceptEncoding = default;
        context.Request.Headers.IfNoneMatch = default;
        context.Request.Headers.IfModifiedSince = default;

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var isHtml = context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ?? false;
        if (context.Response.StatusCode != StatusCodes.Status200OK || !isHtml)
        {
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            return;
        }

        var html = Encoding.UTF8.GetString(buffer.ToArray());
        html = HeadTag().Replace(html, m => m.Value + BuildScriptTag(), 1);

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.ETag = default;
        context.Response.Headers.LastModified = default;
        context.Response.Headers.CacheControl = "no-store";
        await originalBody.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static bool ShouldInject(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var userAgent = request.Headers.UserAgent.ToString();
        if (!userAgent.Contains("SmartCast", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Runs ahead of Jellyfin's BaseUrl mapping, so match the end of the path
        var path = request.Path.Value ?? string.Empty;
        return path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildScriptTag()
    {
        var debug = Plugin.Instance?.Configuration.DebugLogging == true ? "true" : "false";
        return "<script>window.VIZIO_DEBUG=" + debug + ";\n" + Script.Value + "</script>";
    }

    private static string LoadScript()
    {
        var assembly = typeof(IndexInjectionMiddleware).Assembly;
        using var stream = assembly.GetManifestResourceStream("Jellyfin.Plugin.Vizio.Web.vizio.js")
            ?? throw new InvalidOperationException("vizio.js resource missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex("<head[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadTag();
}
