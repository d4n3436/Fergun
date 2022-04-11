using System.Globalization;
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
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[Group("img", "Image search commands.")]
public class ImageModule : InteractionModuleBase
{
    private readonly ILogger<ImageModule> _logger;
    private readonly IFergunLocalizer<ImageModule> _localizer;
    private readonly InteractiveService _interactive;
    private readonly GoogleScraper _googleScraper;
    private readonly DuckDuckGoScraper _duckDuckGoScraper;
    private readonly BraveScraper _braveScraper;
    private readonly IBingVisualSearch _bingVisualSearch;
    private readonly IYandexImageSearch _yandexImageSearch;

    public ImageModule(ILogger<ImageModule> logger, IFergunLocalizer<ImageModule> localizer, InteractiveService interactive, GoogleScraper googleScraper,
        DuckDuckGoScraper duckDuckGoScraper, BraveScraper braveScraper, IBingVisualSearch bingVisualSearch, IYandexImageSearch yandexImageSearch)
    {
        _logger = logger;
        _localizer = localizer;
        _interactive = interactive;
        _googleScraper = googleScraper;
        _duckDuckGoScraper = duckDuckGoScraper;
        _braveScraper = braveScraper;
        _bingVisualSearch = bingVisualSearch;
        _yandexImageSearch = yandexImageSearch;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("google", "Searches for images from Google Images and displays them in a paginator.")]
    public async Task Google([Autocomplete(typeof(GoogleAutocompleteHandler))][Summary(description: "The query to search.")] string query,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation(new EventId(0, "img"), "Query: \"{query}\", is NSFW: {isNsfw}", query, isNsfw);

        var images = await _googleScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict, language: Context.Interaction.GetLanguageCode());

        var filteredImages = images
            .Where(x => x.Url.StartsWith("http") && x.SourceUrl.StartsWith("http"))
            .Chunk(multiImages ? 4 : 1)
            .ToArray();

        _logger.LogInformation(new EventId(0, "img"), "Image results: {count}", filteredImages.Length);

