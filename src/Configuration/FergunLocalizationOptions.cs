using System.Collections.Generic;

namespace Fergun.Configuration;

/// <summary>
/// Represents the settings related to localization.
/// </summary>
public class FergunLocalizationOptions
{
    /// <summary>
    /// Returns the constant "Localization".
    /// </summary>
    public const string Localization = nameof(Localization);

    /// <summary>
    /// Gets the mapping from .NET culture names to the locale codes accepted by Discord (https://docs.discord.com/developers/reference#locales).
    /// </summary>
    public IDictionary<string, string> SupportedLocales { get; init; } = new Dictionary<string, string>();
}