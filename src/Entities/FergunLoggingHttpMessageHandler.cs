using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Fergun;

/// <summary>
/// Handles logging of an HTTP request.
/// </summary>
public class FergunLoggingHttpMessageHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunLoggingHttpMessageHandler"/> class with a specified logger.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to log to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public FergunLoggingHttpMessageHandler(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Loggs the request from the sent <see cref="HttpRequestMessage"/>.</remarks>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        Log.RequestEnd(_logger, request, response, stopwatch.Elapsed);

        return response;
    }

    internal static class Log
    {
        public static readonly EventId RequestEndEvent = new(101, "RequestEnd");

        private static readonly LogDefineOptions _skipEnabledCheckLogDefineOptions = new() { SkipEnabledCheck = true };

        private static readonly Action<ILogger, HttpMethod, string?, int, double, Exception?> _requestEnd =
            LoggerMessage.Define<HttpMethod, string?, int, double>(
                LogLevel.Information,
                RequestEndEvent,
                "HTTP {HttpMethod} {Uri} responded {StatusCode} in {ElapsedMilliseconds} ms", _skipEnabledCheckLogDefineOptions);

        public static void RequestEnd(ILogger logger, HttpRequestMessage request, HttpResponseMessage response, TimeSpan duration)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                _requestEnd(logger, request.Method, GetUriString(request.RequestUri), (int)response.StatusCode, duration.TotalMilliseconds, null);
            }
        }

        private static string? GetUriString(Uri? requestUri)
        {
            return requestUri?.IsAbsoluteUri == true
                ? requestUri.AbsoluteUri
                : requestUri?.ToString();
        }
    }
}