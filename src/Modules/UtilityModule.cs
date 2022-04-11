using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Wikipedia;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;
using Fergun.Utils;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using Humanizer;
using Microsoft.Extensions.Logging;
using YoutubeExplode.Common;
using YoutubeExplode.Search;

namespace Fergun.Modules;

public class UtilityModule : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly ILogger<UtilityModule> _logger;
    private readonly IFergunLocalizer<UtilityModule> _localizer;
    private readonly SharedModule _shared;
    private readonly InteractiveService _interactive;
    private readonly GoogleTranslator _googleTranslator;
    private readonly GoogleTranslator2 _googleTranslator2;
    private readonly MicrosoftTranslator _microsoftTranslator;
    private readonly YandexTranslator _yandexTranslator;
    private readonly SearchClient _searchClient;
    private readonly IWikipediaClient _wikipediaClient;

    private static readonly Lazy<Language[]> _lazyFilteredLanguages = new(() => Language.LanguageDictionary
        .Values
        .Where(x => x.SupportedServices == (TranslationServices.Google | TranslationServices.Bing | TranslationServices.Yandex | TranslationServices.Microsoft))
        .ToArray());

    public UtilityModule(ILogger<UtilityModule> logger, IFergunLocalizer<UtilityModule> localizer,
        SharedModule shared, InteractiveService interactive, GoogleTranslator googleTranslator, GoogleTranslator2 googleTranslator2,
        MicrosoftTranslator microsoftTranslator, YandexTranslator yandexTranslator, SearchClient searchClient, IWikipediaClient wikipediaClient)
    {
        _logger = logger;
        _localizer = localizer;
        _shared = shared;
        _interactive = interactive;
        _googleTranslator = googleTranslator;
        _googleTranslator2 = googleTranslator2;
        _microsoftTranslator = microsoftTranslator;
        _yandexTranslator = yandexTranslator;
        _searchClient = searchClient;
        _wikipediaClient = wikipediaClient;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [MessageCommand("Bad Translator")]
    public async Task BadTranslator(IMessage message)
        => await BadTranslator(message.GetText());

    [SlashCommand("badtranslator", "Passes a text through multiple, different translators.")]
    public async Task BadTranslator([Summary(description: "The text to use.")] string text,
        [Summary(description: "The amount of times to translate the text (2-10).")] [MinValue(2)] [MaxValue(10)] int chainCount = 8)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await Context.Interaction.RespondWarningAsync(_localizer["The message must contain text."], true);
            return;
        }

        if (chainCount is < 2 or > 10)
        {
            await Context.Interaction.RespondWarningAsync(_localizer["The chain count must be between 2 and 10 (inclusive)."], true);
            return;
        }

        await DeferAsync();

        // Create an aggregated translator manually so we can randomize the initial order of the translators and shift them.
        // Bing Translator is not included because it only allows max. 1000 chars per translation
        var translators = new ITranslator[] { _googleTranslator, _googleTranslator2, _microsoftTranslator, _yandexTranslator };
        translators.Shuffle();
        var badTranslator = new AggregateTranslator(translators);

        var languageChain = new List<ILanguage>(chainCount + 1);
        ILanguage? source = null;
        for (int i = 0; i < chainCount; i++)
        {
            ILanguage target;
            if (i == chainCount - 1)
            {
                target = source!;
            }
            else
            {
                // Get unique and random languages.
                do
                {
                    target = _lazyFilteredLanguages.Value[Random.Shared.Next(_lazyFilteredLanguages.Value.Length)];
                } while (languageChain.Contains(target));
            }

            // Shift the translators to avoid spamming them and get more variety
            var last = translators[^1];
            Array.Copy(translators, 0, translators, 1, translators.Length - 1);
            translators[0] = last;

            ITranslationResult result;
            try
            {
                _logger.LogInformation("Translating to: {target}", target.ISO6391);
                result = await badTranslator.TranslateAsync(text, target);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error translating text {text} ({source} -> {target})", text, source?.ISO6391 ?? "auto", target.ISO6391);
                await Context.Interaction.FollowupWarning(e.Message);
                return;
            }

            if (i == 0)
            {
                source = result.SourceLanguage;
                _logger.LogDebug("Badtranslator: Original language: {source}", source.ISO6391);
                languageChain.Add(source);
            }

            _logger.LogDebug("Badtranslator: Translated from {source} to {target}, Service: {service}", result.SourceLanguage.ISO6391, result.TargetLanguage.ISO6391, result.Service);

            text = result.Translation;
            languageChain.Add(target);
        }

        string embedText = $"**{_localizer["Language Chain"]}**\n{string.Join(" -> ", languageChain.Select(x => x.ISO6391))}\n\n**{_localizer["Result"]}**\n";

        var embed = new EmbedBuilder()
            .WithTitle("Bad translator")
            .WithDescription($"{embedText}{text.Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length)}")
            .WithThumbnailUrl(Constants.BadTranslatorLogoUrl)
            .WithColor(Color.Orange)
            .Build();

        await FollowupAsync(embed: embed);
    }

    [RequireOwner]
    [SlashCommand("cmd", "(Owner only) Executes a command.")]
    public async Task Cmd([Summary(description: "The command to execute")] string command, [Summary("noembed", "No embed.")] bool noEmbed = false)
    {
        await DeferAsync();

        var result = CommandUtils.RunCommand(command);

        if (string.IsNullOrWhiteSpace(result))
        {
            await FollowupAsync(_localizer["No output."]);
        }
        else
        {
            int limit = noEmbed ? DiscordConfig.MaxMessageSize : EmbedBuilder.MaxDescriptionLength;
            string sanitized = Format.Code(result.Replace('`', '´').Truncate(limit - 12), "ansi");
            string? text = null;
            Embed? embed = null;

            if (noEmbed)
            {
                text = sanitized;
            }
            else
            {
                embed = new EmbedBuilder()
                    .WithTitle(_localizer["Command output"])
                    .WithDescription(sanitized)
                    .WithColor(Color.Orange)
                    .Build();
            }

            await FollowupAsync(text, embed: embed);
        }
    }

    [SlashCommand("help", "Information about Fergun 2")]
    public async Task Help()
    {
        var embed = new EmbedBuilder()
            .WithTitle("Fergun 2")
            .WithDescription(_localizer["Fergun2Info", "https://github.com/d4n3436/Fergun/wiki/Command-removal-notice"])
            .WithColor(Color.Orange)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("ping", "Sends the response time of the bot.")]
    public async Task Ping()
    {
        var embed = new EmbedBuilder()
            .WithDescription("Pong!")
            .WithColor(Color.Orange)
            .Build();

        var sw = Stopwatch.StartNew();
        await RespondAsync(embed: embed);
        sw.Stop();

        embed = new EmbedBuilder()
            .WithDescription($"Pong! {sw.ElapsedMilliseconds}ms")
            .WithColor(Color.Orange)
            .Build();

        await Context.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed);
    }

    [SlashCommand("say", "Says something.")]
    public async Task Say([Summary(description: "The text to send.")] string text)
    {
        await RespondAsync(text.Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("stats", "Sends the stats of the bot.")]
    public async Task Stats()
    {
        await DeferAsync();

        long temp;
        var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
        var cpuUsage = (int)await CommandUtils.GetCpuUsageForProcessAsync();
        string? cpu = null;
        long? totalRamUsage = null;
        long processRamUsage = 0;
        long? totalRam = null;
        string? os = RuntimeInformation.OSDescription;

        if (OperatingSystem.IsLinux())
        {
            // CPU Name
            if (File.Exists("/proc/cpuinfo"))
            {
                cpu = File.ReadAllLines("/proc/cpuinfo")
                    .FirstOrDefault(x => x.StartsWith("model name", StringComparison.OrdinalIgnoreCase))?
                    .Split(':')
                    .ElementAtOrDefault(1)?
                    .Trim();
            }

            if (string.IsNullOrWhiteSpace(cpu))
            {
                cpu = CommandUtils.RunCommand("lscpu")?
                    .Split('\n')
                    .FirstOrDefault(x => x.StartsWith("model name", StringComparison.OrdinalIgnoreCase))?
                    .Split(':')
                    .ElementAtOrDefault(1)?
                    .Trim();

                if (string.IsNullOrWhiteSpace(cpu))
                {
                    cpu = "?";
                }
            }

            // OS Name
            if (File.Exists("/etc/lsb-release"))
            {
                var distroInfo = File.ReadAllLines("/etc/lsb-release");
                os = distroInfo.ElementAtOrDefault(3)?.Split('=').ElementAtOrDefault(1)?.Trim('\"');
            }

            // Total RAM & total RAM usage
            var output = CommandUtils.RunCommand("free -m")?.Split(Environment.NewLine);
            var memory = output?.ElementAtOrDefault(1)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (long.TryParse(memory?.ElementAtOrDefault(1), out temp)) totalRam = temp;
            if (long.TryParse(memory?.ElementAtOrDefault(2), out temp)) totalRamUsage = temp;

            // Process RAM usage
            processRamUsage = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
        }
        else if (OperatingSystem.IsWindows())
        {
            // CPU Name
            cpu = CommandUtils.RunCommand("wmic cpu get name")
                ?.Trim()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .ElementAtOrDefault(1);

            // Total RAM & total RAM usage
            var output = CommandUtils.RunCommand("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value")
                ?.Trim()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            if (output?.Length > 1)
            {
                long freeRam = 0;
                var split = output[0].Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 1 && long.TryParse(split[1], out temp))
                {
                    freeRam = temp / 1024;
                }

                split = output[1].Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 1 && long.TryParse(split[1], out temp))
                {
                    totalRam = temp / 1024;
                }

                if (totalRam != null && freeRam != 0)
                {
                    totalRamUsage = totalRam - freeRam;
                }
            }

            // Process RAM usage
            processRamUsage = Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024;
        }

        int totalUsers = 0;
        foreach (var guild in Context.Client.Guilds)
        {
            totalUsers += guild.MemberCount;
        }

        int totalUsersInShard = 0;
        int shardId = Context.Channel.IsPrivate() ? 0 : Context.Client.GetShardIdFor(Context.Guild);
        foreach (var guild in Context.Client.GetShard(shardId).Guilds)
        {
            totalUsersInShard += guild.MemberCount;
        }

        string version = $"v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}";

        var elapsed = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime;

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["Fergun Stats"])
            .AddField(_localizer["Operating System"], os, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("CPU", cpu, true)
            .AddField(_localizer["CPU Usage"], cpuUsage + "%", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["RAM Usage"],
                $"{processRamUsage}MB ({(totalRam == null ? 0 : Math.Round((double)processRamUsage / totalRam.Value * 100, 2))}%) " +
                $"/ {(totalRamUsage == null || totalRam == null ? "?MB" : $"{totalRamUsage}MB ({Math.Round((double)totalRamUsage.Value / totalRam.Value * 100, 2)}%)")} " +
                $"/ {totalRam?.ToString() ?? "?"}MB", true)
            .AddField(_localizer["Library"], $"Discord.Net v{DiscordConfig.Version}", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Bot Version"], version, true)
            .AddField(_localizer["Total Servers"], $"{Context.Client.Guilds.Count} (Shard: {Context.Client.GetShard(shardId).Guilds.Count})", true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Total Users"], $"{totalUsers} (Shard: {totalUsersInShard})", true)
            .AddField(_localizer["Shard ID"], shardId, true)
            .AddField("\u200b", "\u200b", true)
            .AddField("Shards", Context.Client.Shards.Count, true)
            .AddField(_localizer["Uptime"], elapsed.Humanize(), true)
            .AddField("\u200b", "\u200b", true)
            .AddField(_localizer["Bot Owner"], owner, true);

        builder.WithColor(Color.Orange);

        await FollowupAsync(embed: builder.Build());
    }

    [MessageCommand("Translate")]
    public async Task Translate(IMessage message)
        => await Translate(message.GetText(), Context.Interaction.GetLanguageCode());

    [SlashCommand("translate", "Translates a text.")]
    public async Task Translate([Summary(description: "The text to translate.")] string text,
        [Autocomplete(typeof(TranslateAutocompleteHandler))] [Summary(description: "Target language (name, code or alias).")] string target,
        [Autocomplete(typeof(TranslateAutocompleteHandler))] [Summary(description: "Source language (name, code or alias).")] string? source = null,
        [Summary(description: "Whether to respond ephemerally.")] bool ephemeral = false)
        => await _shared.TranslateAsync(Context.Interaction, text, target, source, ephemeral);

    [MessageCommand("TTS")]
    public async Task TTS(IMessage message)
        => await TTS(message.GetText());

    [SlashCommand("tts", "Converts text into synthesized speech.")]
    public async Task TTS([Summary(description: "The text to convert.")] string text,
        [Autocomplete(typeof(TtsAutocompleteHandler))] [Summary(description: "The target language.")] string? target = null,
        [Summary(description: "Whether to respond ephemerally.")] bool ephemeral = false)
        => await _shared.TtsAsync(Context.Interaction, text, target, ephemeral);

    [SlashCommand("wikipedia", "Searches for Wikipedia articles.")]
    public async Task Wikipedia([Autocomplete(typeof(WikipediaAutocompleteHandler))] [Summary(description: "The search query.")] string query)
    {
        await DeferAsync();

        var articles = (await _wikipediaClient.GetArticlesAsync(query, Context.Interaction.GetLanguageCode())).ToArray();

        if (articles.Length == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."]);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(articles.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes()
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);

        PageBuilder GeneratePage(int index)
        {
            var article = articles[index];

            var page = new PageBuilder()
                .WithTitle(article.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithUrl($"https://{Context.Interaction.GetLanguageCode()}.wikipedia.org/?curid={article.Id}")
                .WithThumbnailUrl($"https://commons.wikimedia.org/w/index.php?title=Special:Redirect/file/Wikipedia-logo-v2-{Context.Interaction.GetLanguageCode()}.png")
                .WithDescription(article.Extract.Truncate(EmbedBuilder.MaxDescriptionLength))
                .WithFooter(_localizer["Wikipedia Search | Page {0} of {1}", index + 1, articles.Length])
                .WithColor(Color.Orange);

            if (Context.Channel.IsNsfw() && article.Image is not null)
            {
                if (article.Image.Width >= 500 && article.Image.Height >= 500)
                {
                    page.WithImageUrl(article.Image.Url);
                }
                else
                {
                    page.WithThumbnailUrl(article.Image.Url);
                }
            }

            return page;
        }
    }

    [SlashCommand("youtube", "Sends a paginator containing YouTube videos.")]
    public async Task YouTube([Autocomplete(typeof(YouTubeAutocompleteHandler))] [Summary(description: "The query.")] string query)
    {
        await DeferAsync();

        var videos = await _searchClient.GetVideosAsync(query).Take(10);

        switch (videos.Count)
        {
            case 0:
                await Context.Interaction.FollowupWarning(_localizer["No results."]);
                break;

            case 1:
                await Context.Interaction.FollowupAsync(videos[0].Url);
                break;

            default:
                var paginator = new StaticPaginatorBuilder()
                    .AddUser(Context.User)
                    .WithPages(videos.Select((x, i) => new PageBuilder { Text = $"{x.Url}\n{_localizer["Page {0} of {1}", i + 1, videos.Count]}" } as IPageBuilder).ToArray())
                    .WithActionOnCancellation(ActionOnStop.DisableInput)
                    .WithActionOnTimeout(ActionOnStop.DisableInput)
                    .WithFooter(PaginatorFooter.None)
                    .WithFergunEmotes()
                    .Build();

                await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);
                break;
        }
    }
}