namespace Fergun;

/// <summary>
/// Represents the settings related to bot lists.
/// </summary>
public class BotListOptions
{
    public const string BotList = nameof(BotList);

    /// <summary>
    /// Gets or sets the update period.
    /// </summary>
    public TimeSpan UpdatePeriod { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of tokens.
    /// </summary>
    public IDictionary<BotList, string> Tokens { get; set; } = new Dictionary<BotList, string>();
}