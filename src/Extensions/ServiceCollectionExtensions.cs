using System;
using System.Collections.Generic;
using System.Net.Http;
using Fergun.Apis.Dictionary;
using Fergun.Apis.Genius;
using Fergun.Apis.Musixmatch;
using Fergun.Apis.Wikipedia;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;
using Polly.Extensions.Http;
using Polly.Registry;
using Polly.Wrap;

namespace Fergun.Extensions;

/// <summary>
/// Contains extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the cache, retry and timeout policies used in autocomplete handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFergunPolicies(this IServiceCollection services)
        => services.AddMemoryCache()
            .AddSingleton<IAsyncCacheProvider, MemoryCacheProvider>()
            .AddSingleton<IReadOnlyPolicyRegistry<string>, PolicyRegistry>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<IAsyncCacheProvider>().AsyncFor<HttpResponseMessage>();
                var cachePolicy = Policy.CacheAsync(cacheProvider, new SlidingTtl(TimeSpan.FromHours(2)));

                var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError()
                    .OrTransientHttpStatusCode()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

                var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(3));

                return new PolicyRegistry
                {
                    { "GeneralPolicy", Policy.WrapAsync(cachePolicy, retryPolicy) },
                    { "AutocompletePolicy", Policy.WrapAsync(cachePolicy, timeoutPolicy, retryPolicy) },
                    { "GeniusPolicy", provider.CreateAutocompletePolicy<IReadOnlyList<IGeniusSong>>() },
                    { "MusixmatchPolicy", provider.CreateAutocompletePolicy<IReadOnlyList<IMusixmatchSong>>() },
                    { "UrbanPolicy", provider.CreateAutocompletePolicy<IReadOnlyList<string>>() },
                    { "WikipediaPolicy", provider.CreateAutocompletePolicy<IReadOnlyList<IPartialWikipediaArticle>>() },
                    { "WolframPolicy", provider.CreateAutocompletePolicy<IReadOnlyList<string>>() },
                    { "DictionaryPolicy", provider.CreateAutocompletePolicy<IReadOnlyList<IDictionaryWord>>() }
                };
            });

    private static AsyncPolicyWrap<TResult> CreateAutocompletePolicy<TResult>(this IServiceProvider provider)
    {
        var cacheProvider = provider.GetRequiredService<IAsyncCacheProvider>().AsyncFor<TResult>();
        var policy = Policy.CacheAsync(cacheProvider, new SlidingTtl(TimeSpan.FromHours(2)));
        return Policy.WrapAsync(policy, Policy.TimeoutAsync<TResult>(TimeSpan.FromSeconds(3)));
    }
}