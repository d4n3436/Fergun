using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace Fergun;

public class FergunResult : RuntimeResult
{
    private FergunResult(InteractionCommandError? error, string reason, bool isEphemeral, bool isSilent, IDiscordInteraction? interaction)
        : base(error, reason)
    {
        IsEphemeral = isEphemeral;
        IsSilent = isSilent;
        Interaction = interaction;
    }

    private FergunResult(InteractionCommandError? error, LocalizedString reason, bool isEphemeral, bool isSilent, IDiscordInteraction? interaction)
        : this(error, (string)reason, isEphemeral, isSilent, interaction)
    {
        LocalizedErrorReason = reason;
    }

    /// <summary>
    /// Gets the reason of failure as a localized string.
    /// </summary>
    public LocalizedString? LocalizedErrorReason { get; }

    /// <summary>
    /// Gets a value indicating whether the response should be ephemeral.
    /// </summary>
    public bool IsEphemeral { get; }

    /// <summary>
    /// Gets a value indicating whether the response should be silent.
    /// </summary>
    public bool IsSilent { get; }

    /// <summary>
    /// Gets the interaction that should be responded to.
    /// </summary>
    public IDiscordInteraction? Interaction { get; }

    /// <summary>
    /// Creates a successful instance of the <see cref="FergunResult"/> class.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <returns>A <see cref="FergunResult"/>.</returns>
    public static FergunResult FromSuccess(string? reason = null) => new(null, reason ?? string.Empty, false, false, null);

    /// <summary>
    /// Creates a <see cref="FergunResult"/> with error type <see cref="InteractionCommandError.Unsuccessful"/>.
    /// </summary>
    /// <param name="reason">The reason of the result.</param>
    /// <param name="isEphemeral">Whether the response should be ephemeral.</param>
    /// <param name="interaction">The interaction that should be responded to.</param>
    /// <returns>A <see cref="FergunResult"/>.</returns>
    public static FergunResult FromError(string reason, bool isEphemeral = false, IDiscordInteraction? interaction = null)
        => new(InteractionCommandError.Unsuccessful, reason, isEphemeral, false, interaction);

    /// <summary>
    /// Creates a <see cref="FergunResult"/> with error type <see cref="InteractionCommandError.Unsuccessful"/>.
    /// </summary>
    /// <param name="reason">The reason of the result.</param>
    /// <param name="isEphemeral">Whether the response should be ephemeral.</param>
    /// <param name="interaction">The interaction that should be responded to.</param>
    /// <returns>A <see cref="FergunResult"/>.</returns>
    public static FergunResult FromError(LocalizedString reason, bool isEphemeral = false, IDiscordInteraction? interaction = null)
        => new(InteractionCommandError.Unsuccessful, reason, isEphemeral, false, interaction);

    /// <summary>
    /// Creates a silent <see cref="FergunResult"/>.
    /// </summary>
    /// <returns>A <see cref="FergunResult"/>.</returns>
    public static FergunResult FromSilentError() => new(InteractionCommandError.Unsuccessful, string.Empty, false, true, null);
}