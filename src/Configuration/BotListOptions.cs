using System;
using System.Collections.Generic;

namespace Fergun.Configuration;

/// <summary>
/// Represents the settings related to bot lists.
/// </summary>
public class BotListOptions
{
    /// <summary>
    /// Returns the constant "BotList".
    /// </summary>
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