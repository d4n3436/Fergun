using System;
using System.Net.Http;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;

namespace Fergun.Common;

public partial class FergunHttpClientLogger : IHttpClientLogger
{
    private readonly ILogger<FergunHttpClientLogger> _logger;

    public FergunHttpClientLogger(ILogger<FergunHttpClientLogger> logger) => _logger = logger;

    /// <inheritdoc />
    public object? LogRequestStart(HttpRequestMessage request) => null;

    /// <inheritdoc />
    public void LogRequestStop(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed)
        => Log.RequestEnd(_logger, request.Method, Log.GetUriString(request.RequestUri), (int)response.StatusCode, elapsed.TotalMilliseconds);

    /// <inheritdoc />
    public void LogRequestFailed(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception,
        TimeSpan elapsed)
    {
    }

    internal static partial class Log
    {
        [LoggerMessage(101, LogLevel.Information, "HTTP {HttpMethod} {Uri} responded {StatusCode} in {ElapsedMilliseconds} ms", EventName = "RequestEnd")]
        internal static partial void RequestEnd(ILogger logger, HttpMethod httpMethod, string? uri, int statusCode, double elapsedMilliseconds);

        internal static string? GetUriString(Uri? requestUri)
            => requestUri?.IsAbsoluteUri == true
                ? requestUri.AbsoluteUri
                : requestUri?.ToString();
    }
}