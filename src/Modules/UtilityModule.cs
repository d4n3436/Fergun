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
using Fergun.Apis.Dictionary;
using Fergun.Apis.Wikipedia;
using Fergun.Apis.WolframAlpha;
using Fergun.Configuration;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Modules.Handlers;
using Fergun.Preconditions;
using Fergun.Services;
using GTranslate;
using GTranslate.Results;
using Humanizer;
using JetBrains.Annotations;
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

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod)]
public class UtilityModule : InteractionModuleBase
{
    private const string DictionaryCategoryKey = "dictionary-category";
    private const string DictionaryExtraInformationKey = "dictionary-extra-information";

    private static readonly DrawingOptions _cachedDrawingOptions = new();
    private static readonly PngEncoder _cachedPngEncoder = new() { CompressionLevel = PngCompressionLevel.BestCompression, SkipMetadata = true };

    private static readonly Lazy<Language[]> _lazyFilteredLanguages = new(() => Language.LanguageDictionary
        .Values
        .Where(x => x.SupportedServices == (TranslationServices.Google | TranslationServices.Bing | TranslationServices.Yandex | TranslationServices.Microsoft))
        .ToArray());

    private readonly ILogger<UtilityModule> _logger;
    private readonly IFergunLocalizer<UtilityModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly FergunEmoteProvider _emotes;
    private readonly SharedModule _shared;
    private readonly InteractionService _commands;
    private readonly InteractiveService _interactive;
    private readonly ApplicationCommandCache _commandCache;
    private readonly IFergunTranslator _translator;
    private readonly IDictionaryClient _dictionary;
    private readonly SearchClient _searchClient;
    private readonly IWikipediaClient _wikipediaClient;
    private readonly IWolframAlphaClient _wolframAlphaClient;

    public UtilityModule(ILogger<UtilityModule> logger, IFergunLocalizer<UtilityModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions, FergunEmoteProvider emotes,
        SharedModule shared, InteractionService commands, InteractiveService interactive, ApplicationCommandCache commandCache, IDictionaryClient dictionary,
        IFergunTranslator translator, SearchClient searchClient, IWikipediaClient wikipediaClient, IWolframAlphaClient wolframAlphaClient)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _emotes = emotes;
        _shared = shared;
        _commands = commands;
        _interactive = interactive;
        _commandCache = commandCache;
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

            case AvatarType.Default:
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

