using Discord;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                    { "AutocompletePolicy", Policy.WrapAsync(cachePolicy, retryPolicy, timeoutPolicy) }
                };
            });
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
            _ => throw new ArgumentOutOfRangeException(nameof(logSeverity), logSeverity.ToString())
        };

    public static string Display(this IInteractionContext context)
    {
        string displayMessage = context.Channel.Name;

        if (context.Channel is IGuildChannel guildChannel)
            displayMessage += $"/{guildChannel.Guild.Name}";

        return displayMessage;
    }

    /// <summary>
    /// Adds Fergun emotes.
    /// </summary>
    /// <param name="builder">A paginator builder.</param>
    /// <returns>This builder.</returns>
    public static TBuilder WithFergunEmotes<TPaginator, TBuilder>(this PaginatorBuilder<TPaginator, TBuilder> builder)
        where TPaginator : Paginator
        where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
    {
        builder.Options.Clear();

        builder.AddOption(Emoji.Parse("⏮️"), PaginatorAction.SkipToStart);
        builder.AddOption(Emoji.Parse("◀️"), PaginatorAction.Backward);
        builder.AddOption(Emoji.Parse("▶️"), PaginatorAction.Forward);
        builder.AddOption(Emoji.Parse("⏭️"), PaginatorAction.SkipToEnd);
        builder.AddOption(Emoji.Parse("🛑"), PaginatorAction.Exit);

        return (TBuilder)builder;
    }
}