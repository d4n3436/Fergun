using System;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Fergun.Extensions;

/// <summary>
/// Contains extension methods for <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Adds transient HTTP error and retry policies to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The HTTP client builder.</returns>
    public static IHttpClientBuilder AddRetryPolicy(this IHttpClientBuilder builder)
        => builder.AddTransientHttpErrorPolicy(policyBuilder
            => policyBuilder.OrTransientHttpStatusCode().WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
}