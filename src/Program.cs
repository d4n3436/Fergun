using Discord;
using Discord.Addons.Hosting;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun;
using Fergun.Apis.Bing;
using Fergun.Apis.Urban;
using Fergun.Apis.Yandex;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Modules;
using Fergun.Services;
using GScraper.Brave;
using GScraper.DuckDuckGo;
using GScraper.Google;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.SystemConsole.Themes;
using YoutubeExplode.Search;

await Host.CreateDefaultBuilder()
    .ConfigureDiscordShardedHost((context, config) =>
    {
        config.SocketConfig = new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            GatewayIntents = GatewayIntents.Guilds,
            UseInteractionSnowflakeDate = false,
            LogGatewayIntentWarnings = false
        };

        config.Token = context.Configuration["Token"];
    })
    .UseInteractionService((_, config) =>
    {
        config.LogLevel = LogSeverity.Verbose;
        config.DefaultRunMode = RunMode.Async;
        config.UseCompiledLambda = true;
    })
    .ConfigureLogging(logging => logging.ClearProviders())
    .UseSerilog((_, config) =>
    {
        config.MinimumLevel.Debug()
            .Filter.ByExcluding(e => e.Level == LogEventLevel.Debug && Matching.FromSource("Discord.WebSocket.DiscordShardedClient").Invoke(e) && e.MessageTemplate.Render(e.Properties).ContainsAny("Connected to", "Disconnected from"))
            .Filter.ByExcluding(e => e.Level == LogEventLevel.Warning && Matching.FromSource("Discord.WebSocket.DiscordShardedClient").Invoke(e) && e.MessageTemplate.Render(e.Properties).Contains("Unknown Dispatch"))
            .Filter.ByExcluding(e => e.Level <= LogEventLevel.Debug && Matching.FromSource("Microsoft.Extensions.Http").Invoke(e))
            .WriteTo.Console(LogEventLevel.Debug, theme: AnsiConsoleTheme.Literate)
            .WriteTo.Async(logger => logger.File("logs/log-.txt", LogEventLevel.Debug, rollingInterval: RollingInterval.Day));
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<InteractionHandlingService>();
        services.AddSingleton<InteractiveService>();
        services.AddFergunPolicies();

        services.AddHttpClient<BingVisualSearch>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<YandexImageSearch>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<IUrbanDictionary, UrbanDictionary>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<GoogleTranslator>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<GoogleTranslator2>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<YandexTranslator>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        // We have to register the named client and service separately because Bing Translator and Microsoft Translator aren't stateless,
        // They store a token required to make API calls that is obtained once and updated occasionally, since AddHttpClient<TClient>
        // adds the services with a transient scope, this means that the token would be obtained every time the services are used.
        services.AddHttpClient(nameof(MicrosoftTranslator))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddSingleton(s => new MicrosoftTranslator(s.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MicrosoftTranslator))));

        services.AddHttpClient(nameof(BingTranslator))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddSingleton(s => new BingTranslator(s.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(BingTranslator))));

        services.AddHttpClient<SearchClient>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<UtilityModule>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient(nameof(GoogleScraper))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient(nameof(DuckDuckGoScraper))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient(nameof(BraveScraper))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient("autocomplete", client => client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.ChromeUserAgent))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30));

        services.AddSingleton<AggregateTranslator>();
        services.AddSingleton(x => new GoogleScraper(x.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GoogleScraper))));
        services.AddSingleton(x => new DuckDuckGoScraper(x.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DuckDuckGoScraper))));
        services.AddSingleton(x => new BraveScraper(x.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(BraveScraper))));
        services.AddTransient<SharedModule>();
    }).RunConsoleAsync();