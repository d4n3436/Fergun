using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Bing;
using Fergun.Apis.Google;
using Fergun.Apis.Yandex;
using Fergun.Common;
using Fergun.Configuration;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Localization;
using Fergun.Modules.Handlers;
using Fergun.Preconditions;
using Fergun.Services;
using GScraper;
using GScraper.DuckDuckGo;
using GScraper.Google;
using Humanizer;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[Ratelimit(2, Constants.GlobalRatelimitPeriod)]
[Group("img", "Image search commands.")]
public class ImageModule : InteractionModuleBase
{
    private readonly ILogger<ImageModule> _logger;
    private readonly IFergunLocalizer<ImageModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly FergunEmoteProvider _emotes;
    private readonly InteractiveService _interactive;
    private readonly GoogleScraper _googleScraper;
    private readonly DuckDuckGoScraper _duckDuckGoScraper;
    private readonly IBingVisualSearch _bingVisualSearch;
    private readonly IYandexImageSearch _yandexImageSearch;
    private readonly IGoogleLensClient _googleLens;

    public ImageModule(ILogger<ImageModule> logger, IFergunLocalizer<ImageModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions, FergunEmoteProvider emotes, InteractiveService interactive,
        GoogleScraper googleScraper, DuckDuckGoScraper duckDuckGoScraper, IBingVisualSearch bingVisualSearch, IYandexImageSearch yandexImageSearch, IGoogleLensClient googleLens)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _emotes = emotes;
        _interactive = interactive;
        _googleScraper = googleScraper;
        _duckDuckGoScraper = duckDuckGoScraper;
        _bingVisualSearch = bingVisualSearch;
        _yandexImageSearch = yandexImageSearch;
        _googleLens = googleLens;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("google", "Searches for images from Google Images and displays them in a paginator.")]
    public async Task<RuntimeResult> GoogleAsync([Autocomplete<GoogleAutocompleteHandler>][Summary(description: "The query to search.")] string query,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await Context.Interaction.DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation("Sending Google Images request (query: \"{Query}\", is NSFW: {IsNsfw})", query, isNsfw);

        var images = (await _googleScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict, language: Context.Interaction.GetLanguageCode()))
            .ToArray();

        _logger.LogDebug("Google Image result count: {Count}", images.Length);

        if (images.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)images.Length / count) - 1;
        _logger.LogDebug("Sending Google Images paginator with {Count} pages (multi-images: {MultiImages})", maxIndex + 1, multiImages);

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_emotes)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(maxIndex)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            int start = index * count;

            var builders = images.Take(start..(start + count)).Select(result => new EmbedBuilder()
                .WithTitle(result.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithDescription(_localizer["GoogleImagesSearch"])
                .WithUrl(multiImages ? Constants.GoogleUrl : result.SourceUrl)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["PaginatorFooter", index + 1, maxIndex + 1], Constants.GoogleLogoUrl)
                .WithColor((Color)(result.Color ?? Constants.DefaultColor)));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    [SlashCommand("duckduckgo", "Searches for images from DuckDuckGo and displays them in a paginator.")]
    public async Task<RuntimeResult> DuckDuckGoAsync(
        [Autocomplete<DuckDuckGoAutocompleteHandler>][MaxLength(DuckDuckGoScraper.MaxQueryLength)][Summary(description: "The query to search.")] string query,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await Context.Interaction.DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation("Sending DuckDuckGo image request (query: \"{Query}\", is NSFW: {IsNsfw})", query, isNsfw);

        var images = (await _duckDuckGoScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict))
            .ToArray();

        _logger.LogDebug("DuckDuckGo image result count: {Count}", images.Length);

        if (images.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)images.Length / count) - 1;
        _logger.LogDebug("Sending DuckDuckGo image paginator with {Count} pages (multi-images: {MultiImages})", maxIndex + 1, multiImages);

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_emotes)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(maxIndex)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            int start = index * count;

            var builders = images.Take(start..(start + count)).Select(result => new EmbedBuilder()
                .WithTitle(result.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithDescription(_localizer["DuckDuckGoImageSearch"])
                .WithUrl(multiImages ? Constants.DuckDuckGoUrl : result.SourceUrl)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["PaginatorFooter", index + 1, maxIndex + 1], Constants.DuckDuckGoLogoUrl)
                .WithColor(Constants.DefaultColor));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    [MessageCommand("Reverse Image Search")]
    public async Task<RuntimeResult> ReverseAsync(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault();
        var embed = message.Embeds.FirstOrDefault(x => x.Image is not null || x.Thumbnail is not null);

        string? url = attachment?.Url ?? embed?.Image?.Url ?? embed?.Thumbnail?.Url;

        if (url is null)
        {
            return FergunResult.FromError(_localizer["NoImageUrlInMessage"], true);
        }

        var page = new PageBuilder()
            .WithTitle(_localizer["SelectImageSearchEngine"])
            .WithColor(Constants.DefaultColor);

        var selection = new SelectionBuilder<ReverseImageSearchEngine>()
            .AddUser(Context.User)
            .WithOptions(Enum.GetValues<ReverseImageSearchEngine>().Where(x => x != ReverseImageSearchEngine.Google).ToArray()) // TODO: Remove when Google Lens reverse image search is fixed
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithSelectionPage(page)
            .WithLocalizedPrompts(_localizer)
            .Build();

        var result = await _interactive.SendSelectionAsync(selection, Context.Interaction, TimeSpan.FromMinutes(1), ephemeral: true);

        if (result.IsSuccess)
        {
            return await ReverseAsync(url, result.Value, false, result.StopInteraction!, Context.Interaction, true);
        }

        return FergunResult.FromSilentError();
    }

    [SlashCommand("reverse", "Performs a reverse image search and displays the results in a paginator.")]
    public async Task<RuntimeResult> ReverseAsync([Summary(description: "The URL of an image.")] string? url = null,
        [Summary(description: "An image file.")] IAttachment? file = null,
        [Summary(description: $"The search engine. The default is {nameof(ReverseImageSearchEngine.Yandex)}.")] ReverseImageSearchEngine engine = ReverseImageSearchEngine.Yandex,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        url = file?.Url ?? url;

        if (url is null)
        {
            return FergunResult.FromError(_localizer["UrlOrAttachmentRequired"], true);
        }

        return await ReverseAsync(url, engine, multiImages, Context.Interaction);
    }

    public async Task<RuntimeResult> ReverseAsync(string url, ReverseImageSearchEngine engine, bool multiImages,
        IDiscordInteraction interaction, IDiscordInteraction? originalInteraction = null, bool ephemeral = false)
    {
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return FergunResult.FromError(_localizer["UrlNotWellFormed"], true, interaction);
        }

        _logger.LogDebug("Performing reverse image search (engine: {Engine}, multi-images: {MultiImages}, ephemeral: {Ephemeral})", engine, multiImages, ephemeral);

        return engine switch
        {
            ReverseImageSearchEngine.Yandex => await ReverseYandexAsync(url, multiImages, interaction, originalInteraction, ephemeral),
            ReverseImageSearchEngine.Bing => await ReverseBingAsync(url, multiImages, interaction, originalInteraction, ephemeral),
            ReverseImageSearchEngine.Google => await ReverseGoogleAsync(url, multiImages, interaction, originalInteraction, ephemeral),
            _ => throw new ArgumentException(_localizer["InvalidImageSearchEngine"], nameof(engine))
        };
    }

    public virtual async Task<RuntimeResult> ReverseYandexAsync(string url, bool multiImages, IDiscordInteraction interaction,
        IDiscordInteraction? originalInteraction = null, bool ephemeral = false)
    {
        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        try
        {
            if (originalInteraction is not null)
                await originalInteraction.DeleteOriginalResponseAsync();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to delete the original interaction response");
        }

        bool isNsfw = Context.Channel.IsNsfw();

        IReadOnlyList<IYandexReverseImageSearchResult> results;

        _logger.LogInformation("Sending Yandex reverse image search request (URL: {Url}, is NSFW: {IsNsfw})", url, isNsfw);
        try
        {
            results = await _yandexImageSearch.ReverseImageSearchAsync(url, isNsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family);
        }
        catch (YandexException e)
        {
            _logger.LogWarning(e, "Failed to perform reverse image search to url {Url}", url);
            return FergunResult.FromError(e.Message, ephemeral, interaction);
        }

        _logger.LogDebug("Yandex reverse image search result count: {Count}", results.Count);

        if (results.Count == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"], ephemeral, interaction);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)results.Count / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_emotes)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(maxIndex)
            .WithFooter(PaginatorFooter.None)
            .AddUser(interaction.User)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource, ephemeral);

        return FergunResult.FromSuccess();

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            int start = index * count;

            var builders = results.Take(start..(start + count)).Select(result => new EmbedBuilder()
                .WithTitle(result.Title?.Truncate(EmbedBuilder.MaxTitleLength) ?? string.Empty)
                .WithDescription(result.Text)
                .WithUrl(multiImages ? Constants.YandexImageSearchUrl : result.SourceUrl)
                .WithThumbnailUrl(url)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["YandexVisualSearchPaginatorFooter", index + 1, maxIndex + 1], Constants.YandexIconUrl)
                .WithColor(Constants.DefaultColor));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    public virtual async Task<RuntimeResult> ReverseBingAsync(string url, bool multiImages, IDiscordInteraction interaction,
        IDiscordInteraction? originalInteraction = null, bool ephemeral = false)
    {
        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        try
        {
            if (originalInteraction is not null)
                await originalInteraction.DeleteOriginalResponseAsync();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to delete the original interaction response");
        }

        bool isNsfw = Context.Channel.IsNsfw();
        IReadOnlyList<IBingReverseImageSearchResult> results;

        _logger.LogInformation("Sending Bing reverse image search request (URL: {Url}, is NSFW: {IsNsfw}, language: {Language})", url, isNsfw, interaction.GetLanguageCode());
        try
        {
            results = await _bingVisualSearch.ReverseImageSearchAsync(url, isNsfw ? BingSafeSearchLevel.Off : BingSafeSearchLevel.Strict, interaction.GetLanguageCode());
        }
        catch (BingException e)
        {
            _logger.LogWarning(e, "Failed to perform reverse image search to url {Url}", url);
            return FergunResult.FromError(e.ImageCategory is null ? e.Message : _localizer[$"Bing{e.ImageCategory}"], ephemeral, interaction);
        }

        _logger.LogDebug("Bing reverse image search result count: {Count}", results.Count);

        if (results.Count == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"], ephemeral, interaction);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)results.Count / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_emotes)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(maxIndex)
            .WithFooter(PaginatorFooter.None)
            .AddUser(interaction.User)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource, ephemeral);

        return FergunResult.FromSuccess();

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            int start = index * count;

            var builders = results.Take(start..(start + count)).Select(result => new EmbedBuilder()
                .WithTitle(result.Text.Truncate(EmbedBuilder.MaxTitleLength))
                .WithUrl(multiImages ? Constants.BingVisualSearchUrl : result.SourceUrl)
                .WithThumbnailUrl(url)
                .WithDescription(result.FriendlyDomainName ?? (Uri.TryCreate(result.SourceUrl, UriKind.Absolute, out var uri) ? uri.Host : null))
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["BingVisualSearchPaginatorFooter", index + 1, maxIndex + 1], Constants.BingIconUrl)
                .WithColor((Color)result.AccentColor));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    public virtual async Task<RuntimeResult> ReverseGoogleAsync(string url, bool multiImages, IDiscordInteraction interaction,
        IDiscordInteraction? originalInteraction = null, bool ephemeral = false)
    {
        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        try
        {
            if (originalInteraction is not null)
                await originalInteraction.DeleteOriginalResponseAsync();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to delete the original interaction response");
        }

        IReadOnlyList<IGoogleLensResult> results;

        _logger.LogInformation("Sending Google reverse image search request (URL: {Url}, language: {Language})", url, interaction.GetLanguageCode());
        try
        {
            results = await _googleLens.ReverseImageSearchAsync(url, interaction.GetLanguageCode());
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to perform reverse image search to url {Url}", url);
            return FergunResult.FromError(_localizer["GoogleLensError"], ephemeral, interaction);
        }

        _logger.LogDebug("Google reverse image search result count: {Count}", results.Count);

        if (results.Count == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"], ephemeral, interaction);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)results.Count / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_emotes)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(maxIndex)
            .WithFooter(PaginatorFooter.None)
            .AddUser(interaction.User)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, interaction, _fergunOptions.PaginatorTimeout, InteractionResponseType.DeferredChannelMessageWithSource, ephemeral);

        return FergunResult.FromSuccess();

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            int start = index * count;

            var builders = results.Take(start..(start + count)).Select(result => new EmbedBuilder()
                .WithTitle(result.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithUrl(multiImages ? Constants.GoogleLensUrl : result.SourcePageUrl)
                .WithThumbnailUrl(url)
                .WithAuthor(result.SourceDomainName, result.SourceIconUrl)
                .WithImageUrl(result.ThumbnailUrl)
                .WithFooter(_localizer["GoogleLensPaginatorFooter", index + 1, maxIndex + 1], Constants.GoogleLensLogoUrl)
                .WithColor(Constants.DefaultColor));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }
}