        _logger.LogInformation("Displaying avatar of user {User} ({Id}), type: {Type}", user, user.Id, type);

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
        [Summary(description: "The amount of times to translate the text (2-10).")][MinValue(2)][MaxValue(10)] int chainCount = 8)
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

        _logger.LogInformation("Performing bad translation (chain count: {Count})", chainCount);

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
                _logger.LogDebug("Translating to: {Target}", target.ISO6391);
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
            .WithDescription(embedText + text.Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length))
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
            _logger.LogInformation("Sending image of generated color: {Color}", DisplayColor());
        }
        else
        {
            _logger.LogInformation("Sending image of color: {Color}", DisplayColor());
        }

        using var image = new Image<Rgba32>(500, 500);

        image.Mutate(x => x.Fill(_cachedDrawingOptions, SixLabors.ImageSharp.Color.FromRgb(color.R, color.G, color.B)));
        await using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream, _cachedPngEncoder);
        stream.Seek(0, SeekOrigin.Begin);

        string hex = $"{color.R:X2}{color.G:X2}{color.B:X2}";

        var builder = new EmbedBuilder()
            .WithTitle(DisplayColor())
            .WithImageUrl($"attachment://{hex}.png")
            .WithFooter($"R: {color.R}, G: {color.G}, B: {color.B}")
            .WithColor((Color)color);

        await Context.Interaction.RespondWithFileAsync(new FileAttachment(stream, $"{hex}.png"), embed: builder.Build());

        return FergunResult.FromSuccess();

        string DisplayColor() => $"#{color.R:X2}{color.G:X2}{color.B:X2}{(color.IsNamedColor ? $" ({color.Name})" : string.Empty)}";
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("define", "Gets the definitions of a word from Dictionary.com (only English).")]
    public async Task<RuntimeResult> DefineAsync(
    [MaxLength(100)] [Autocomplete(typeof(DictionaryAutocompleteHandler))]
    [Summary(description: "The word to get its definitions.")] string word)
    {
        await Context.Interaction.DeferAsync();

        _logger.LogInformation("Requesting definitions for word \"{Word}\"", word);
        var result = await _dictionary.GetDefinitionsAsync(word);

        var group = result.Data?.Content
            .FirstOrDefault(content => content.Source is "luna" or "collins");

        if (group is null)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        var entries = group.Entries;
        _logger.LogDebug("Received dictionary response (source(s): {Sources}, {Source} entry count: {Count})", string.Join(", ", result.Data!.Content.Select(x => x.Source)), group.Source, entries.Count);

        var state = new DictionaryPaginatorState();

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(entries.Count)
            .WithUserState(state)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(20), InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        IPage GeneratePage(IComponentPaginator p)
        {
            if (p.CurrentPageIndex != state.LastPageIndex)
            {
                // Reset the current category index to avoid out of range exceptions
                state.LastPageIndex = p.CurrentPageIndex;
                state.CurrentCategoryIndex = 0;
            }

            var entry = entries[p.CurrentPageIndex];
            var block = entry.PartOfSpeechBlocks[state.CurrentCategoryIndex];
            var options = entry
                .PartOfSpeechBlocks
                .Select((x, i) =>
                    new SelectMenuOptionBuilder((x.PartOfSpeech ?? (x.SupplementaryInfo.Length == 0 ? entry.Entry : x.SupplementaryInfo)).Truncate(SelectMenuOptionBuilder.MaxSelectLabelLength),
                        i.ToString(), isDefault: i == state.CurrentCategoryIndex))
                .ToList();

            string extraInfo = DictionaryFormatter.FormatExtraInformation(entry);
            string footer = $"-# {_emotes.DictionaryComIconEmote} Dictionary.com results | Definition {p.CurrentPageIndex + 1} of {entries.Count}";

            var container = new ContainerBuilder();

            if (state.IsDisplayingExtraInfo)
            {
                container.WithTextDisplay(extraInfo.Truncate(4000 - footer.Length))
                    .WithSeparator();
            }
            else
            {
                string title = DictionaryFormatter.FormatEntry(entry);
                string formattedBlock = DictionaryFormatter.FormatPartOfSpeechBlock(block, entry, 4000 - title.Length - 1 - footer.Length);

                container.WithTextDisplay($"{title}\n{formattedBlock}")
                    .WithSeparator()
                    .WithActionRow(new ActionRowBuilder()
                        .WithSelectMenu(DictionaryCategoryKey, options, disabled: entry.PartOfSpeechBlocks.Count == 1 || p.ShouldDisable())); // Navigation of inner pages (categories)

                // Navigation of outer set of pages (definitions)
                if (entries.Count > 1)
                {
                    container.WithActionRow(new ActionRowBuilder()
                        .AddPreviousButton(p, "Previous definition", ButtonStyle.Secondary, _emotes.SkipToStartEmote)
                        .AddNextButton(p, "Next definition", ButtonStyle.Secondary, _emotes.SkipToEndEmote));
                }
            }

            container.WithActionRow(new ActionRowBuilder()
                    .WithButton(state.IsDisplayingExtraInfo ? "Go back" : "More information", DictionaryExtraInformationKey,
                        ButtonStyle.Secondary, _emotes.InfoEmote, null, string.IsNullOrEmpty(extraInfo) || p.ShouldDisable())
                    .AddStopButton(p, emote: _emotes.ExitEmote))
                .WithSeparator()
                .WithTextDisplay(footer)
                .WithAccentColor(new Color(0x0049D7));

            var components = new ComponentBuilderV2()
                .WithContainer(container)
                .Build();

            return new PageBuilder()
                .WithComponents(components)
                .Build();
        }
    }

    [ComponentInteraction(DictionaryCategoryKey)]
    public async Task<RuntimeResult> SetDictionaryCategoryAsync(int index)
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!_interactive.TryGetComponentPaginator(interaction.Message, out var paginator) || !paginator.CanInteract(Context.User))
        {
            await Context.Interaction.DeferAsync(true);
            return FergunResult.FromSuccess();
        }

        var state = paginator.GetUserState<DictionaryPaginatorState>();

        state.CurrentCategoryIndex = index;
        await paginator.RenderPageAsync(interaction);

        return FergunResult.FromSuccess();
    }

    [ComponentInteraction(DictionaryExtraInformationKey)]
    public async Task<RuntimeResult> InvertDictionaryExtraInfoAsync()
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!_interactive.TryGetComponentPaginator(interaction.Message, out var paginator) || !paginator.CanInteract(Context.User))
        {
            await Context.Interaction.DeferAsync(true);
            return FergunResult.FromSuccess();
        }

        var state = paginator.GetUserState<DictionaryPaginatorState>();

        state.IsDisplayingExtraInfo = !state.IsDisplayingExtraInfo;
        await paginator.RenderPageAsync(interaction);

        return FergunResult.FromSuccess();
    }

    [SlashCommand("help", "Information about Fergun.")]
    public async Task<RuntimeResult> HelpAsync()
    {
        string description = _localizer["Fergun2Info"];

        var links = new List<string>();

        if (_fergunOptions.SupportServerUrl is not null)
        {
            _logger.LogDebug("Adding support server link to embed");
            links.Add(Format.Url(_localizer["Support"], _fergunOptions.SupportServerUrl));
            description += $"\n\n{_localizer["Fergun2SupportInfo"]}";
        }

        description += $"\n\n{_localizer["CategorySelection"]}";

        if (_fergunOptions.VoteUrl is not null)
        {
            _logger.LogDebug("Adding vote link to embed");
            links.Add(Format.Url(_localizer["Vote"], _fergunOptions.VoteUrl));
        }

        if (_fergunOptions.DonationUrl is not null)
        {
            _logger.LogDebug("Adding donation link to embed");
            links.Add(Format.Url(_localizer["Donate"], _fergunOptions.DonationUrl));
        }

        var page = new PageBuilder()
            .WithTitle(_localizer["FergunHelp"])
            .WithDescription(description)
            .WithColor(Color.Orange);

        string joinedLinks = string.Join(" | ", links);

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

        _logger.LogInformation("Displaying help menu to user {User} ({Id})", Context.User, Context.User.Id);

        var menu = new MenuSelectionBuilder<ModuleOption>()
            .AddUser(Context.User)
            .WithOptions(categories)
            .WithEmoteConverter(x => x.Emote)
            .WithStringConverter(x => x.Name)
            .WithPlaceholder(_localizer["HelpMenuPlaceholder"])
            .WithInputHandler(SetModule)
            .WithSetDefaultValues(true)
            .WithInputType(InputType.SelectMenus)
            .WithSelectionPage(page)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendSelectionAsync(menu, Context.Interaction, _fergunOptions.SelectionTimeout);

        return FergunResult.FromSuccess();

        IPage SetModule(ModuleOption option)
        {
            var module = modules[option.Name.Name];
            IEnumerable<string> commandDescriptions;

            string locale = Context.Interaction.UserLocale;
            if (module.IsSlashGroup)
            {
                var group = _commandCache.CachedCommands
                    .First(globalCommand => module.SlashGroupName == globalCommand.Name);

                // Slash command mentions can't be localized
                commandDescriptions = group.Options
                    .OrderBy(x => x.NameLocalized ?? x.Name)
                    .Select(x => $"</{group.Name} {x.Name}:{group.Id}> - {x.DescriptionLocalizations.GetValueOrDefault(locale, x.Description)}");
            }
            else
            {
                commandDescriptions = _commandCache.CachedCommands
                    .Where(globalCommand => module.SlashCommands.Any(slashCommand => globalCommand.Name == slashCommand.Name))
                    .OrderBy(x => x.NameLocalized ?? x.Name)
                    .Select(x => $"</{x.Name}:{x.Id}> - {x.DescriptionLocalizations.GetValueOrDefault(locale, x.Description)}");
            }

            var builder = new PageBuilder()
                .WithTitle($"{option.Emote} {_localizer[option.Name]}")
                .WithDescription(_localizer[option.Description])
                .AddField(_localizer["Commands"], string.Join('\n', commandDescriptions))
                .WithColor(Color.Orange);

            if (!string.IsNullOrEmpty(joinedLinks))
            {
                page.AddField(_localizer["Links"], joinedLinks);
            }

            return builder.Build();
        }
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

        _logger.LogDebug("Ping interaction took {Elapsed}ms to process", sw.ElapsedMilliseconds);

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
        [Autocomplete(typeof(TranslateAutocompleteHandler))][Summary(description: "Target language (name, code or alias).")] string target,
        [Autocomplete(typeof(TranslateAutocompleteHandler))][Summary(description: "Source language (name, code or alias).")] string? source = null,
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
                    ? x.ToString()
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
            .AddField(_localizer["IsBot"], _localizer[user.IsBot ? "Yes" : "No"])
            .AddField(_localizer["CreatedAt"], GetTimestamp(user.CreatedAt))
            .AddField(_localizer["ServerJoinDate"], GetTimestamp(guildUser?.JoinedAt))
            .AddField(_localizer["BoostingSince"], GetTimestamp(guildUser?.PremiumSince))
            .WithThumbnailUrl(avatarUrl)
            .WithColor(Color.Orange);

        _logger.LogInformation("Displaying user info of {User} ({Id})", user, user.Id);

        await Context.Interaction.RespondAsync(embed: builder.Build());

        return FergunResult.FromSuccess();

        static string GetTimestamp(DateTimeOffset? dateTime)
            => dateTime == null ? "N/A" : $"{TimestampTag.FromDateTimeOffset(dateTime.Value)} ({TimestampTag.FromDateTimeOffset(dateTime.Value).ToString(TimestampTagStyles.Relative)})";
    }

    [Ratelimit(2, Constants.GlobalRatelimitPeriod)]
    [SlashCommand("wikipedia", "Searches for Wikipedia articles.")]
    public async Task<RuntimeResult> WikipediaAsync([MaxValue(int.MaxValue)][Autocomplete(typeof(WikipediaAutocompleteHandler))][Summary(name: "query", description: "The search query.")] int id)
    {
        await Context.Interaction.DeferAsync();

        _logger.LogInformation("Requesting Wikipedia article (ID: {Id}, Language: {Language})", id, Context.Interaction.GetLanguageCode());
        var article = await _wikipediaClient.GetArticleAsync(id, Context.Interaction.GetLanguageCode());

        if (article is null)
        {
            _logger.LogDebug("Wikipedia article with ID {Id} not found", id);
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
    public async Task<RuntimeResult> WolframAlphaAsync([Autocomplete(typeof(WolframAlphaAutocompleteHandler))][Summary(description: "Something to calculate or know about.")] string input)
    {
        await Context.Interaction.DeferAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        _logger.LogInformation("Sending Wolfram Alpha request (query: \"{Query}\", language: {Language})", input, Context.Interaction.GetLanguageCode());
        var result = await _wolframAlphaClient.SendQueryAsync(input, Context.Interaction.GetLanguageCode(), true, cts.Token);

        _logger.LogInformation("Received Wolfram Alpha result (type: {Type}, pod count: {Count})", result.Type, result.Pods.Count);

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

        if (result.Type is WolframAlphaResultType.NoResult or WolframAlphaResultType.DidYouMean)
        {
            return FergunResult.FromError(_localizer["WolframAlphaNoResults"]);
        }

        if (result.Warnings.Count > 0)
        {
            _logger.LogDebug("Wolfram Alpha result warnings: {Warnings}", string.Join(", ", result.Warnings.Select(x => x.Text)));
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
                if (!string.IsNullOrWhiteSpace(subPod.PlainText) && !subPod.PlainText.Contains('\n'))
                {
                    text.Append(subPod.PlainText)
                        .Append('\n');
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
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(builders.Count == 0 ? 0 : builders.Count - 1)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_emotes)
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
    public async Task<RuntimeResult> YouTubeAsync([Autocomplete(typeof(YouTubeAutocompleteHandler))][Summary(description: "The search query.")] string query)
    {
        await Context.Interaction.DeferAsync();

        _logger.LogInformation("Requesting YouTube videos (query: \"{Query}\")", query);
        var videos = await _searchClient.GetVideosAsync(query).Take(10);

        _logger.LogDebug("YouTube search result count: {Count}", videos.Count);

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
                    .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
                    .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
                    .WithMaxPageIndex(videos.Count - 1)
                    .WithFooter(PaginatorFooter.None)
                    .WithFergunEmotes(_emotes)
                    .WithLocalizedPrompts(_localizer)
                    .Build();

                await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);
                break;
        }

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int index) => new PageBuilder().WithText($"{videos[index].Url}\n{_localizer["PaginatorFooter", index + 1, videos.Count]}");
    }
}