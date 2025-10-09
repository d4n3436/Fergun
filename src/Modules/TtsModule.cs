using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Fergun.Modules.Handlers;
using Fergun.Preconditions;
using GTranslate;
using GTranslate.Translators;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[Ratelimit(2, Constants.GlobalRatelimitPeriod)]
[Group("tts", "TTS commands.")]
public class TtsModule : InteractionModuleBase
{
    private readonly ILogger<TtsModule> _logger;
    private readonly IFergunLocalizer<TtsModule> _localizer;
    private readonly SharedModule _shared;
    private readonly MicrosoftTranslator _microsoftTranslator;

    public TtsModule(ILogger<TtsModule> logger, IFergunLocalizer<TtsModule> localizer, SharedModule shared, MicrosoftTranslator microsoftTranslator)
    {
        _logger = logger;
        _localizer = localizer;
        _shared = shared;
        _microsoftTranslator = microsoftTranslator;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [MessageCommand("TTS")]
    public async Task<RuntimeResult> TtsAsync(IMessage message)
        => await GoogleAsync(message.GetText()); // NOTE: Should we allow the user to specify a TTS engine/language?

    [SlashCommand("google", "Converts text into synthesized speech using Google.")]
    public async Task<RuntimeResult> GoogleAsync([Summary(description: "The text to convert.")] string text,
        [Autocomplete<TtsAutocompleteHandler>][Summary(description: "The target language.")] string? target = null,
        [Summary(description: "Whether to respond ephemerally.")] bool ephemeral = false)
        => await _shared.GoogleTtsAsync(Context.Interaction, text, target ?? Context.Interaction.GetLanguageCode(), ephemeral);

    [SlashCommand("microsoft", "Converts text into synthesized speech using Microsoft Azure.")]
    public async Task<RuntimeResult> MicrosoftAsync([Summary(description: "The text to convert.")] string text,
        [Autocomplete<MicrosoftTtsAutocompleteHandler>][Summary(description: "The target voice.")] MicrosoftVoice voice,
        [Summary(description: "Whether to respond ephemerally.")] bool ephemeral = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return FergunResult.FromError(_localizer["TextMustNotBeEmpty"], true);
        }

        await Context.Interaction.DeferAsync(ephemeral);

        _logger.LogInformation("Sending Microsoft TTS request (text: {Text}, voice: {Voice})", text, voice.ShortName);
        await using var stream = await _microsoftTranslator.TextToSpeechAsync(text, voice);

        await Context.Interaction.FollowupWithFileAsync(new FileAttachment(stream, $"{voice.ShortName}.mp3"), ephemeral: ephemeral);

        return FergunResult.FromSuccess();
    }
}