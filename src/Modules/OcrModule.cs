using System.Diagnostics;
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
    private readonly SharedModule _shared;
    private readonly InteractiveService _interactive;
    private readonly IBingVisualSearch _bingVisualSearch;
    private readonly IYandexImageSearch _yandexImageSearch;

    public OcrModule(ILogger<OcrModule> logger, SharedModule shared, InteractiveService interactive, IBingVisualSearch bingVisualSearch, IYandexImageSearch yandexImageSearch)
    {
        _logger = logger;
        _shared = shared;
        _interactive = interactive;
        _bingVisualSearch = bingVisualSearch;
        _yandexImageSearch = yandexImageSearch;
    }

    [MessageCommand("OCR")]
    public async Task Ocr(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault();
        var embed = message.Embeds.FirstOrDefault(x => x.Image is not null || x.Thumbnail is not null);

        string? url = attachment?.Url ?? embed?.Image?.Url ?? embed?.Thumbnail?.Url;

        if (url is null)
        {
            await Context.Interaction.RespondWarningAsync("Unable to get an image URL from the message.", true);
            return;
        }

        var page = new PageBuilder()
            .WithTitle("Select an OCR engine")
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
            await interaction.RespondWarningAsync("The URL is not well formed.", true);
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
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to perform OCR to url {url}", url);
            await interaction.FollowupWarning(e.Message, ephemeral);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await interaction.FollowupWarning("The OCR did not give results.", ephemeral);
            return;
        }

        stopwatch.Stop();

        interaction.TryGetLanguage(out var language);

        var (name, iconUrl) = ocrEngine switch
        {
            OcrEngine.Bing => ("Bing Visual Search", Constants.BingIconUrl),
            OcrEngine.Yandex => ("Yandex OCR", Constants.YandexIconUrl),
            _ => throw new ArgumentException("Invalid OCR engine.", nameof(ocrEngine))
        };

        string embedText = "**Output**\n";

        var builder = new EmbedBuilder()
            .WithTitle("OCR Results")
            .WithDescription($"{embedText}```{text.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6)}```")
            .WithThumbnailUrl(url)
            .WithFooter($"{name} | Processing time: {stopwatch.ElapsedMilliseconds}ms", iconUrl)
            .WithColor(Color.Orange);

        var components = new ComponentBuilder()
            .WithButton($"Translate{(language is null ? "" : $" to {language.Name}")}", "ocrtranslate", ButtonStyle.Secondary)
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