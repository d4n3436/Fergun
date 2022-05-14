namespace Fergun;

/// <summary>
/// Represents the settings related to Fergun.
/// </summary>
public class FergunOptions
{
    public const string Fergun = nameof(Fergun);

    /// <summary>
    /// Gets or sets the token of the bot.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the guild to register the commands for testing.
    /// </summary>
    public ulong TestingGuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild to register owner commands.
    /// </summary>
    public ulong OwnerCommandsGuildId { get; set; }
}