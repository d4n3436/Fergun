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
    public async Task<RuntimeResult> OcrAsync(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault();
        var embed = message.Embeds.FirstOrDefault(x => x.Image is not null || x.Thumbnail is not null);

        string? url = attachment?.Url ?? embed?.Image?.Url ?? embed?.Thumbnail?.Url;

        if (url is null)
        {
            return FergunResult.FromError(_localizer["Unable to get an image URL from the message."], true);
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
            return await OcrAsync(result.Value, url, result.StopInteraction!, true);
        }

        return FergunResult.FromSilentError();
    }

    [SlashCommand("bing", "Performs OCR to an image using Bing Visual Search.")]
    public async Task<RuntimeResult> BingAsync([Summary(description: "The URL of an image.")] string? url = null,
        [Summary(description: "An image file.")] IAttachment? file = null)
        => await OcrAsync(OcrEngine.Bing, file?.Url ?? url, Context.Interaction);

    [SlashCommand("yandex", "Performs OCR to an image using Yandex.")]
    public async Task<RuntimeResult> YandexAsync([Summary(description: "The URL of an image.")] string? url = null,
        [Summary(description: "An image file.")] IAttachment? file = null)
        => await OcrAsync(OcrEngine.Yandex, file?.Url ?? url, Context.Interaction);

    public async Task<RuntimeResult> OcrAsync(OcrEngine ocrEngine, string? url, IDiscordInteraction interaction, bool ephemeral = false)
    {
        if (url is null)
        {
            return FergunResult.FromError(_localizer["A URL or attachment is required."], true, interaction);
        }

        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return FergunResult.FromError(_localizer["The URL is not well formed."], true, interaction);
        }

        var ocrTask = ocrEngine switch
        {
            OcrEngine.Bing => _bingVisualSearch.OcrAsync(url),
            OcrEngine.Yandex => _yandexImageSearch.OcrAsync(url),
            _ => throw new ArgumentException(_localizer["Invalid OCR engine."], nameof(ocrEngine))
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
            return FergunResult.FromError(_localizer[e.Message], ephemeral, interaction);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["The OCR yielded no results."], ephemeral, interaction);
        }

        stopwatch.Stop();

        interaction.TryGetLanguage(out var language);

        var (name, iconUrl) = ocrEngine switch
        {
            OcrEngine.Bing => (_localizer["Bing Visual Search"], Constants.BingIconUrl),
            OcrEngine.Yandex => (_localizer["Yandex OCR"], Constants.YandexIconUrl),
            _ => throw new ArgumentException(_localizer["Invalid OCR engine."], nameof(ocrEngine))
        };

        string embedText = $"**{_localizer["Output"]}**\n";

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["OCR Results"])
            .WithDescription($"{embedText}```{text.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6)}```")
            .WithThumbnailUrl(url)
            .WithFooter(_localizer["{0} | Processing time: {1}ms", name, stopwatch.ElapsedMilliseconds], iconUrl)
            .WithColor(Color.Orange);

        string buttonText;
        if (language is null)
        {
            buttonText = _localizer["Translate"];
        }
        else
        {
            var localizedString = _localizer["Translate to {0}", language.NativeName];
            if (localizedString.ResourceNotFound && language.ISO6391 != "en")
            {
                localizedString = _localizer["Translate to {0} ({1})", language.Name, language.NativeName];
            }

            buttonText = localizedString.Value;
        }

        var components = new ComponentBuilder()
            .WithButton(buttonText, "ocrtranslate", ButtonStyle.Secondary)
            .WithButton("TTS", "ocrtts", ButtonStyle.Secondary)
            .Build();

        await interaction.FollowupAsync(embed: builder.Build(), components: components, ephemeral: ephemeral);

        return FergunResult.FromSuccess();
    }

    [ComponentInteraction("ocrtranslate", true)]
    public async Task<RuntimeResult> OcrTranslateAsync()
    {
        string text = ((IComponentInteraction)Context.Interaction).Message.Embeds.First().Description;
        int startIndex = text.IndexOf('`', StringComparison.Ordinal) + 3;
        text = text[startIndex..^3];

        return await _shared.TranslateAsync(Context.Interaction, text, Context.Interaction.GetLanguageCode(), ephemeral: true);
    }

    [ComponentInteraction("ocrtts", true)]
    public async Task<RuntimeResult> OcrTtsAsync()
    {
        string text = ((IComponentInteraction)Context.Interaction).Message.Embeds.First().Description;
        int startIndex = text.IndexOf('`', StringComparison.Ordinal) + 3;
        text = text[startIndex..^3];

        return await _shared.GoogleTtsAsync(Context.Interaction, text, Context.Interaction.GetLanguageCode(), true);
    }

    public enum OcrEngine
    {
        Bing,
        Yandex
    }
}