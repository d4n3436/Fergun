using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Fergun.Apis.Dictionary;
using Fergun.Apis.Wikipedia;
using Fergun.Apis.WolframAlpha;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Modules.Handlers;
using Fergun.Preconditions;
using GTranslate;
using GTranslate.Results;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using Color = Discord.Color;

namespace Fergun.Modules;

[Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod)]
public class UtilityModule : InteractionModuleBase
{
    private static IReadOnlyCollection<RestApplicationCommand>? _cachedComands;
    private static readonly DrawingOptions _cachedDrawingOptions = new();
    private static readonly PngEncoder _cachedPngEncoder = new() { CompressionLevel = PngCompressionLevel.BestCompression, SkipMetadata = true };
    private static readonly Lazy<Language[]> _lazyFilteredLanguages = new(() => Language.LanguageDictionary
        .Values
        .Where(x => x.SupportedServices == (TranslationServices.Google | TranslationServices.Bing | TranslationServices.Yandex | TranslationServices.Microsoft))
        .ToArray());

    private readonly ILogger<UtilityModule> _logger;
    private readonly IFergunLocalizer<UtilityModule> _localizer;
    private readonly StartupOptions _startupOptions;
    private readonly FergunOptions _fergunOptions;
    private readonly SharedModule _shared;
    private readonly InteractionService _commands;
    private readonly InteractiveService _interactive;
    private readonly IFergunTranslator _translator;
    private readonly IDictionaryClient _dictionary;
    private readonly SearchClient _searchClient;
    private readonly IWikipediaClient _wikipediaClient;
    private readonly IWolframAlphaClient _wolframAlphaClient;

    public UtilityModule(ILogger<UtilityModule> logger, IFergunLocalizer<UtilityModule> localizer, IOptions<StartupOptions> startupOptions,
        IOptionsSnapshot<FergunOptions> fergunOptions, SharedModule shared, InteractionService commands, InteractiveService interactive,
        IDictionaryClient dictionary, IFergunTranslator translator, SearchClient searchClient, IWikipediaClient wikipediaClient, IWolframAlphaClient wolframAlphaClient)
    {
        _logger = logger;
        _localizer = localizer;
        _startupOptions = startupOptions.Value;
        _fergunOptions = fergunOptions.Value;
        _shared = shared;
        _commands = commands;
        _interactive = interactive;
        _dictionary = dictionary;
        _translator = translator;
        _searchClient = searchClient;
        _wikipediaClient = wikipediaClient;
        _wolframAlphaClient = wolframAlphaClient;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [UserCommand("Avatar")]
    public async Task<RuntimeResult> AvatarUserCommandAsync(IUser user)
        => await AvatarAsync(user);

    [SlashCommand("avatar", "Displays the avatar of a user.")]
    public async Task<RuntimeResult> AvatarAsync([Summary(description: "The user.")] IUser user,
        [Summary(description: "An specific avatar type.")] AvatarType type = AvatarType.FirstAvailable)
    {
        string? url;
        string title;

        switch (type)
        {
            case AvatarType.FirstAvailable:
                url = (user as IGuildUser)?.GetGuildAvatarUrl(size: 2048) ?? user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();
                title = user.ToString()!;
                break;

            case AvatarType.Server:
                url = (user as IGuildUser)?.GetGuildAvatarUrl(size: 2048);
                if (url is null)
                {
                    return FergunResult.FromError(_localizer["NoServerAvatar", user]);
                }

                title = $"{user} ({_localizer["Server"]})";
                break;

            case AvatarType.Global:
                url = user.GetAvatarUrl(size: 2048);
                if (url is null)
                {
                    return FergunResult.FromError(_localizer["NoGlobalAvatar", user]);
                }

                title = $"{user} ({_localizer["Global"]})";
                break;

            default:
                url = user.GetDefaultAvatarUrl();
                title = $"{user} ({_localizer["Default"]})";
                break;
        }

        var builder = new EmbedBuilder
        {
            Title = title,
            ImageUrl = url,
            Color = Color.Orange
        };

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }

    [Ratelimit(1, Constants.GlobalRatelimitPeriod)]
    [MessageCommand("Bad Translator")]
    public async Task<RuntimeResult> BadTranslatorAsync(IMessage message)
        => await BadTranslatorAsync(message.GetText());

    [Ratelimit(1, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("bad-translator", "Passes a text through multiple, different translators.")]
    public async Task<RuntimeResult> BadTranslatorAsync([Summary(description: "The text to use.")] string text,
        [Summary(description: "The amount of times to translate the text (2-10).")] [MinValue(2)] [MaxValue(10)] int chainCount = 8)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["TextMustNotBeEmpty"], true);
        }

