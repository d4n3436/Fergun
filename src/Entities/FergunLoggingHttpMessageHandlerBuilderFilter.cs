using System;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Fergun;

internal sealed class FergunLoggingHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly ILoggerFactory _loggerFactory;

    public FergunLoggingHttpMessageHandlerBuilderFilter(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return builder =>
        {
            next(builder);

            string loggerName = !string.IsNullOrEmpty(builder.Name) ? builder.Name : "Default";
            var innerLogger = _loggerFactory.CreateLogger($"System.Net.Http.HttpClient.{loggerName}.ClientHandler");

            builder.AdditionalHandlers.Add(new FergunLoggingHttpMessageHandler(innerLogger));
        };
    }
}