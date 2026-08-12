using System;
using System.Net;
using System.Net.Http;
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

    /// <summary>
    /// Adds a retry policy for responses with status code 403 to <paramref name="builder"/>.
    /// </summary>
    /// <remarks>Used for Google as it sporadically returns 403 for otherwise valid scraping requests, and most of them succeed on retry.</remarks>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The HTTP client builder.</returns>
    public static IHttpClientBuilder AddForbiddenRetryPolicy(this IHttpClientBuilder builder)
        => builder.AddPolicyHandler(Policy
            .HandleResult<HttpResponseMessage>(static response => response.StatusCode == HttpStatusCode.Forbidden)
            .WaitAndRetryAsync(1, static _ => TimeSpan.FromSeconds(1)));
}