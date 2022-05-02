using System.Globalization;
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
            return FergunResult.FromError(_localizer["The text must not be empty."], true, interaction);
        }

        if (!Language.TryGetLanguage(target, out _))
        {
            return FergunResult.FromError(_localizer["Invalid target language \"{0}\".", target], true, interaction);
        }

        if (source != null && !Language.TryGetLanguage(source, out _))
        {
            return FergunResult.FromError(_localizer["Invalid source language \"{0}\".", source], true, interaction);
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
            return FergunResult.FromError(e.Message, ephemeral, interaction);
        }

        string thumbnailUrl = result.Service switch
        {
            "BingTranslator" => Constants.BingTranslatorLogoUrl,
            "MicrosoftTranslator" => Constants.MicrosoftAzureLogoUrl,
            "YandexTranslator" => Constants.YandexTranslateLogoUrl,
            _ => Constants.GoogleTranslateLogoUrl
        };

        string embedText = $"**{_localizer[source is null ? "Source language (Detected)" : "Source language"]}**\n" +
                            $"{DisplayName(result.SourceLanguage)}\n\n" +
                            $"**{_localizer["Target language"]}**\n" +
                            $"{DisplayName(result.TargetLanguage)}\n\n" +
                            $"**{_localizer["Result"]}**\n";

        string translation = result.Translation.Replace('`', '´').Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length - 6);

        var builder = new EmbedBuilder()
            .WithTitle(_localizer["Translation result"])
            .WithDescription($"{embedText}```{translation}```")
            .WithThumbnailUrl(thumbnailUrl)
            .WithColor(Color.Orange);

        await interaction.FollowupAsync(embed: builder.Build(), ephemeral: ephemeral);

        return FergunResult.FromSuccess();
        
        static string DisplayName(ILanguage language)
            => $"{language.Name}{(language is not Language lang || lang.NativeName == language.Name ? "" : $" ({lang.NativeName})")}";
    }

    public async Task<RuntimeResult> GoogleTtsAsync(IDiscordInteraction interaction, string text, string target, bool ephemeral = false)
    {
        _localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["The text must not be empty."], true, interaction);
        }

        if (!Language.TryGetLanguage(target, out var language) || !GoogleTranslator2.TextToSpeechLanguages.Contains(language))
        {
            return FergunResult.FromError(_localizer["Language \"{0}\" not supported.", target], true, interaction);
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

        return FergunResult.FromSuccess();
    }
}