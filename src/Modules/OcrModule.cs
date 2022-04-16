using System.Diagnostics;
using System.Globalization;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Bing;
using Fergun.Apis.Yandex;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Selection;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[Group("ocr", "OCR commands.")]
public class OcrModule : InteractionModuleBase
{
    private readonly ILogger<OcrModule> _logger;
    private readonly IFergunLocalizer<OcrModule> _localizer;
    private readonly SharedModule _shared;
    private readonly InteractiveService _interactive;
    private readonly IBingVisualSearch _bingVisualSearch;
    private readonly IYandexImageSearch _yandexImageSearch;

    public OcrModule(ILogger<OcrModule> logger, IFergunLocalizer<OcrModule> localizer, SharedModule shared,
        InteractiveService interactive,IBingVisualSearch bingVisualSearch, IYandexImageSearch yandexImageSearch)
    {
        _logger = logger;
        _localizer = localizer;
        _shared = shared;
        _interactive = interactive;
        _bingVisualSearch = bingVisualSearch;
        _yandexImageSearch = yandexImageSearch;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [MessageCommand("OCR")]
    public async Task Ocr(IMessage message)
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
            .WithTitle(_localizer["Select an OCR engine"])
            .WithColor(Color.Orange);

        var selection = new SelectionBuilder<OcrEngine>()
            .AddUser(Context.User)
            .WithOptions(Enum.GetValues<OcrEngine>())
            .WithSelectionPage(page)
            .Build();

        var result = await _interactive.SendSelectionAsync(selection, Context.Interaction, TimeSpan.FromMinutes(1), ephemeral: true);

        // Attempt to disable the components
        _ = Context.Interaction.ModifyOriginalResponseAsync(x => x.Components = selection.GetOrAddComponents(true).Build());

        if (result.IsSuccess)
        {
            await OcrAsync(result.Value, url, result.StopInteraction!, true);
        }
    }

    [SlashCommand("bing", "Performs OCR to an image using Bing Visual Search.")]
    public async Task Bing([Summary(description: "An image URL.")] string url)
        => await OcrAsync(OcrEngine.Bing, url, Context.Interaction);

    [SlashCommand("yandex", "Performs OCR to an image using Yandex.")]
    public async Task Yandex([Summary(description: "An image URL.")] string url)
        => await OcrAsync(OcrEngine.Yandex, url, Context.Interaction);

    public async Task OcrAsync(OcrEngine ocrEngine, string url, IDiscordInteraction interaction, bool ephemeral = false)
    {
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            await interaction.RespondWarningAsync(_localizer["The URL is not well formed."], true);
            return;
        }

        var ocrTask = ocrEngine switch
        {
            OcrEngine.Bing => _bingVisualSearch.OcrAsync(url),
            OcrEngine.Yandex => _yandexImageSearch.OcrAsync(url),
            _ => throw new ArgumentException("Invalid OCR engine.", nameof(ocrEngine))
        };

        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        var stopwatch = Stopwatch.StartNew();
        string? text;

        try
        {
            text = await ocrTask;
        }
        catch (Exception e) when (e is BingException or YandexException)
        {
            _logger.LogWarning(e, "Failed to perform OCR to url {url}", url);
            await interaction.FollowupWarning(_localizer[e.Message], ephemeral);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await interaction.FollowupWarning(_localizer["The OCR yielded no results."], ephemeral);
            return;
        }

        stopwatch.Stop();

        interaction.TryGetLanguage(out var language);

        var (name, iconUrl) = ocrEngine switch
        {
            OcrEngine.Bing => (_localizer["Bing Visual Search"], Constants.BingIconUrl),
            OcrEngine.Yandex => (_localizer["Yandex OCR"], Constants.YandexIconUrl),
            _ => throw new ArgumentException("Invalid OCR engine.", nameof(ocrEngine))
        };

        string embedText = $"**{_localizer["Output"]}**\n";

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["OCR Results"])
            .WithDescription($"{embedText}```{text.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6)}```")
            .WithThumbnailUrl(url)
            .WithFooter(_localizer["{0} | Processing time: {1}ms", name, stopwatch.ElapsedMilliseconds], iconUrl)
            .WithColor(Color.Orange);

        var components = new ComponentBuilder()
            .WithButton(language is null ? _localizer["Translate"] : _localizer["Translate to {0}", language.Name], "ocrtranslate", ButtonStyle.Secondary)
            .WithButton("TTS", "ocrtts", ButtonStyle.Secondary)
            .Build();

        await interaction.FollowupAsync(embed: builder.Build(), components: components, ephemeral: ephemeral);
    }

    [ComponentInteraction("ocrtranslate", true)]
    public async Task OcrTranslate()
    {
        string text = ((IComponentInteraction)Context.Interaction).Message.Embeds.First().Description;
        int startIndex = text.IndexOf('`', StringComparison.Ordinal) + 3;
        text = text[startIndex..^3];

        await _shared.TranslateAsync(Context.Interaction, text, Context.Interaction.GetLanguageCode(), ephemeral: true, deferLoad: true);
    }

    [ComponentInteraction("ocrtts", true)]
    public async Task OcrTts()
    {
        string text = ((IComponentInteraction)Context.Interaction).Message.Embeds.First().Description;
        int startIndex = text.IndexOf('`', StringComparison.Ordinal) + 3;
        text = text[startIndex..^3];

        await _shared.TtsAsync(Context.Interaction, text, ephemeral: true, deferLoad: true);
    }

    public enum OcrEngine
    {
        Bing,
        Yandex
    }
}