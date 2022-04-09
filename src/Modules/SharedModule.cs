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
    private readonly AggregateTranslator _translator;
    private readonly GoogleTranslator2 _googleTranslator2;

    public SharedModule(ILogger<SharedModule> logger, AggregateTranslator translator, GoogleTranslator2 googleTranslator2)
    {
        _logger = logger;
        _translator = translator;
        _googleTranslator2 = googleTranslator2;
    }

    public async Task TranslateAsync(IDiscordInteraction interaction, string text, string target, string? source = null, bool ephemeral = false, bool deferLoad = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await interaction.RespondWarningAsync("The message must contain text.", true);
            return;
        }

        if (!Language.TryGetLanguage(target, out _))
        {
            await interaction.RespondWarningAsync($"Invalid target language \"{target}\".", true);
            return;
        }

        if (source != null && !Language.TryGetLanguage(source, out _))
        {
            await interaction.RespondWarningAsync($"Invalid source language \"{source}\".", true);
            return;
        }

        if (deferLoad && interaction is IComponentInteraction componentInteraction)
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
            _logger.LogWarning(new(0, "Translate"), e, "Error translating text {text} ({source} -> {target})", text, source ?? "auto", target);
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

        string embedText = $"**Source language** {(source == null ? "**(Detected)**" : "")}\n" +
                           $"{result.SourceLanguage.Name}\n\n" +
                           "**Target language**\n" +
                           $"{result.TargetLanguage.Name}" +
                           "\n\n**Result**\n";

        string translation = result.Translation.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6);

        var builder = new EmbedBuilder()
            .WithTitle("Translation result")
            .WithDescription($"{embedText}```{translation}```")
            .WithThumbnailUrl(thumbnailUrl)
            .WithColor(Color.Orange);

        await interaction.FollowupAsync(embed: builder.Build(), ephemeral: ephemeral);
    }

    public async Task TtsAsync(IDiscordInteraction interaction, string text, string? target = null, bool ephemeral = false, bool deferLoad = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await interaction.RespondWarningAsync("The message must contain text.", true);
            return;
        }

        target ??= interaction.GetLanguageCode();

        if (!Language.TryGetLanguage(target, out var language) || !GoogleTranslator2.TextToSpeechLanguages.Contains(language))
        {
            await interaction.RespondWarningAsync($"Language \"{target}\" not supported.", true);
            return;
        }

        if (deferLoad && interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        try
        {
            await using var stream = await _googleTranslator2.TextToSpeechAsync(text, language);
            await interaction.FollowupWithFileAsync(new FileAttachment(stream, "tts.mp3"), ephemeral: ephemeral);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "TTS: Error obtaining TTS from text {text} ({language})", text, language);
            await interaction.FollowupWarning(e.Message, ephemeral);
        }
    }
}