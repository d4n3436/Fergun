using System.Globalization;
using Discord;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

/// <summary>
/// Module that contains shared methods used across modules.
/// </summary>
public class SharedModule
{
    private readonly ILogger<SharedModule> _logger;
    private readonly IFergunLocalizer<SharedResource> _localizer;
    private readonly ITranslator _translator;
    private readonly GoogleTranslator2 _googleTranslator2;

    public SharedModule(ILogger<SharedModule> logger, IFergunLocalizer<SharedResource> localizer, ITranslator translator, GoogleTranslator2 googleTranslator2)
    {
        _logger = logger;
        _localizer = localizer;
        _translator = translator;
        _googleTranslator2 = googleTranslator2;
    }

    public async Task TranslateAsync(IDiscordInteraction interaction, string text, string target, string? source = null, bool ephemeral = false)
    {
        _localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

        if (string.IsNullOrWhiteSpace(text))
        {
            await interaction.RespondWarningAsync(_localizer["The message must contain text."], true);
            return;
        }

        if (!Language.TryGetLanguage(target, out _))
        {
            await interaction.RespondWarningAsync(_localizer["Invalid target language \"{0}\".", target], true);
            return;
        }

        if (source != null && !Language.TryGetLanguage(source, out _))
        {
            await interaction.RespondWarningAsync(_localizer["Invalid source language \"{0}\".", source], true);
            return;
        }

        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        ITranslationResult result;

        try
        {
            result = await _translator.TranslateAsync(text, target, source);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error translating text {text} ({source} -> {target})", text, source ?? "auto", target);
            await interaction.FollowupWarning(e.Message, ephemeral);
            return;
        }

        string thumbnailUrl = result.Service switch
        {
            "BingTranslator" => Constants.BingTranslatorLogoUrl,
            "MicrosoftTranslator" => Constants.MicrosoftAzureLogoUrl,
            "YandexTranslator" => Constants.YandexTranslateLogoUrl,
            _ => Constants.GoogleTranslateLogoUrl
        };

        string embedText = $"**{_localizer[source is null ? "Source language (Detected)" : "Source language"]}**\n" +
                            $"{result.SourceLanguage.Name}\n\n" +
                            $"**{_localizer["Target language"]}**\n" +
                            $"{result.TargetLanguage.Name}" +
                            $"\n\n**{_localizer["Result"]}**\n";

        string translation = result.Translation.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6);

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["Translation result"])
            .WithDescription($"{embedText}```{translation}```")
            .WithThumbnailUrl(thumbnailUrl)
            .WithColor(Color.Orange);

        await interaction.FollowupAsync(embed: builder.Build(), ephemeral: ephemeral);
    }

    public async Task TtsAsync(IDiscordInteraction interaction, string text, string target, bool ephemeral = false)
    {
        _localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

        if (string.IsNullOrWhiteSpace(text))
        {
            await interaction.RespondWarningAsync(_localizer["The message must contain text."], true);
            return;
        }

        if (!Language.TryGetLanguage(target, out var language) || !GoogleTranslator2.TextToSpeechLanguages.Contains(language))
        {
            await interaction.RespondWarningAsync(_localizer["Language \"{0}\" not supported.", target], true);
            return;
        }

        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        await using var stream = await _googleTranslator2.TextToSpeechAsync(text, language);
        await interaction.FollowupWithFileAsync(new FileAttachment(stream, "tts.mp3"), ephemeral: ephemeral);
    }
}