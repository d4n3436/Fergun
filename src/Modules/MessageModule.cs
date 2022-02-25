using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

public class MessageModule : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly ILogger<MessageModule> _logger;
    private readonly AggregateTranslator _translator;
    private readonly GoogleTranslator _googleTranslator;
    private readonly GoogleTranslator2 _googleTranslator2;
    private readonly BingTranslator _bingTranslator;
    private readonly MicrosoftTranslator _microsoftTranslator;
    private readonly YandexTranslator _yandexTranslator;
    private static readonly Lazy<Language[]> _lazyFilteredLanguages = new(() => Language.LanguageDictionary
        .Values
        .Where(x => x.SupportedServices == (TranslationServices.Google | TranslationServices.Bing | TranslationServices.Yandex | TranslationServices.Microsoft))
        .ToArray());

    public MessageModule(ILogger<MessageModule> logger, AggregateTranslator translator, GoogleTranslator googleTranslator,
        GoogleTranslator2 googleTranslator2, BingTranslator bingTranslator, MicrosoftTranslator microsoftTranslator, YandexTranslator yandexTranslator)
    {
        _logger = logger;
        _translator = translator;
        _googleTranslator = googleTranslator;
        _googleTranslator2 = googleTranslator2;
        _bingTranslator = bingTranslator;
        _microsoftTranslator = microsoftTranslator;
        _yandexTranslator = yandexTranslator;
    }

    [MessageCommand("Get Reference")]
    public async Task GetReferencedMessage(IUserMessage message)
    {
        if (message.Type != MessageType.Reply)
        {
            await Context.Interaction.RespondWarningAsync("Message is not an inline reply.", true);
            return;
        }

        if (message.Reference?.MessageId.IsSpecified is not true)
        {
            await Context.Interaction.RespondWarningAsync("Unable to get the referenced message.", true);
            return;
        }

        string url = $"https://discord.com/channels/{message.Reference.GuildId.ToNullable()?.ToString() ?? "@me"}/{message.Reference.ChannelId}/{message.Reference.MessageId}";

        var button = new ComponentBuilder()
            .WithButton("Jump to message", style: ButtonStyle.Link, url: url)
            .Build();

        await RespondAsync("\u200b", ephemeral: true, components: button);
    }

    [MessageCommand("TTS")]
    public async Task TTS(IUserMessage message)
    {
        string text = message.GetText();

        if (string.IsNullOrWhiteSpace(text))
        {
            await Context.Interaction.RespondWarningAsync("The message must contain text.", true);
            return;
        }

        string target = Context.Interaction.GetTwoLetterLanguageCode();

        if (!Language.TryGetLanguage(target, out var language) || !GoogleTranslator2.TextToSpeechLanguages.Contains(language))
        {
            await Context.Interaction.RespondWarningAsync($"Language \"{target}\" not supported.", true);
            return;
        }

        await DeferAsync();

        try
        {
            await using var stream = await _googleTranslator2.TextToSpeechAsync(text, language);
            await Context.Interaction.FollowupWithFileAsync(new FileAttachment(stream, "tts.mp3"));
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning(e, "TTS: Error while getting TTS");
            await Context.Interaction.FollowupWarning("An error occurred.");
        }
        catch (TaskCanceledException e)
        {
            _logger.LogWarning(e, "TTS: Error while getting TTS");
            await Context.Interaction.FollowupWarning("Request timed out.");
        }
    }

    [MessageCommand("Bad Translator")]
    public async Task BadTranslator(IUserMessage message)
    {
        string text = message.GetText();

        if (string.IsNullOrWhiteSpace(text))
        {
            await Context.Interaction.RespondWarningAsync("The message must contain text.", true);
            return;
        }

        await DeferAsync();

        // Create an aggregated translator manually so we can randomize the initial order of the translators and shift them.
        // Bing Translator is not included because it only allows max. 1000 chars per translation
        var translators = new ITranslator[] { _googleTranslator, _googleTranslator2, _microsoftTranslator, _yandexTranslator };
        translators.Shuffle();
        var badTranslator = new AggregateTranslator(translators);

        var languageChain = new List<ILanguage>();
        const int chainCount = 8;
        ILanguage? sourceLanguage = null;
        for (int i = 0; i < chainCount; i++)
        {
            ILanguage targetLanguage;
            if (i == chainCount - 1)
            {
                targetLanguage = sourceLanguage!;
            }
            else
            {
                // Get unique and random languages.
                do
                {
                    targetLanguage = _lazyFilteredLanguages.Value[Random.Shared.Next(_lazyFilteredLanguages.Value.Length)];
                } while (languageChain.Contains(targetLanguage));
            }

            // Shift the translators to avoid spamming them and get more variety
            var last = translators[^1];
            Array.Copy(translators, 0, translators, 1, translators.Length - 1);
            translators[0] = last;

            ITranslationResult result;
            try
            {
                _logger.LogInformation("Translating to: {target}", targetLanguage.ISO6391);
                result = await badTranslator.TranslateAsync(text, targetLanguage);
            }
            catch (Exception e) when (e is TranslatorException or HttpRequestException)
            {
                _logger.LogWarning(e, "Error translating");
                await Context.Interaction.FollowupWarning(e.Message);
                return;
            }

            if (i == 0)
            {
                sourceLanguage = result.SourceLanguage;
                _logger.LogDebug("Badtranslator: Original language: {source}", sourceLanguage.ISO6391);
                languageChain.Add(sourceLanguage);
            }

            _logger.LogDebug("Badtranslator: Translated from {source} to {target}, Service: {service}", result.SourceLanguage.ISO6391, result.TargetLanguage.ISO6391, result.Service);

            text = result.Translation;
            languageChain.Add(targetLanguage);
        }

        string embedText = $"**Language Chain**\n{string.Join(" -> ", languageChain.Select(x => x.ISO6391))}\n\n**Result**\n";

        var embed = new EmbedBuilder()
            .WithTitle("Bad translator")
            .WithDescription($"{embedText}{text.Truncate(EmbedBuilder.MaxDescriptionLength - embedText.Length)}")
            .WithThumbnailUrl(Constants.BadTranslatorLogoUrl)
            .WithColor(Color.Orange)
            .Build();

        await FollowupAsync(embed: embed);
    }
}