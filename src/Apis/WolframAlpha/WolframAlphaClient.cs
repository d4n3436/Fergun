using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents the default WolframAlpha client.
/// </summary>
public sealed class WolframAlphaClient : IWolframAlphaClient, IDisposable
{
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";
    private static readonly Uri _resultsUri = new("wss://www.wolframalpha.com/n/v1/api/fetcher/results");
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WolframAlphaClient"/> class.
    /// </summary>
    public WolframAlphaClient()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WolframAlphaClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public WolframAlphaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetAutocompleteResultsAsync(string input)
    {
        EnsureNotDisposed();

        await using var stream = await _httpClient.GetStreamAsync(new Uri($"https://www.wolframalpha.com/n/v1/api/autocomplete/?i={Uri.EscapeDataString(input)}")).ConfigureAwait(false);

        var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        return document
            .RootElement
            .GetProperty("results")
            .EnumerateArray()
            .Select(x => x.GetProperty("input").GetString()!);
    }

    /// <inheritdoc/>
    public async Task<IWolframAlphaResult> GetResultsAsync(string input, string language, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(language);
        cancellationToken.ThrowIfCancellationRequested();

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("User-Agent", _defaultUserAgent);

        await ws.ConnectAsync(_resultsUri, cancellationToken).ConfigureAwait(false);

        var encodedInput = JsonEncodedText.Encode(input);

        using var stream = new MemoryStream(126 + language.Length * 2 + encodedInput.EncodedUtf8Bytes.Length);
        await using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "init");
        writer.WriteString("lang", language);
        
        writer.WriteStartArray("messages");
        writer.WriteStartObject();
        writer.WriteString("type", "newQuery");
        writer.WriteNull("locationId");
        writer.WriteString("language", language);
        writer.WriteBoolean("requestSidebarAd", false);
        writer.WriteString("input", encodedInput);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WriteEndObject();

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await ws.SendAsync(stream.GetBuffer().AsMemory(0, (int)writer.BytesCommitted), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);

        var wolframResult = new WolframAlphaResult();
        var pods = new List<IWolframAlphaPod>();
        var positions = new HashSet<int>();

        bool exit = false;
        while (!exit && ws.State == WebSocketState.Open)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OwnedMemorySegment? start = null;
            OwnedMemorySegment? end = null;

            try
            {
                ValueWebSocketReceiveResult result;

                do
                {
                    var owner = MemoryPool<byte>.Shared.Rent(4096);
                    result = await ws.ReceiveAsync(owner.Memory, cancellationToken).ConfigureAwait(false);

                    var memory = owner.Memory[..result.Count];

                    if (start is null)
                    {
                        start = new OwnedMemorySegment(owner, memory);
                    }
                    else if (end is null)
                    {
                        end = start.Append(owner, memory);
                    }
                    else
                    {
                        end = end.Append(owner, memory);
                    }
                } while (!result.EndOfMessage);
            }
            catch
            {
                var current = start;

                while (current is not null)
                {
                    current.Dispose();
                    current = current.Next as OwnedMemorySegment;
                }

                continue;
            }

            var sequence = end is null ? new ReadOnlySequence<byte>(start.Memory) : new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length);

            try
            {
                using var document = JsonDocument.Parse(sequence);
                string? type = document.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "pods":
                        foreach (var pod in document.RootElement.GetProperty("pods").EnumerateArray())
                        {
                            if (!pod.GetProperty("error").GetBoolean() &&
                                pod.TryGetProperty("numsubpods", out var subPodCount) && subPodCount.GetInt32() != 0 &&
                                positions.Add(pod.GetProperty("position").GetInt32()))
                            {
                                pods.Add(pod.Deserialize<WolframAlphaPod>()!);
                            }
                        }
                        break;

                    case "didyoumean": // After queryComplete, the API returns info about one of the interpretations from didyoumean
                        wolframResult.Type = WolframAlphaResultType.DidYouMean;
                        string[] values = document
                            .RootElement
                            .GetProperty("didyoumean")
                            .EnumerateArray()
                            .Select(x => x.GetProperty("val").GetString()!)
                            .ToArray();

                        wolframResult.DidYouMean = Array.AsReadOnly(values);
                        break;

                    case "futureTopic":
                        wolframResult.Type = WolframAlphaResultType.FutureTopic;
                        wolframResult.FutureTopic = document.RootElement.GetProperty("futureTopic").Deserialize<WolframAlphaFutureTopic>();
                        break;

                    case "noResult":
                        wolframResult.Type = WolframAlphaResultType.NoResult;
                        break;

                    case "queryComplete":
                        exit = true;
                        if (wolframResult.Type == WolframAlphaResultType.Unknown)
                        {
                            wolframResult.Type = WolframAlphaResultType.Success;
                        }
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
                        break;

                    case "error":
                        exit = true;
                        wolframResult.Type = WolframAlphaResultType.Error;
                        wolframResult.StatusCode = document.RootElement.GetProperty("status").GetInt32();
                        wolframResult.ErrorMessage = document.RootElement.GetProperty("message").GetString();
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
                        break;

                }
            }
            finally
            {
                var current = start;

                while (current is not null)
                {
                    current.Dispose();
                    current = current.Next as OwnedMemorySegment;
                }
            }
        }

        pods.Sort((x, y) => x.Position.CompareTo(y.Position));
        wolframResult.Pods = pods.AsReadOnly();
        
        return wolframResult;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WolframAlphaClient));
        }
    }
}