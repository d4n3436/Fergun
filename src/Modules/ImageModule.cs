using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Bing;
using Fergun.Apis.Yandex;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Modules.Handlers;
using GScraper;
using GScraper.Brave;
using GScraper.DuckDuckGo;
using GScraper.Google;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

[Group("img", "Image search commands.")]
public class ImageModule : InteractionModuleBase
{
    private readonly ILogger<ImageModule> _logger;
    private readonly IFergunLocalizer<ImageModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly InteractiveService _interactive;
    private readonly GoogleScraper _googleScraper;
    private readonly DuckDuckGoScraper _duckDuckGoScraper;
    private readonly BraveScraper _braveScraper;
    private readonly IBingVisualSearch _bingVisualSearch;
    private readonly IYandexImageSearch _yandexImageSearch;

    public ImageModule(ILogger<ImageModule> logger, IFergunLocalizer<ImageModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions,
        InteractiveService interactive, GoogleScraper googleScraper, DuckDuckGoScraper duckDuckGoScraper, BraveScraper braveScraper,
        IBingVisualSearch bingVisualSearch, IYandexImageSearch yandexImageSearch)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _interactive = interactive;
        _googleScraper = googleScraper;
        _duckDuckGoScraper = duckDuckGoScraper;
        _braveScraper = braveScraper;
        _bingVisualSearch = bingVisualSearch;
        _yandexImageSearch = yandexImageSearch;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("google", "Searches for images from Google Images and displays them in a paginator.")]
    public async Task<RuntimeResult> GoogleAsync([Autocomplete(typeof(GoogleAutocompleteHandler))][Summary(description: "The query to search.")] string query,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await Context.Interaction.DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation("Query: \"{Query}\", is NSFW: {IsNsfw}", query, isNsfw);

        var images = (await _googleScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict, language: Context.Interaction.GetLanguageCode()))
            .ToArray();

        _logger.LogInformation("Image results: {Count}", images.Length);

        if (images.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)images.Length / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_fergunOptions)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
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
                .WithUrl(multiImages ? "https://google.com" : result.SourceUrl)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["PaginatorFooter", index + 1, maxIndex + 1], Constants.GoogleLogoUrl)
                .WithColor((Color)(result.Color ?? Color.Orange)));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    [SlashCommand("duckduckgo", "Searches for images from DuckDuckGo and displays them in a paginator.")]
    public async Task<RuntimeResult> DuckDuckGoAsync([Autocomplete(typeof(DuckDuckGoAutocompleteHandler))][Summary(description: "The query to search.")] string query,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await Context.Interaction.DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation("Query: \"{Query}\", is NSFW: {IsNsfw}", query, isNsfw);

        var images = (await _duckDuckGoScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict))
            .ToArray();

        _logger.LogInformation("Image results: {Count}", images.Length);

        if (images.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)images.Length / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_fergunOptions)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
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
                .WithUrl(multiImages ? "https://duckduckgo.com" : result.SourceUrl)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["PaginatorFooter", index + 1, maxIndex + 1], Constants.DuckDuckGoLogoUrl)
                .WithColor(Color.Orange));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    [SlashCommand("brave", "Searches for images from Brave and displays them in a paginator.")]
    public async Task<RuntimeResult> BraveAsync([Autocomplete(typeof(BraveAutocompleteHandler))][Summary(description: "The query to search.")] string query,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await Context.Interaction.DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation("Query: \"{Query}\", is NSFW: {IsNsfw}", query, isNsfw);

        var images = (await _braveScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict))
            .ToArray();

        _logger.LogInformation("Image results: {Count}", images.Length);

        if (images.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)images.Length / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_fergunOptions)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
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
                .WithDescription(_localizer["BraveImageSearch"])
                .WithUrl(multiImages ? "https://search.brave.com" : result.SourceUrl)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["PaginatorFooter", index + 1, maxIndex + 1], Constants.BraveLogoUrl)
                .WithColor(Color.Orange));

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
            .WithColor(Color.Orange);

        var selection = new SelectionBuilder<ReverseImageSearchEngine>()
            .AddUser(Context.User)
            .WithOptions(Enum.GetValues<ReverseImageSearchEngine>())
            .WithSelectionPage(page)
            .Build();

        var result = await _interactive.SendSelectionAsync(selection, Context.Interaction, TimeSpan.FromMinutes(1), ephemeral: true);

        // Attempt to disable the components
        _ = Context.Interaction.ModifyOriginalResponseAsync(x => x.Components = selection.GetOrAddComponents(true).Build());

        if (result.IsSuccess)
        {
            return await ReverseAsync(url, result.Value, false, result.StopInteraction!, Context.Interaction, true);
        }

        return FergunResult.FromSilentError();
    }

    [SlashCommand("reverse", "Reverse image search.")]
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
        return engine switch
        {
            ReverseImageSearchEngine.Yandex => await YandexAsync(url, multiImages, interaction, originalInteraction, ephemeral),
            ReverseImageSearchEngine.Bing => await BingAsync(url, multiImages, interaction, originalInteraction, ephemeral),
            _ => throw new ArgumentException(_localizer["InvalidImageSearchEngine"], nameof(engine))
        };
    }

    public virtual async Task<RuntimeResult> YandexAsync(string url, bool multiImages, IDiscordInteraction interaction,
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
            _logger.LogWarning(e, "Failed to delete the original interaction response.");
        }

        bool isNsfw = Context.Channel.IsNsfw();

        IYandexReverseImageSearchResult[] results;

        try
        {
            results = (await _yandexImageSearch.ReverseImageSearchAsync(url, isNsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family))
                .ToArray();
        }
        catch (YandexException e)
        {
            _logger.LogWarning(e, "Failed to perform reverse image search to url {Url}", url);
            return FergunResult.FromError(e.Message, ephemeral, interaction);
        }

        if (results.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"], ephemeral, interaction);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)results.Length / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_fergunOptions)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
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
                .WithUrl(multiImages ? "https://yandex.com/images" : result.SourceUrl)
                .WithThumbnailUrl(url)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["YandexVisualSearchPaginatorFooter", index + 1, maxIndex + 1], Constants.YandexIconUrl)
                .WithColor(Color.Orange));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    public virtual async Task<RuntimeResult> BingAsync(string url, bool multiImages, IDiscordInteraction interaction,
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
            _logger.LogWarning(e, "Failed to delete the original interaction response.");
        }

        bool isNsfw = Context.Channel.IsNsfw();
        IBingReverseImageSearchResult[] results;

        try
        {
            results = (await _bingVisualSearch.ReverseImageSearchAsync(url, isNsfw ? BingSafeSearchLevel.Off : BingSafeSearchLevel.Strict, interaction.GetLanguageCode()))
                .ToArray();
        }
        catch (BingException e)
        {
            _logger.LogWarning(e, "Failed to perform reverse image search to url {Url}", url);
            return FergunResult.FromError(e.ImageCategory is null ? e.Message : _localizer[$"Bing{e.ImageCategory}"], ephemeral, interaction);
        }

        if (results.Length == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"], ephemeral, interaction);
        }

        int count = multiImages ? 4 : 1;
        int maxIndex = (int)Math.Ceiling((double)results.Length / count) - 1;

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_fergunOptions)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
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
                .WithUrl(multiImages ? "https://www.bing.com/visualsearch" : result.SourceUrl)
                .WithThumbnailUrl(url)
                .WithDescription(result.FriendlyDomainName ?? (Uri.TryCreate(result.SourceUrl, UriKind.Absolute, out var uri) ? uri.Host : null))
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["BingVisualSearchPaginatorFooter", index + 1, maxIndex + 1], Constants.BingIconUrl)
                .WithColor((Color)result.AccentColor));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }
}