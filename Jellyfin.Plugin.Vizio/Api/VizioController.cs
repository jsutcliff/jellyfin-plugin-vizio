using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Vizio.Api;

[ApiController]
[Route("Vizio")]
public class VizioController : ControllerBase
{
    private const int MaxLogLength = 1000;

    private readonly VizioTvClient _tv;
    private readonly INetworkManager _network;
    private readonly ISessionManager _sessionManager;
    private readonly IUserManager _userManager;
    private readonly IServerApplicationHost _appHost;
    private readonly ILogger<VizioController> _logger;

    public VizioController(
        VizioTvClient tv,
        INetworkManager network,
        ISessionManager sessionManager,
        IUserManager userManager,
        IServerApplicationHost appHost,
        ILogger<VizioController> logger)
    {
        _tv = tv;
        _network = network;
        _sessionManager = sessionManager;
        _userManager = userManager;
        _appHost = appHost;
        _logger = logger;
    }

    /// <summary>
    /// Opens Jellyfin on the paired TV. Meant to be bookmarked on a phone, so it
    /// needs no login, but only answers requests from the local network. The login
    /// for the configured TV user goes to the TV, never back to the requester.
    /// </summary>
    [HttpGet("Launch")]
    [AllowAnonymous]
    [Produces("text/html")]
    public async Task<ActionResult> Launch(CancellationToken ct)
    {
        if (!IsLocalRequest())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var config = Plugin.Instance!.Configuration;
        if (string.IsNullOrEmpty(config.TvAddress) || string.IsNullOrEmpty(config.AuthToken))
        {
            return LaunchPage("No TV paired yet. Pair one under Dashboard → Plugins → Vizio SmartCast.");
        }

        var url = string.IsNullOrEmpty(config.LaunchUrl)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/web/"
            : config.LaunchUrl;

        try
        {
            url += await BuildLoginFragmentAsync(config.TvUserId, config.DeviceId).ConfigureAwait(false);
            await _tv.LaunchAsync(config.TvAddress, config.AuthToken, url, ct).ConfigureAwait(false);
            return LaunchPage("Launched on the TV.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Launching on Vizio TV {Address} failed", config.TvAddress);
            return LaunchPage("Couldn't reach the TV: " + ex.Message);
        }
    }

    [HttpPost("Pair/Start")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> StartPairing([FromBody, Required] StartPairingRequest body, CancellationToken ct)
    {
        var plugin = Plugin.Instance!;
        var config = plugin.Configuration;
        config.TvAddress = body.TvAddress.Trim();
        if (string.IsNullOrEmpty(config.DeviceId))
        {
            config.DeviceId = "jellyfin-" + Guid.NewGuid().ToString("N")[..12];
        }

        plugin.SaveConfiguration();

        try
        {
            await _tv.StartPairingAsync(config.TvAddress, config.DeviceId, ct).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("Pair/Finish")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> FinishPairing([FromBody, Required] FinishPairingRequest body, CancellationToken ct)
    {
        var plugin = Plugin.Instance!;
        var config = plugin.Configuration;

        try
        {
            config.AuthToken = await _tv.FinishPairingAsync(config.TvAddress, config.DeviceId, body.Pin.Trim(), ct).ConfigureAwait(false);
            plugin.SaveConfiguration();
            return NoContent();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Log sink for the TV's browser, which has no devtools. Only active with debug logging on.
    /// </summary>
    [HttpPost("Log")]
    [AllowAnonymous]
    [Consumes("text/plain")]
    public async Task<ActionResult> Log()
    {
        if (!IsLocalRequest() || Plugin.Instance?.Configuration.DebugLogging != true)
        {
            return NotFound();
        }

        using var reader = new StreamReader(Request.Body);
        var message = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (message.Length > MaxLogLength)
        {
            message = message[..MaxLogLength];
        }

        _logger.LogInformation("[tv] {Message}", message);
        return NoContent();
    }

    /// <summary>
    /// Logs the TV user in (the same way Quick Connect does) and packs the result into
    /// a URL fragment that vizio.js saves as jellyfin-web's stored login. A fragment
    /// isn't sent to the server, so the token stays out of request logs.
    /// </summary>
    private async Task<string> BuildLoginFragmentAsync(string tvUserId, string pairingDeviceId)
    {
        if (!Guid.TryParse(tvUserId, out var userId) || _userManager.GetUserById(userId) is null)
        {
            return string.Empty;
        }

        // Stable per TV, so each launch replaces the TV's previous login instead of adding one
        var deviceId = pairingDeviceId + "-tv";
        var result = await _sessionManager.AuthenticateDirect(new AuthenticationRequest
        {
            UserId = userId,
            App = "Jellyfin Web",
            AppVersion = _appHost.ApplicationVersionString,
            DeviceId = deviceId,
            DeviceName = "Vizio TV"
        }).ConfigureAwait(false);

        var login = new JsonObject
        {
            ["serverId"] = result.ServerId,
            ["serverName"] = _appHost.FriendlyName,
            ["userId"] = userId.ToString("N"),
            ["accessToken"] = result.AccessToken,
            ["deviceId"] = deviceId
        };
        return "#vizio-login=" + Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(login.ToJsonString())));
    }

    private bool IsLocalRequest()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        return ip is not null && (IPAddress.IsLoopback(ip) || _network.IsInLocalNetwork(ip));
    }

    private ContentResult LaunchPage(string message)
    {
        var html = $$"""
            <!doctype html>
            <html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Jellyfin on TV</title>
            <style>body{font-family:sans-serif;background:#101010;color:#eee;display:flex;flex-direction:column;align-items:center;justify-content:center;min-height:90vh;margin:0 16px;text-align:center}
            a{margin-top:24px;padding:14px 28px;background:#00a4dc;color:#fff;border-radius:8px;text-decoration:none}</style>
            </head><body><p>{{WebUtility.HtmlEncode(message)}}</p><a href="">Launch again</a></body></html>
            """;
        return Content(html, "text/html");
    }

    public class StartPairingRequest
    {
        [Required]
        public string TvAddress { get; set; } = string.Empty;
    }

    public class FinishPairingRequest
    {
        [Required]
        public string Pin { get; set; } = string.Empty;
    }
}
