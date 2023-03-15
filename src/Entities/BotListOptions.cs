using System;
using System.Collections.Generic;

namespace Fergun;

/// <summary>
/// Represents the settings related to bot lists.
/// </summary>
public class BotListOptions
{
    public const string BotList = nameof(BotList);

    /// <summary>
    /// Gets the update period.
    /// </summary>
    public TimeSpan UpdatePeriod { get; init; }

    /// <summary>
    /// Gets the dictionary of tokens.
    /// </summary>
    public IDictionary<BotList, string> Tokens { get; init; } = new Dictionary<BotList, string>();
}