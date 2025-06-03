using System;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Localization;

namespace Fergun;

/// <summary>
/// Represents a <see cref="LocalizedString"/> that also includes a localized English value.
/// </summary>
public sealed class DualLocalizedString : LocalizedString
{
    private readonly Lazy<string> _lazyEnglishValue;

    private DualLocalizedString(LocalizedString localizedString, string englishValue)
        : base(localizedString.Name, localizedString.Value, localizedString.ResourceNotFound, localizedString.SearchedLocation)
    {
        _lazyEnglishValue = new Lazy<string>(englishValue);
    }

    private DualLocalizedString(LocalizedString localizedString, IStringLocalizer localizer, string name, object[] arguments)
        : base(localizedString.Name, localizedString.Value, localizedString.ResourceNotFound, localizedString.SearchedLocation)
    {
        _lazyEnglishValue = new Lazy<string>(() =>
        {
            Thread.CurrentThread.CurrentUICulture = EnglishCulture;
            return localizer[name, arguments].Value;
        }, true);
    }

    /// <summary>
    /// Gets the actual string in English.
    /// </summary>
    public string EnglishValue => _lazyEnglishValue.Value;

    private static CultureInfo EnglishCulture => CultureInfo.GetCultureInfo("en");

    /// <inheritdoc cref="Create(IStringLocalizer, CultureInfo, string, object[])"/>
    public static DualLocalizedString Create(IStringLocalizer localizer, CultureInfo culture, string name)
        => Create(localizer, culture, name, []);

    /// <summary>
    /// Creates a new <see cref="DualLocalizedString"/> using the provided values.
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    /// <param name="culture">The target culture.</param>
    /// <param name="name">The name of the string resource.</param>
    /// <param name="arguments">The values to format the string with.</param>
    /// <returns>The formatted string resource as a <see cref="DualLocalizedString"/>.</returns>
    public static DualLocalizedString Create(IStringLocalizer localizer, CultureInfo culture, string name, params object[] arguments)
    {
        Thread.CurrentThread.CurrentUICulture = culture;
        var localized = localizer[name, arguments];

        return culture.Equals(EnglishCulture)
            ? new DualLocalizedString(localized, localized.Value)
            : new DualLocalizedString(localized, localizer, name, arguments);
    }
}