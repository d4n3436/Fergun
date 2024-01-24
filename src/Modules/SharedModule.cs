using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
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
    private readonly IFergunTranslator _translator;
    private readonly GoogleTranslator2 _googleTranslator2;

    public SharedModule(ILogger<SharedModule> logger, IFergunLocalizer<SharedResource> localizer, IFergunTranslator translator, GoogleTranslator2 googleTranslator2)
    {
        _logger = logger;
        _localizer = localizer;
        _translator = translator;
        _googleTranslator2 = googleTranslator2;
    }

    public async Task<RuntimeResult> TranslateAsync(IDiscordInteraction interaction, string text, string target, string? source = null, bool ephemeral = false)
    {
        _localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["TextMustNotBeEmpty"], true, interaction);
        }

        if (!Language.TryGetLanguage(target, out _))
        {
            return FergunResult.FromError(_localizer["InvalidTargetLanguage", target], true, interaction);
        }

        if (source != null && !Language.TryGetLanguage(source, out _))
        {
            return FergunResult.FromError(_localizer["InvalidSourceLanguage", source], true, interaction);
        }

        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        _logger.LogInformation("Performing translation ({Source} -> {Target}), ephemeral: {Ephemeral}", source ?? "auto", target, ephemeral);
        ITranslationResult result;

        try
        {
            result = await _translator.TranslateAsync(text, target, source);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error translating text {Text} ({Source} -> {Target})", text, source ?? "auto", target);
            return FergunResult.FromError(e.Message, ephemeral, interaction);
        }

        _logger.LogDebug("Received translation from service: {Service}", result.Service);

        if (source is null)
        {
            _logger.LogDebug("Detected language: {Name} ({Code})", result.SourceLanguage.Name, result.SourceLanguage.ISO6391);
        }
        
        string thumbnailUrl = result.Service switch
        {
            "BingTranslator" => Constants.BingTranslatorLogoUrl,
            "MicrosoftTranslator" => Constants.MicrosoftAzureLogoUrl,
            "YandexTranslator" => Constants.YandexTranslateLogoUrl,
            _ => Constants.GoogleTranslateLogoUrl
        };

        string embedText = $"**{_localizer[source is null ? "SourceLanguageDetected" : "SourceLanguage"]}**\n" +
                            $"{DisplayName(result.SourceLanguage)}\n\n" +
                            $"**{_localizer["TargetLanguage"]}**\n" +
                            $"{DisplayName(result.TargetLanguage)}\n\n" +
                            $"**{_localizer["Result"]}**\n";

        string translation = result.Translation.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6);

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["TranslationResult"])
            .WithDescription($"{embedText}```{translation}```")
            .WithThumbnailUrl(thumbnailUrl)
            .WithColor(Color.Orange);

        await interaction.FollowupAsync(embed: builder.Build(), ephemeral: ephemeral);

        return FergunResult.FromSuccess();

        static string DisplayName(ILanguage language)
            => $"{language.Name}{(language is not Language lang || lang.NativeName == language.Name ? string.Empty : $" ({lang.NativeName})")}";
    }

    public async Task<RuntimeResult> GoogleTtsAsync(IDiscordInteraction interaction, string text, string target, bool ephemeral = false)
    {
        _localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["TextMustNotBeEmpty"], true, interaction);
        }

        if (!Language.TryGetLanguage(target, out var language) || !GoogleTranslator2.TextToSpeechLanguages.Contains(language))
        {
            return FergunResult.FromError(_localizer["LanguageNotSupported", target], true, interaction);
        }

        if (interaction is IComponentInteraction componentInteraction)
        {
            await componentInteraction.DeferLoadingAsync(ephemeral);
        }
        else
        {
            await interaction.DeferAsync(ephemeral);
        }

        _logger.LogInformation("Performing TTS with {Translator} and language: {Name} ({Code}), ephemeral: {Ephemeral}", nameof(GoogleTranslator2), language.Name, language.ISO6391, ephemeral);

        await using var stream = await _googleTranslator2.TextToSpeechAsync(text, language);
        await interaction.FollowupWithFileAsync(new FileAttachment(stream, "tts.mp3"), ephemeral: ephemeral);

        return FergunResult.FromSuccess();
    }
}