        if (filteredImages.Length == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."]);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes()
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(filteredImages.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            var builders = filteredImages[index].Select(result => new EmbedBuilder()
                .WithTitle(result.Title)
                .WithDescription(_localizer["Google Images search"])
                .WithUrl(multiImages ? "https://google.com" : result.SourceUrl)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["Page {0} of {1}", index + 1, filteredImages.Length], Constants.GoogleLogoUrl)
                .WithColor(Color.Orange));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    [SlashCommand("duckduckgo", "Searches for images from DuckDuckGo and displays them in a paginator.")]
    public async Task DuckDuckGo([Autocomplete(typeof(DuckDuckGoAutocompleteHandler))][Summary(description: "The query to search.")] string query)
    {
        await DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation(new EventId(0, "img2"), "Query: \"{query}\", is NSFW: {isNsfw}", query, isNsfw);

        var images = await _duckDuckGoScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict);

        var filteredImages = images
            .Where(x => x.Url.StartsWith("http") && x.SourceUrl.StartsWith("http"))
            .ToArray();

        _logger.LogInformation(new EventId(0, "img2"), "Image results: {count}", filteredImages.Length);

        if (filteredImages.Length == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."]);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePageAsync)
            .WithFergunEmotes()
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(filteredImages.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);

        Task<PageBuilder> GeneratePageAsync(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle(filteredImages[index].Title)
                .WithDescription(_localizer["DuckDuckGo image search"])
                .WithUrl(filteredImages[index].SourceUrl)
                .WithImageUrl(filteredImages[index].Url)
                .WithFooter(_localizer["Page {0} of {1}", index + 1, filteredImages.Length], Constants.DuckDuckGoLogoUrl)
                .WithColor(Color.Orange);

            return Task.FromResult(pageBuilder);
        }
    }

    [SlashCommand("brave", "Searches for images from Brave and displays them in a paginator.")]
    public async Task Brave([Autocomplete(typeof(BraveAutocompleteHandler))][Summary(description: "The query to search.")] string query)
    {
        await DeferAsync();

        bool isNsfw = Context.Channel.IsNsfw();
        _logger.LogInformation(new EventId(0, "img3"), "Query: \"{query}\", is NSFW: {isNsfw}", query, isNsfw);

        var images = await _braveScraper.GetImagesAsync(query, isNsfw ? SafeSearchLevel.Off : SafeSearchLevel.Strict);

        var filteredImages = images
            .Where(x => x.Url.StartsWith("http") && x.SourceUrl.StartsWith("http"))
            .ToArray();

        _logger.LogInformation(new EventId(0, "img3"), "Image results: {count}", filteredImages.Length);

        if (filteredImages.Length == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."]);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePageAsync)
            .WithFergunEmotes()
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(filteredImages.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);

        Task<PageBuilder> GeneratePageAsync(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle(filteredImages[index].Title)
                .WithDescription(_localizer["Brave image search"])
                .WithUrl(filteredImages[index].SourceUrl)
                .WithImageUrl(filteredImages[index].Url)
                .WithFooter(_localizer["Page {0} of {1}", index + 1, filteredImages.Length], Constants.BraveLogoUrl)
                .WithColor(Color.Orange);

            return Task.FromResult(pageBuilder);
        }
    }

    [MessageCommand("Reverse Image Search")]
    public async Task Reverse(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault();
        var embed = message.Embeds.FirstOrDefault(x => x.Image is not null || x.Thumbnail is not null);

        string? url = attachment?.Url ?? embed?.Image?.Url ?? embed?.Thumbnail?.Url;

        if (url is null)
        {
            await Context.Interaction.RespondWarningAsync(_localizer["Unable to get an image URL from the message."], true);
            return;
        }

        var page = new PageBuilder()
            .WithTitle(_localizer["Select an image search engine"])
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
            await ReverseAsync(url, result.Value, false, result.StopInteraction!, true);
        }
    }

    [SlashCommand("reverse", "Reverse image search.")]
    public async Task Reverse([Summary(description: "The url of an image.")] string url,
        [Summary(description: $"The search engine. The default is {nameof(ReverseImageSearchEngine.Yandex)}.")] ReverseImageSearchEngine engine = ReverseImageSearchEngine.Yandex,
        [Summary(description: "Whether to display multiple images in a single page.")] bool multiImages = false)
    {
        await ReverseAsync(url, engine, multiImages, Context.Interaction);
    }

    public async Task ReverseAsync(string url, ReverseImageSearchEngine engine, bool multiImages, IDiscordInteraction interaction, bool ephemeral = false)
    {
        await (engine switch
        {
            ReverseImageSearchEngine.Yandex => YandexAsync(url, multiImages, interaction, ephemeral),
            ReverseImageSearchEngine.Bing => BingAsync(url, multiImages, interaction, ephemeral),
            _ => throw new ArgumentException("Invalid engine", nameof(engine))
        });
    }

    public async Task YandexAsync(string url, bool multiImages, IDiscordInteraction interaction, bool ephemeral = false)
    {
        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        bool isNsfw = Context.Channel.IsNsfw();

        var results = (await _yandexImageSearch.ReverseImageSearchAsync(url, isNsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family))
            .Chunk(multiImages ? 4 : 1)
            .ToArray();

        if (results.Length == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."], ephemeral);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes()
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(results.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource, ephemeral);

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            var builders = results[index].Select(result => new EmbedBuilder()
                .WithTitle(result.Title ?? "")
                .WithDescription(result.Text)
                .WithUrl(multiImages ? "https://yandex.com/images" : result.SourceUrl)
                .WithThumbnailUrl(url)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["Yandex Visual Search | Page {0} of {1}", index + 1, results.Length], Constants.YandexIconUrl)
                .WithColor(Color.Orange));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    public async Task BingAsync(string url, bool multiImages, IDiscordInteraction interaction, bool ephemeral = false)
    {
        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        bool isNsfw = Context.Channel.IsNsfw();

        var results = (await _bingVisualSearch.ReverseImageSearchAsync(url, !isNsfw))
            .Chunk(multiImages ? 4 : 1)
            .ToArray();

        if (results.Length == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."], ephemeral);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes()
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(results.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource, ephemeral);

        MultiEmbedPageBuilder GeneratePage(int index)
        {
            var builders = results[index].Select(result => new EmbedBuilder()
                .WithTitle(result.Text)
                .WithUrl(multiImages ? "https://www.bing.com/visualsearch" : result.SourceUrl)
                .WithThumbnailUrl(url)
                .WithImageUrl(result.Url)
                .WithFooter(_localizer["Bing Visual Search | Page {0} of {1}", index + 1, results.Length], Constants.BingIconUrl)
                .WithColor((Color)result.AccentColor));

            return new MultiEmbedPageBuilder().WithBuilders(builders);
        }
    }

    public enum ReverseImageSearchEngine
    {
        Bing,
        Yandex
    }
}