        if (chainCount is < 2 or > 10)
        {
            return FergunResult.FromError(_localizer["ChainCountMustBeInRange", 2, 10], true);
        }

        await Context.Interaction.DeferAsync();

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
                }
                while (languageChain.Contains(target));
            }

            _translator.Randomize();

            ITranslationResult result;
            try
            {
                _logger.LogInformation("Translating to: {Target}", target.ISO6391);
                result = await _translator.TranslateAsync(text, target);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error translating text {Text} ({Source} -> {Target})", text, source?.ISO6391 ?? "auto", target.ISO6391);
                return FergunResult.FromError(e.Message);
            }
            if (i == 0)
            {
                source = result.SourceLanguage;
                _logger.LogDebug("Badtranslator: Original language: {Source}", source.ISO6391);
                languageChain.Add(source);
            }

            _logger.LogDebug("Badtranslator: Translated from {Source} to {Target}, Service: {Service}", result.SourceLanguage.ISO6391, result.TargetLanguage.ISO6391, result.Service);

            text = result.Translation;
            languageChain.Add(target);
        }

        string embedText = $"**{_localizer["LanguageChain"]}**\n{string.Join(" -> ", languageChain.Select(x => x.ISO6391))}\n\n**{_localizer["Result"]}**\n";

        var embed = new EmbedBuilder()
            .WithTitle("Bad translator")
            .WithDescription($"{embedText}{text.Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length)}")
            .WithThumbnailUrl(Constants.BadTranslatorLogoUrl)
            .WithColor(Color.Orange)
            .Build();

        await Context.Interaction.FollowupAsync(embed: embed);

        return FergunResult.FromSuccess();
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("color", "Displays a color.")]
    public async Task<RuntimeResult> ColorAsync([Summary(description: "A color name, hex string or raw value. Leave empty to get a random color.")]
        System.Drawing.Color color = default)
    {
        if (color.IsEmpty)
        {
            color = System.Drawing.Color.FromArgb(Random.Shared.Next((int)(Color.MaxDecimalValue + 1)));
        }

        using var image = new Image<Rgba32>(500, 500);

        image.Mutate(x => x.Fill(_cachedDrawingOptions, SixLabors.ImageSharp.Color.FromRgb(color.R, color.G, color.B)));
        await using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream, _cachedPngEncoder);
        stream.Seek(0, SeekOrigin.Begin);

        string hex = $"{color.R:X2}{color.G:X2}{color.B:X2}";

        var builder = new EmbedBuilder()
            .WithTitle($"#{hex}{(color.IsNamedColor ? $" ({color.Name})" : string.Empty)}")
            .WithImageUrl($"attachment://{hex}.png")
            .WithFooter($"R: {color.R}, G: {color.G}, B: {color.B}")
            .WithColor((Color)color);

        await Context.Interaction.RespondWithFileAsync(new FileAttachment(stream, $"{hex}.png"), embed: builder.Build());

        return FergunResult.FromSuccess();
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("define", "Gets the definitions of a word from Dictionary.com (only English).")]
    public async Task<RuntimeResult> DefineAsync(
    [MaxLength(100)] [Autocomplete(typeof(DictionaryAutocompleteHandler))]
    [Summary(description: "The word to get its defintions.")] string word)
    {
        await Context.Interaction.DeferAsync();

        var result = await _dictionary.GetDefinitionsAsync(word);

        var entries = result.Data?.Content
            .FirstOrDefault(content => content.Source == "luna")?
            .Entries;

        if (entries is null)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        var maxIndexes = new List<int>();
        var pages = new List<List<PageBuilder>>();
        var extraInfos = new List<IPage?>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            string title = DictionaryFormatter.FormatEntry(entry);
            var innerPages = new List<PageBuilder>();

            for (int j = 0; j < entry.PartOfSpeechBlocks.Count; j++)
            {
                var block = entry.PartOfSpeechBlocks[j];
                string description = DictionaryFormatter.FormatPartOfSpeechBlock(block);

                var builder = new PageBuilder()
                    .WithTitle(title)
                    .WithDescription(description)
                    .WithFooter($"Dictionary.com results | Definition {i + 1} of {entries.Count} (Category {j + 1} of {entry.PartOfSpeechBlocks.Count})")
                    .WithColor(Color.Blue);

                innerPages.Add(builder);
            }

            pages.Add(innerPages);
            maxIndexes.Add(innerPages.Count - 1);

            string extraInfo = DictionaryFormatter.FormatExtraInformation(entry);
            if (!string.IsNullOrEmpty(extraInfo))
            {
                var extraInfoPage = new PageBuilder()
                    .WithDescription(extraInfo.Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithColor(Color.Blue)
                    .Build();

                extraInfos.Add(extraInfoPage);
            }
            else
            {
                extraInfos.Add(null);
            }
        }

        var actions = new List<PaginatorAction>();

        if (pages.Count > 1 || pages[0].Count > 1)
        {
            actions.Add(PaginatorAction.Backward);
            actions.Add(PaginatorAction.Forward);
        }

        if (pages.Count > 1)
        {
            actions.Add(PaginatorAction.SkipToStart);
            actions.Add(PaginatorAction.SkipToEnd);
        }

        DictionaryPaginator? paginator = null;
        paginator = new DictionaryPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithCacheLoadedPages(false)
            .WithMaxPageIndex(pages.Count - 1)
            .WithMaxCategoryIndexes(maxIndexes)
            .WithExtraInformation(extraInfos)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_fergunOptions, actions.ToArray())
            .AddOption(_fergunOptions.ExtraEmotes.InfoEmote, PaginatorAction.Jump)
            .AddOption(_fergunOptions.PaginatorEmotes[PaginatorAction.Exit], PaginatorAction.Exit)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(20), InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int entryIndex) => pages[entryIndex][paginator!.CurrentCategoryIndex];
    }

    [SlashCommand("help", "Information about Fergun.")]
    public async Task<RuntimeResult> HelpAsync()
    {
        var responseType = InteractionResponseType.ChannelMessageWithSource;

        if (_cachedComands is null)
        {
            responseType = InteractionResponseType.DeferredChannelMessageWithSource;
            await Context.Interaction.DeferAsync();
        }

        if (_startupOptions.TestingGuildId == 0)
        {
            _cachedComands ??= await ((ShardedInteractionContext)Context).Client.Rest.GetGlobalApplicationCommands(true);
        }
        else
        {
            _cachedComands ??= await ((ShardedInteractionContext)Context).Client.Rest.GetGuildApplicationCommands(_startupOptions.TestingGuildId, true);
        }

        string description = _localizer["Fergun2Info"];

        var links = new List<string>();

        if (_fergunOptions.SupportServerUrl is not null)
        {
            links.Add(Format.Url(_localizer["Support"], _fergunOptions.SupportServerUrl));
            description += $"\n\n{_localizer["Fergun2SupportInfo"]}";
        }

        description += $"\n\n{_localizer["CategorySelection"]}";

        if (_fergunOptions.VoteUrl is not null)
        {
            links.Add(Format.Url(_localizer["Vote"], _fergunOptions.VoteUrl));
        }

        if (_fergunOptions.DonationUrl is not null)
        {
            links.Add(Format.Url(_localizer["Donate"], _fergunOptions.DonationUrl));
        }

        string joinedLinks = string.Join(" | ", links);

        var page = new PageBuilder()
            .WithTitle(_localizer["FergunHelp"])
            .WithDescription(description)
            .WithColor(Color.Orange);

        if (!string.IsNullOrEmpty(joinedLinks))
        {
            page.AddField(_localizer["Links"], joinedLinks);
        }

        var categories = new List<ModuleOption>(6)
        {
            new(new Emoji("🛠️"), _localizer[nameof(UtilityModule)], _localizer[$"{nameof(UtilityModule)}Description"]),
            new(new Emoji("🔍"), _localizer[nameof(ImageModule)], _localizer[$"{nameof(ImageModule)}Description"]),
            new(new Emoji("📄"), _localizer[nameof(OcrModule)], _localizer[$"{nameof(OcrModule)}Description"]),
            new(new Emoji("🔊"), _localizer[nameof(TtsModule)], _localizer[$"{nameof(TtsModule)}Description"]),
            new(new Emoji("📖"), _localizer[nameof(UrbanModule)], _localizer[$"{nameof(UrbanModule)}Description"]),
            new(new Emoji("💡"), _localizer[nameof(OtherModule)], _localizer[$"{nameof(OtherModule)}Description"])
        };

        var modules = _commands.Modules.Where(x => x.Name is not nameof(OwnerModule) and not nameof(BlacklistModule))
            .ToDictionary(x => x.Name, x => x);

        InteractiveMessageResult<ModuleOption?>? result = null;
        var interaction = Context.Interaction;

        _logger.LogInformation("Displaying help menu to user {User} ({Id})", Context.User, Context.User.Id);

        while (result is null || result.Status == InteractiveStatus.Success)
        {
            var selection = new SelectionBuilder<ModuleOption>()
                .AddUser(Context.User)
                .WithOptions(categories)
                .WithEmoteConverter(x => x.Emote)
                .WithStringConverter(x => x.Name)
                .WithInputType(InputType.SelectMenus)
                .WithSelectionPage(page)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .Build();

            result = await _interactive.SendSelectionAsync(selection, interaction, _fergunOptions.SelectionTimeout, responseType);

            if (!result.IsSuccess) break;

            responseType = InteractionResponseType.UpdateMessage;
            interaction = result.StopInteraction!;
            var module = modules[result.Value.Name.Name];
            IEnumerable<string> commandDescriptions;

            string locale = Context.Interaction.UserLocale;
            if (module.IsSlashGroup)
            {
                var group = _cachedComands
                    .First(globalCommand => module.SlashGroupName == globalCommand.Name);

                // Slash command mentions can't be localized
                commandDescriptions = group.Options
                    .OrderBy(x => x.NameLocalized ?? x.Name)
                    .Select(x => $"</{group.Name} {x.Name}:{group.Id}> - {x.DescriptionLocalizations.GetValueOrDefault(locale, x.Description)}");
            }
            else
            {
                commandDescriptions = _cachedComands
                    .Where(globalCommand => module.SlashCommands.Any(slashCommand => globalCommand.Name == slashCommand.Name))
                    .OrderBy(x => x.NameLocalized ?? x.Name)
                    .Select(x => $"</{x.Name}:{x.Id}> - {x.DescriptionLocalizations.GetValueOrDefault(locale, x.Description)}");
            }

            page = new PageBuilder()
                .WithTitle($"{result.Value.Emote} {_localizer[result.Value.Name]}")
                .WithDescription(_localizer[result.Value.Description])
                .AddField(_localizer["Commands"], string.Join('\n', commandDescriptions))
                .WithColor(Color.Orange);

            if (!string.IsNullOrEmpty(joinedLinks))
            {
                page.AddField(_localizer["Links"], joinedLinks);
            }
        }

        return FergunResult.FromSuccess();
    }

    [SlashCommand("ping", "Sends the response time of the bot.")]
    public async Task<RuntimeResult> PingAsync()
    {
        var embed = new EmbedBuilder()
            .WithDescription("Pong!")
            .WithColor(Color.Orange)
            .Build();

        var sw = Stopwatch.StartNew();
        await Context.Interaction.RespondAsync(embed: embed);
        sw.Stop();

        embed = new EmbedBuilder()
            .WithDescription($"Pong! {sw.ElapsedMilliseconds}ms")
            .WithColor(Color.Orange)
            .Build();

        await Context.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed);

        return FergunResult.FromSuccess();
    }

    [SlashCommand("say", "Says something.")]
    public async Task<RuntimeResult> SayAsync([Summary(description: "The text to send.")] string text)
    {
        await Context.Interaction.RespondAsync(text.Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);

        return FergunResult.FromSuccess();
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [MessageCommand("Translate Text")]
    public async Task<RuntimeResult> TranslateAsync(IMessage message)
        => await TranslateAsync(message.GetText(), Context.Interaction.GetLanguageCode());

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("translate", "Translates a text.")]
    public async Task<RuntimeResult> TranslateAsync([Summary(description: "The text to translate.")] string text,
        [Autocomplete(typeof(TranslateAutocompleteHandler))] [Summary(description: "Target language (name, code or alias).")] string target,
        [Autocomplete(typeof(TranslateAutocompleteHandler))] [Summary(description: "Source language (name, code or alias).")] string? source = null,
        [Summary(description: "Whether to respond ephemerally.")] bool ephemeral = false)
        => await _shared.TranslateAsync(Context.Interaction, text, target, source, ephemeral);

    [UserCommand("User Info")]
    [SlashCommand("user", "Gets information about a user.")]
    public async Task<RuntimeResult> UserInfoAsync([Summary(description: "The user.")] IUser user)
    {
        string activities = string.Empty;
        if (user.Activities.Count > 0)
        {
            activities = string.Join('\n', user.Activities.Select(x =>
                x.Type == ActivityType.CustomStatus
                    ? ((CustomStatusGame)x).ToString()
                    : $"{x.Type} {x.Name}"));
        }

        if (string.IsNullOrWhiteSpace(activities))
            activities = $"({_localizer["None"]})";

        string clients = "?";
        if (user.ActiveClients.Count > 0)
        {
            clients = string.Join(' ', user.ActiveClients.Select(x =>
                x switch
                {
                    ClientType.Desktop => "🖥",
                    ClientType.Mobile => "📱",
                    ClientType.Web => "🌐",
                    _ => string.Empty
                }));
        }

        if (string.IsNullOrWhiteSpace(clients))
            clients = "?";

        var guildUser = user as IGuildUser;
        string avatarUrl = guildUser?.GetGuildAvatarUrl(size: 2048) ?? user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["UserInfo"])
            .AddField(_localizer["Name"], user.ToString())
            .AddField(_localizer["Nickname"], guildUser?.Nickname ?? $"({_localizer["None"]})")
            .AddField(_localizer["ID"], user.Id)
            .AddField(_localizer["Activities"], activities, true)
            .AddField(_localizer["ActiveClients"], clients, true)
            .AddField(_localizer["IsBot"], user.IsBot)
            .AddField(_localizer["CreatedAt"], GetTimestamp(user.CreatedAt))
            .AddField(_localizer["ServerJoinDate"], GetTimestamp(guildUser?.JoinedAt))
            .AddField(_localizer["BoostingSince"], GetTimestamp(guildUser?.PremiumSince))
            .WithThumbnailUrl(avatarUrl)
            .WithColor(Color.Orange);

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();

        static string GetTimestamp(DateTimeOffset? dateTime)
            => dateTime == null ? "N/A" : $"{dateTime.Value.ToDiscordTimestamp()} ({dateTime.Value.ToDiscordTimestamp('R')})";
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("wikipedia", "Searches for Wikipedia articles.")]
    public async Task<RuntimeResult> WikipediaAsync([Autocomplete(typeof(WikipediaAutocompleteHandler))] [Summary(name: "query", description: "The search query.")] int id)
    {
        await Context.Interaction.DeferAsync();

        var article = await _wikipediaClient.GetArticleAsync(id, Context.Interaction.GetLanguageCode());

        if (article is null)
        {
            return FergunResult.FromError(_localizer["UnableToGetArticle"]);
        }

        var builder = new EmbedBuilder()
            .WithTitle(article.Title.Truncate(EmbedBuilder.MaxTitleLength))
            .WithUrl($"https://{Context.Interaction.GetLanguageCode()}.wikipedia.org/?curid={article.Id}")
            .WithThumbnailUrl($"https://commons.wikimedia.org/w/index.php?title=Special:Redirect/file/Wikipedia-logo-v2-{Context.Interaction.GetLanguageCode()}.png")
            .WithDescription(article.Extract.Truncate(EmbedBuilder.MaxDescriptionLength))
            .WithFooter(_localizer["WikipediaSearch"])
            .WithColor(Color.Orange);

        if (Context.Channel.IsNsfw() && article.Image is not null)
        {
            if (article.Image.Width >= 500 && article.Image.Height >= 500)
            {
                builder.WithImageUrl(article.Image.Url);
            }
            else
            {
                builder.WithThumbnailUrl(article.Image.Url);
            }
        }

        await Context.Interaction.FollowupAsync(embed: builder.Build());

        return FergunResult.FromSuccess();
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("wolfram", "Asks Wolfram|Alpha about something.")]
    public async Task<RuntimeResult> WolframAlphaAsync([Autocomplete(typeof(WolframAlphaAutocompleteHandler))] [Summary(description: "Something to calculate or know about.")] string input)
    {
        await Context.Interaction.DeferAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var result = await _wolframAlphaClient.SendQueryAsync(input, Context.Interaction.GetLanguageCode(), true, cts.Token);

        if (result.Type == WolframAlphaResultType.Error)
        {
            return FergunResult.FromError(_localizer["WolframAlphaError", result.ErrorInfo!.StatusCode, result.ErrorInfo!.Message]);
        }

        if (result.Type == WolframAlphaResultType.FutureTopic)
        {
            var embed = new EmbedBuilder()
                .WithTitle(result.FutureTopic!.Topic)
                .WithDescription(result.FutureTopic!.Message)
                .WithThumbnailUrl(Constants.WolframAlphaLogoUrl)
                .WithColor(Color.Red)
                .Build();

            await Context.Interaction.FollowupAsync(embed: embed);
            return FergunResult.FromSuccess();
        }

        if (result.Type is  WolframAlphaResultType.NoResult or WolframAlphaResultType.DidYouMean)
        {
            return FergunResult.FromError(_localizer["WolframAlphaNoResults"]);
        }

        var builders = new List<List<EmbedBuilder>>();

        var topEmbed = new EmbedBuilder()
            .WithTitle(_localizer["WolframAlphaResults"])
            .WithDescription(string.Join('\n', result.Warnings.Select(x => $"⚠️ {x.Text}")))
            .WithThumbnailUrl(Constants.WolframAlphaLogoUrl)
            .WithColor(Color.Red);

        foreach (var pod in result.Pods)
        {
            var text = new StringBuilder();
            var subBuilders = new List<EmbedBuilder>();

            foreach (var subPod in pod.SubPods.Take(9))
            {
                // If there's data in plain text and there isn't a newline, use that instead
                if (!string.IsNullOrEmpty(subPod.PlainText) && !subPod.PlainText.Contains('\n'))
                {
                    text.Append(subPod.PlainText);
                    text.Append('\n');
                }
                else
                {
                    var builder = new EmbedBuilder()
                        .WithDescription(subBuilders.Count == 0 ? Format.Bold(pod.Title) : null)
                        .WithImageUrl(subPod.Image.SourceUrl)
                        .WithColor(Color.Red);

                    subBuilders.Add(builder);
                }
            }

            if (text.Length > 0)
                topEmbed.AddField(pod.Title, text.ToString().Truncate(EmbedFieldBuilder.MaxFieldValueLength), true);

            if (subBuilders.Count > 0)
                builders.Add(subBuilders);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(builders.Count == 0 ? 0 : builders.Count - 1)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_fergunOptions)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10),
            InteractionResponseType.DeferredChannelMessageWithSource, cancellationToken: CancellationToken.None);

        return FergunResult.FromSuccess();

        IPageBuilder GeneratePage(int index)
        {
            var builder = new MultiEmbedPageBuilder();
            if (topEmbed.Fields.Count == 0) // No info in plain text
            {
                builders[index][0].WithTitle(_localizer["WolframAlphaResults"])
                    .WithThumbnailUrl(Constants.WolframAlphaLogoUrl);
            }
            else
            {
                builder.AddBuilder(topEmbed);
            }

            if (builders.Count == 0)
            {
                topEmbed.WithFooter(_localizer["WolframAlphaPaginatorFooter", index + 1, 1], Constants.WolframAlphaLogoUrl);

                return builder;
            }

            for (int i = 0; i < builders[index].Count; i++)
            {
                builder.AddBuilder(builders[index][i]);
            }

            builders[index][^1].WithFooter(_localizer["WolframAlphaPaginatorFooter", index + 1, builders.Count], Constants.WolframAlphaLogoUrl);

            return builder;
        }
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("youtube", "Sends a paginator containing YouTube videos.")]
    public async Task<RuntimeResult> YouTubeAsync([Autocomplete(typeof(YouTubeAutocompleteHandler))] [Summary(description: "The search query.")] string query)
    {
        await Context.Interaction.DeferAsync();

        var videos = await _searchClient.GetVideosAsync(query).Take(10);

        switch (videos.Count)
        {
            case 0:
                return FergunResult.FromError(_localizer["NoResults"]);

            case 1:
                await Context.Interaction.FollowupAsync(videos[0].Url);
                break;

            default:
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(Context.User)
                    .WithPageFactory(GeneratePage)
                    .WithActionOnCancellation(ActionOnStop.DisableInput)
                    .WithActionOnTimeout(ActionOnStop.DisableInput)
                    .WithMaxPageIndex(videos.Count - 1)
                    .WithFooter(PaginatorFooter.None)
                    .WithFergunEmotes(_fergunOptions)
                    .WithLocalizedPrompts(_localizer)
                    .Build();

                await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);
                break;
        }

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int index) => new PageBuilder().WithText($"{videos[index].Url}\n{_localizer["PaginatorFooter", index + 1, videos.Count]}");
    }
}