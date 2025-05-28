using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Bing;
using Fergun.Apis.Google;
using Fergun.Apis.Yandex;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Selection;
using Fergun.Preconditions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[Ratelimit(2, 20)]
[Group("ocr", "OCR commands.")]
public class OcrModule : InteractionModuleBase
{
    private readonly ILogger<OcrModule> _logger;
    private readonly IFergunLocalizer<OcrModule> _localizer;
    private readonly SharedModule _shared;
    private readonly InteractiveService _interactive;
    private readonly IGoogleLensClient _googleLens;
    private readonly IBingVisualSearch _bingVisualSearch;
    private readonly IYandexImageSearch _yandexImageSearch;

    private const int OcrTextId = 10;
    private const string OcrTranslateKey = "ocr-translate";
    private const string OcrTtsKey = "ocr-tts";

    public OcrModule(ILogger<OcrModule> logger, IFergunLocalizer<OcrModule> localizer, SharedModule shared, InteractiveService interactive,
        IGoogleLensClient googleLens, IBingVisualSearch bingVisualSearch, IYandexImageSearch yandexImageSearch)
    {
        _logger = logger;
        _localizer = localizer;
        _shared = shared;
        _interactive = interactive;
        _googleLens = googleLens;
        _bingVisualSearch = bingVisualSearch;
        _yandexImageSearch = yandexImageSearch;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("bing", "Performs OCR to an image using Bing Visual Search.")]
    public async Task<RuntimeResult> BingAsync([Summary(description: "The URL of an image.")] string? url = null,
        [Summary(description: "An image file.")] IAttachment? file = null)
        => await OcrAsync(OcrEngine.Bing, file?.Url ?? url, Context.Interaction);

    [SlashCommand("google", "Performs OCR to an image using Google Lens.")]
    public async Task<RuntimeResult> GoogleAsync([Summary(description: "The URL of an image.")] string? url = null,
        [Summary(description: "An image file.")] IAttachment? file = null)
        => await OcrAsync(OcrEngine.Google, file?.Url ?? url, Context.Interaction);

    [SlashCommand("yandex", "Performs OCR to an image using Yandex.")]
    public async Task<RuntimeResult> YandexAsync([Summary(description: "The URL of an image.")] string? url = null,
        [Summary(description: "An image file.")] IAttachment? file = null)
        => await OcrAsync(OcrEngine.Yandex, file?.Url ?? url, Context.Interaction);

    [MessageCommand("OCR")]
    public async Task<RuntimeResult> OcrAsync(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault();
        var embed = message.Embeds.FirstOrDefault(x => x.Image is not null || x.Thumbnail is not null);

        string? url = attachment?.Url ?? embed?.Image?.Url ?? embed?.Thumbnail?.Url;

        if (url is null)
        {
            return FergunResult.FromError(_localizer["NoImageUrlInMessage"], true);
        }

        var page = new PageBuilder()
            .WithTitle(_localizer["SelectOCREngine"])
            .WithColor(Color.Orange);

        var selection = new SelectionBuilder<OcrEngine>()
            .AddUser(Context.User)
            .WithOptions(Enum.GetValues<OcrEngine>())
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithSelectionPage(page)
            .WithLocalizedPrompts(_localizer)
            .Build();

        var result = await _interactive.SendSelectionAsync(selection, Context.Interaction, TimeSpan.FromMinutes(1), ephemeral: true);

        if (result.IsSuccess)
        {
            return await OcrAsync(result.Value, url, result.StopInteraction!, Context.Interaction, true);
        }

        return FergunResult.FromSilentError();
    }

    public async Task<RuntimeResult> OcrAsync(OcrEngine ocrEngine, string? url, IDiscordInteraction interaction,
        IDiscordInteraction? originalInteraction = null, bool ephemeral = false)
    {
        if (url is null)
        {
            return FergunResult.FromError(_localizer["UrlOrAttachmentRequired"], true, interaction);
        }

        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return FergunResult.FromError(_localizer["UrlNotWellFormed"], true, interaction);
        }

        if (!Enum.IsDefined(ocrEngine))
        {
            throw new ArgumentException(_localizer["InvalidOCREngine"], nameof(ocrEngine));
        }

        _logger.LogInformation("Sending OCR request (engine: {Engine}, URL: {Url})", ocrEngine, url);

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
            {
                _logger.LogDebug("Deleting original interaction response");
                await originalInteraction.DeleteOriginalResponseAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to delete the original interaction response");
        }

        var stopwatch = Stopwatch.StartNew();
        string? text;

