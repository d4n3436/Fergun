using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using Discord;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using Fergun;
using Fergun.Apis.Bing;
using Fergun.Apis.Dictionary;
using Fergun.Apis.Genius;
using Fergun.Apis.Google;
using Fergun.Apis.Musixmatch;
using Fergun.Apis.Urban;
using Fergun.Apis.Wikipedia;
using Fergun.Apis.WolframAlpha;
using Fergun.Apis.Yandex;
using Fergun.Configuration;
using Fergun.Converters;
using Fergun.Data;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Modules;
using Fergun.Services;
using GScraper.DuckDuckGo;
using GScraper.Google;
using GTranslate.Translators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using YoutubeExplode.Search;

// The current directory is changed so the SQLite database is stored in the current folder
// instead of the project folder (if the data source path is relative).
Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

var builder = Host.CreateApplicationBuilder();

TypeDescriptor.AddAttributes(typeof(IEmote), new TypeConverterAttribute(typeof(EmoteConverter)));
builder.Services.AddOptions<StartupOptions>().BindConfiguration(StartupOptions.Startup)
    .PostConfigure(startup =>
    {
        if (startup.MobileStatus)
        {
            MobilePatcher.Patch();
        }
    });
builder.Services.AddOptions<BotListOptions>().BindConfiguration(BotListOptions.BotList);
builder.Services.AddOptions<FergunOptions>().BindConfiguration(FergunOptions.Fergun);

builder.Services.AddSqlite<FergunContext>(builder.Configuration.GetConnectionString("FergunDatabase"));

builder.Services.AddDiscordShardedHost((config, _) =>
{
    config.SocketConfig = new DiscordSocketConfig
    {
        LogLevel = LogSeverity.Verbose,
        GatewayIntents = GatewayIntents.Guilds,
        UseInteractionSnowflakeDate = false,
        LogGatewayIntentWarnings = false,
        FormatUsersInBidirectionalUnicode = false
    };

    config.Token = builder.Configuration.GetSection(StartupOptions.Startup).Get<StartupOptions>()!.Token;
});

builder.Services.AddInteractionService((config, _) => config.LogLevel = LogSeverity.Critical);

builder.Logging.ClearProviders();
builder.Services.AddSerilog(config => config.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddTransient(typeof(IFergunLocalizer<>), typeof(FergunLocalizer<>));
builder.Services.AddSingleton<FergunLocalizationManager>();
builder.Services.AddHostedService<InteractionHandlingService>();
builder.Services.AddHostedService<BotListService>();
builder.Services.ConfigureHttpClientDefaults(b =>
{
    b.AddRetryPolicy();
    b.RemoveAllLoggers();
    b.AddLogger<FergunHttpClientLogger>();
});
builder.Services.AddSingleton(new InteractiveConfig { ReturnAfterSendingPaginator = true, DeferStopSelectionInteractions = false });
builder.Services.AddSingleton<InteractiveService>();
builder.Services.AddSingleton<FergunHttpClientLogger>();
builder.Services.AddHostedService<InteractiveServiceLoggerHost>();
builder.Services.AddSingleton<MusixmatchClientState>();
builder.Services.AddFergunPolicies();

builder.Services.AddHttpClient<IBingVisualSearch, BingVisualSearch>();
builder.Services.AddHttpClient<IYandexImageSearch, YandexImageSearch>().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
builder.Services.AddHttpClient<IGoogleLensClient, GoogleLensClient>();
builder.Services.AddHttpClient<IUrbanDictionary, UrbanDictionary>();
builder.Services.AddHttpClient<IWikipediaClient, WikipediaClient>();
builder.Services.AddHttpClient<IDictionaryClient, DictionaryClient>();
builder.Services.AddHttpClient<IWolframAlphaClient, WolframAlphaClient>();
builder.Services.AddHttpClient<IGeniusClient, GeniusClient>().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
builder.Services.AddHttpClient<IMusixmatchClient, MusixmatchClient>().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
builder.Services.AddHttpClient<ITranslator, GoogleTranslator>();
builder.Services.AddHttpClient<ITranslator, GoogleTranslator2>();
builder.Services.AddHttpClient<GoogleTranslator2>(); // Registered twice so the one added as "itself" can be used in SharedModule
builder.Services.AddHttpClient<ITranslator, YandexTranslator>();
builder.Services.AddHttpClient<ITranslator, MicrosoftTranslator>();
builder.Services.AddHttpClient<SearchClient>();
builder.Services.AddHttpClient<OtherModule>();
builder.Services.AddHttpClient(nameof(GoogleScraper));
builder.Services.AddHttpClient(nameof(DuckDuckGoScraper)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
builder.Services.AddHttpClient("autocomplete", client => client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.ChromeUserAgent));

builder.Services.AddSingleton(s => new MicrosoftTranslator(s.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MicrosoftTranslator)))); // Singleton used in TtsModule and MicrosoftVoiceConverter
builder.Services.AddTransient<IFergunTranslator, FergunTranslator>();
builder.Services.AddTransient(s => new GoogleScraper(s.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GoogleScraper))));
builder.Services.AddTransient(s => new DuckDuckGoScraper(s.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DuckDuckGoScraper))));
builder.Services.AddTransient<SharedModule>();

var host = builder.Build();

host.ApplyMigrations();

await host.RunAsync();