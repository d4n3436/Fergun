namespace Fergun;

/// <summary>
/// Represents the configuration of Fergun.
/// </summary>
public class FergunConfig
{
    /// <summary>
    /// Gets or sets the token of the bot.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the guild to register the guild commands.
    /// </summary>
    public ulong TargetGuildId { get; set; }
}