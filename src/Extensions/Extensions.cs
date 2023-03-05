using Discord;
using Fergun.Apis.Dictionary;
using Fergun.Apis.Genius;
using Fergun.Apis.Musixmatch;
using Fergun.Apis.Wikipedia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;
using Polly.Extensions.Http;
using Polly.Registry;

namespace Fergun.Extensions;

public static class Extensions
{
    public static IHttpClientBuilder AddRetryPolicy(this IHttpClientBuilder builder)
        => builder.AddTransientHttpErrorPolicy(policyBuilder
            => policyBuilder.OrTransientHttpStatusCode().WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

    public static IServiceCollection AddFergunPolicies(this IServiceCollection services)
    {
        return services.AddMemoryCache()
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
    }

    private static IAsyncPolicy<TResult> CreateAutocompletePolicy<TResult>(this IServiceProvider provider)
    {
        var cacheProvider = provider.GetRequiredService<IAsyncCacheProvider>().AsyncFor<TResult>();
        var policy = Policy.CacheAsync(cacheProvider, new SlidingTtl(TimeSpan.FromHours(2)));
        return Policy.WrapAsync(policy, Policy.TimeoutAsync<TResult>(TimeSpan.FromSeconds(3)));
    }

    public static LogLevel ToLogLevel(this LogSeverity logSeverity)
        => logSeverity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => throw new ArgumentOutOfRangeException(nameof(logSeverity))
        };

    public static string Display(this IInteractionContext context)
    {
        string displayMessage = string.Empty;

        if (context.Channel is IGuildChannel guildChannel)
            displayMessage = $"{guildChannel.Guild.Name}/";

        displayMessage += context.Channel?.Name ?? $"??? (Id: {context.Interaction.ChannelId})";

        return displayMessage;
    }

    public static string Dump<T>(this T obj, int maxDepth = 2)
    {
        using var strWriter = new StringWriter { NewLine = "\n" };
        using var jsonWriter = new CustomJsonTextWriter(strWriter);
        var resolver = new CustomContractResolver(jsonWriter, maxDepth);
        var serializer = new JsonSerializer
        {
            ContractResolver = resolver,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        };
        serializer.Serialize(jsonWriter, obj);
        return strWriter.ToString();
    }
}