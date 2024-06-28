using System.Collections.Generic;
using Discord;

namespace Fergun.Services;

/// <summary>
/// Stores the registered application commands.
/// </summary>
public class ApplicationCommandCache
{
    /// <summary>
    /// Gets the registered application commands.
    /// </summary>
    public IReadOnlyCollection<IApplicationCommand> CachedCommands { get; internal set; } = [];
}