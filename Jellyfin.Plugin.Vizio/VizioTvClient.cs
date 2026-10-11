using System;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Vizio;

/// <summary>
/// Talks to the SmartCast local API: the same pairing and launch calls Vizio's
/// conjure-launcher page makes.
/// </summary>
public sealed class VizioTvClient : IDisposable
{
    private const int Port = 7345;

    // The TV serves a self-signed certificate, so we can't validate it. Only ever
    // used for requests to the configured TV.
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true
        }
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private JsonNode? _pendingPairToken;
    private JsonNode? _pendingChallengeType;

    public async Task StartPairingAsync(string tvAddress, string deviceId, CancellationToken ct)
    {
        var item = await PutAsync(tvAddress, "pairing/start", null, new JsonObject
        {
            ["DEVICE_ID"] = deviceId,
            ["DEVICE_NAME"] = "Jellyfin"
        }, ct).ConfigureAwait(false);

        _pendingPairToken = item["PAIRING_REQ_TOKEN"]?.DeepClone();
        _pendingChallengeType = item["CHALLENGE_TYPE"]?.DeepClone() ?? 1;
    }

    /// <summary>Completes pairing with the PIN shown on the TV and returns the auth token.</summary>
    public async Task<string> FinishPairingAsync(string tvAddress, string deviceId, string pin, CancellationToken ct)
    {
        if (_pendingPairToken is null)
        {
            throw new InvalidOperationException("Pairing hasn't been started.");
        }

        var item = await PutAsync(tvAddress, "pairing/pair", null, new JsonObject
        {
            ["DEVICE_ID"] = deviceId,
            ["CHALLENGE_TYPE"] = _pendingChallengeType?.DeepClone(),
            ["RESPONSE_VALUE"] = pin,
            ["PAIRING_REQ_TOKEN"] = _pendingPairToken.DeepClone()
        }, ct).ConfigureAwait(false);

        _pendingPairToken = null;
        return item["AUTH_TOKEN"]?.ToString()
            ?? throw new InvalidOperationException("TV didn't return an auth token.");
    }

    public Task LaunchAsync(string tvAddress, string authToken, string url, CancellationToken ct)
    {
        return PutAsync(tvAddress, "app/launch", authToken, new JsonObject
        {
            ["VALUE"] = new JsonObject
            {
                ["NAME_SPACE"] = 4,
                ["APP_ID"] = "17",
                ["MESSAGE"] = url
            }
        }, ct);
    }

    private async Task<JsonNode> PutAsync(string tvAddress, string path, string? authToken, JsonObject body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"https://{tvAddress}:{Port}/{path}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        if (authToken is not null)
        {
            request.Headers.Add("AUTH", authToken);
        }

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        JsonNode? json;
        try
        {
            json = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"TV returned HTTP {(int)response.StatusCode}: {text}");
        }

        // The TV reports errors in STATUS.RESULT rather than via HTTP status
        var result = json?["STATUS"]?["RESULT"]?.ToString();
        if (!string.Equals(result, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            var detail = json?["STATUS"]?["DETAIL"]?.ToString();
            throw new InvalidOperationException($"TV rejected {path}: {result} {detail}".Trim());
        }

        return json?["ITEM"] ?? new JsonObject();
    }

    public void Dispose() => _http.Dispose();
}