        try
        {
            text = ocrEngine switch
            {
                OcrEngine.Google => await _googleLens.OcrAsync(url),
                OcrEngine.Bing => await _bingVisualSearch.OcrAsync(url),
                OcrEngine.Yandex => await _yandexImageSearch.OcrAsync(url),
                _ => throw new ArgumentException(_localizer["InvalidOCREngine"], nameof(ocrEngine))
            };
        }
        catch (GoogleLensException e)
        {
            _logger.LogWarning(e, "Failed to perform Google Lens OCR to url {Url}", url);
            return FergunResult.FromError(_localizer["GoogleLensOCRError"], ephemeral, interaction);
        }
        catch (BingException e)
        {
            _logger.LogWarning(e, "Failed to perform Bing OCR to url {Url}", url);
            return FergunResult.FromError(e.ImageCategory is null ? e.Message : _localizer[$"Bing{e.ImageCategory}"], ephemeral, interaction);
        }
        catch (YandexException e)
        {
            _logger.LogWarning(e, "Failed to perform Yandex OCR to url {Url}", url);
            return FergunResult.FromError(_localizer["YandexOCRError"], ephemeral, interaction);
        }

        stopwatch.Stop();
        _logger.LogDebug("Received OCR result after {Elapsed}ms", stopwatch.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["OCRNoResults"], ephemeral, interaction);
        }

        var emotes = await Context.Client.GetApplicationEmotesAsync();

        var (ocrEngineName, iconEmote) = ocrEngine switch
        {
            OcrEngine.Google => (_localizer["GoogleLensOCR"], emotes.FirstOrDefault(x => x.Name == Constants.GoogleLensIconEmoteName)),
            OcrEngine.Bing => (_localizer["BingVisualSearch"], emotes.FirstOrDefault(x => x.Name == Constants.BingIconEmoteName)),
            OcrEngine.Yandex => (_localizer["YandexOCR"], emotes.FirstOrDefault(x => x.Name == Constants.YandexIconEmoteName)),
            _ => throw new ArgumentException(_localizer["InvalidOCREngine"], nameof(ocrEngine))
        };

        string translateText;
        if (interaction.TryGetLanguage(out var language))
        {
            _logger.LogDebug("Retrieved GTranslate language \"{Name}\" from code {Code}", language.Name,
                language.ISO6391);

            var localizedString = _localizer["TranslateTo", language.NativeName];
            if (localizedString.ResourceNotFound && language.ISO6391 != "en")
            {
                localizedString = _localizer["TranslateToWithNativeName", language.Name, language.NativeName];
            }

            translateText = localizedString;
        }
        else
        {
            _logger.LogDebug("Unable to get GTranslate language from user locale \"{Locale}\"",
                interaction.GetLocale());

            translateText = _localizer["Translate"];
        }

        string title = $"## {_localizer["OCRResults"]}";
        string inputText = $"## {_localizer["InputImage"]}";
        string footer = $"-# {iconEmote} {_localizer["OCRFooter", ocrEngineName, stopwatch.ElapsedMilliseconds]}";

        var components = new ComponentBuilderV2()
            .WithContainer(new ContainerBuilder()
                .WithTextDisplay(title)
                .WithTextDisplay($"```{text.Replace('`', '´').Truncate(4000 - 6 - title.Length - inputText.Length - footer.Length)}```", OcrTextId)
                .WithActionRow([
                    new ButtonBuilder(translateText, OcrTranslateKey, ButtonStyle.Secondary),
                    new ButtonBuilder("TTS", OcrTtsKey, ButtonStyle.Secondary)
                ])
                .WithAccentColor(Color.Orange))
            .WithContainer(new ContainerBuilder()
                .WithTextDisplay(inputText)
                .WithMediaGallery([new MediaGalleryItemProperties(url, _localizer["InputImage"])])
                .WithTextDisplay(footer)
                .WithAccentColor(Color.Orange))
            .Build();

        await interaction.FollowupAsync(components: components, ephemeral: ephemeral);

        return FergunResult.FromSuccess();
    }

    // Note: Components interactions share the same ratelimit, probably a bug
    [ComponentInteraction(OcrTranslateKey, true)]
    public async Task<RuntimeResult> OcrTranslateAsync()
    {
        _logger.LogInformation("Received translate request from OCR component button");

        var textComponent = ((IComponentInteraction)Context.Interaction).Message.Components.FindComponentById<TextDisplayComponent>(OcrTextId);
        if (textComponent is null)
        {
            return FergunResult.FromError(_localizer["TextNotFound"], true);
        }

        string text = textComponent.Content.Trim('`');

        return await _shared.TranslateAsync(Context.Interaction, text, Context.Interaction.GetLanguageCode(), ephemeral: true);
    }

    [ComponentInteraction(OcrTtsKey, true)]
    public async Task<RuntimeResult> OcrTtsAsync()
    {
        _logger.LogInformation("Received TTS request from OCR component button");

        var textComponent = ((IComponentInteraction)Context.Interaction).Message.Components.FindComponentById<TextDisplayComponent>(OcrTextId);
        if (textComponent is null)
        {
            return FergunResult.FromError(_localizer["TextNotFound"], true);
        }

        string text = textComponent.Content.Trim('`');

        return await _shared.GoogleTtsAsync(Context.Interaction, text, Context.Interaction.GetLanguageCode(), true);
    }
}