namespace Fergun;

/// <summary>
/// Represents the settings related to bot lists.
/// </summary>
public class BotListOptions
{
    public const string BotList = nameof(BotList);

    /// <summary>
    /// Gets or sets the update period in minutes.
    /// </summary>
    public int UpdatePeriodInMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the dictionary of tokens.
    /// </summary>
    public IDictionary<BotList, string> Tokens { get; set; } = new Dictionary<BotList, string>();
}