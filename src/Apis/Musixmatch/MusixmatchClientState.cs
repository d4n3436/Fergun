using System.Text;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents the state of <see cref="MusixmatchClient"/>. It contains a cached HMAC key (signature secret) used to call the APIs.
/// </summary>
public class MusixmatchClientState
{
    private static ReadOnlySpan<byte> SignatureSecretStart => Encoding.UTF8.GetBytes("apiUrlSigning:!0,signatureSecret:\"");

    private static readonly Uri _jsUri = new("https://s.mxmcdn.net/site/js/app-bd6761883f732df7f43e.js");
    private static readonly TimeSpan _defaultAge = TimeSpan.FromDays(2);

    private readonly SemaphoreSlim _signatureSemaphore = new(1, 1);
    private byte[] _signatureSecret = Array.Empty<byte>();
    private double _maxAge = 315360000; // 5256 minutes (3.65 days)
    private DateTimeOffset _expirationDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClientState"/> class.
    /// </summary>
    public MusixmatchClientState()
    {
    }

    /// <summary>
    /// Returns a cached signature secret, or obtains a new one and caches it.
    /// </summary>
    /// <param name="httpClient">An <see cref="HttpClient"/> instance.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the signature secret.</returns>
    /// <exception cref="MusixmatchException"></exception>
    public async ValueTask<byte[]> GetSignatureSecretAsync(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (_expirationDate > DateTimeOffset.UtcNow)
        {
            return _signatureSecret;
        }

        await _signatureSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_expirationDate > DateTimeOffset.UtcNow)
            {
                return _signatureSecret;
            }

            using var response = await httpClient.GetAsync(_jsUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            byte[] js = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            int start = js.AsSpan().IndexOf(SignatureSecretStart);
            if (start == -1)
            {
                throw new MusixmatchException("Unable to get Musixmatch signature secret.");
            }

            start += SignatureSecretStart.Length;

            int length = js.AsSpan(start).IndexOf((byte)'"');
            if (length == -1)
            {
                throw new MusixmatchException("Unable to get Musixmatch signature secret.");
            }

            _maxAge = response.Headers.CacheControl?.MaxAge?.TotalMilliseconds / 1000 ?? _maxAge;
            _expirationDate = DateTimeOffset.UtcNow.AddMilliseconds(_maxAge).Subtract(response.Headers.Age.GetValueOrDefault(_defaultAge)); // Use _defaultAge if there's no Age header (which shouldn't happen)

            _signatureSecret = js.AsSpan(start, length).ToArray(); // Copy to a new array so the 3MB array we got can be reclaimed by the GC
        }
        finally
        {
            _signatureSemaphore.Release();
        }

        return _signatureSecret;
    }
}