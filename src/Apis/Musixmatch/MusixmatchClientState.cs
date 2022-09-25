using System.Net;
using System.Text.Json;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents the state of <see cref="MusixmatchClient"/>. It contains a cached user token used to call the APIs.
/// </summary>
public class MusixmatchClientState
{
    private static readonly Uri _uri = new("https://apic-desktop.musixmatch.com/ws/1.1/token.get?app_id=web-desktop-app-v1.0&format=json");

    private string? _userToken;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _signatureSemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClientState"/> class.
    /// </summary>
    public MusixmatchClientState(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Returns a cached signature secret, or obtains a new one and caches it.
    /// </summary>
    /// <param name="refresh">Whether to get a new user token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the signature secret.</returns>
    /// <exception cref="MusixmatchException"></exception>
    public async ValueTask<string> GetUserTokenAsync(bool refresh = false)
    {
        var client = _httpClientFactory.CreateClient();

        if (!refresh && _userToken is not null)
        {
            return _userToken;
        }

        await _signatureSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!refresh && _userToken is not null)
            {
                return _userToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
            request.Headers.Add("Cookie", "AWSELB=0");

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            var statusCode = (HttpStatusCode)document
                .RootElement
                .GetProperty("message")
                .GetProperty("header")
                .GetProperty("status_code")
                .GetInt32();

            if (statusCode != HttpStatusCode.OK)
            {
                string? hint = document
                    .RootElement
                    .GetProperty("message")
                    .GetProperty("header")
                    .GetProperty("hint")
                    .GetString();

                MusixmatchException.Throw(statusCode, hint);
            }

            string? token = document
                .RootElement
                .GetProperty("message")
                .GetProperty("body")
                .GetProperty("user_token")
                .GetString();

            if (string.IsNullOrEmpty(token) || token == "UpgradeOnlyUpgradeOnlyUpgradeOnlyUpgradeOnly")
            {
                throw new MusixmatchException("Unable to get the Musixmatch user token.");
            }

            _userToken = token;
        }
        finally
        {
            _signatureSemaphore.Release();
        }

        return _userToken;
    }
}