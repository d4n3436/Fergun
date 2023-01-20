using Polly;
using System.Text.Json;
using Polly.RateLimit;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents the state of <see cref="MusixmatchClient"/>. It contains a cached user token used to call the APIs.
/// </summary>
public class MusixmatchClientState
{
    private static readonly Uri _uri = new("https://apic-desktop.musixmatch.com/ws/1.1/token.get?app_id=web-desktop-app-v1.0&format=json");

    private string? _userToken;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);
    private readonly AsyncRateLimitPolicy<string> _rateLimitPolicy = Policy.RateLimitAsync<string>(1, TimeSpan.FromMinutes(1), 2);

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClientState"/> class.
    /// </summary>
    public MusixmatchClientState(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Returns a cached user token, or obtains a new one and caches it.
    /// </summary>
    /// <param name="refresh">Whether to get a new user token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the user token.</returns>
    /// <exception cref="MusixmatchException"></exception>
    public async ValueTask<string> GetUserTokenAsync(bool refresh = false)
    {
        if (!refresh && _userToken is not null)
        {
            return _userToken;
        }

        await _tokenSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!refresh && _userToken is not null)
            {
                return _userToken;
            }

            _userToken = null; // Invalidate the token
            _userToken = await _rateLimitPolicy.ExecuteAsync(FetchUserTokenAsync).ConfigureAwait(false);
        }
        finally
        {
            _tokenSemaphore.Release();
        }

        return _userToken;
    }

    /// <summary>
    /// Gets a new token.
    /// </summary>
    /// <returns>A new token.</returns>
    /// <exception cref="MusixmatchException"></exception>
    private async Task<string> FetchUserTokenAsync()
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
        request.Headers.Add("Cookie", "AWSELB=0");

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        MusixmatchClient.ThrowIfNotSuccessful(document.RootElement);

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

        return token;
    }
}