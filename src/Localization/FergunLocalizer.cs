using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fergun.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Fergun.Localization;

/// <summary>
/// Represents the default implementation of <see cref="IFergunLocalizer"/>.
/// </summary>
/// <remarks>By default, this localizer has a dependency in the target localizer (<see cref="IStringLocalizer"/>), and a shared localizer (<see cref="SharedResource"/>),
/// both of which are used to get a localized string.</remarks>
public class FergunLocalizer : IFergunLocalizer
{
    private readonly IStringLocalizer _localizer;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunLocalizer"/> class.
    /// </summary>
    /// <param name="localizer">The target localizer.</param>
    /// <param name="sharedLocalizer">The shared localizer.</param>
    /// <param name="options">The localization options.</param>
    public FergunLocalizer(IStringLocalizer localizer, IStringLocalizer<SharedResource> sharedLocalizer, IOptions<FergunLocalizationOptions> options)
    {
        _localizer = localizer;
        _sharedLocalizer = sharedLocalizer;
        SupportedCultures = [DefaultCulture, ..options.Value.SupportedLocales.Keys.Select(CultureInfo.GetCultureInfo)];
    }

    /// <summary>
    /// Gets the default <see cref="CultureInfo"/> (English).
    /// </summary>
    public static CultureInfo DefaultCulture => CultureInfo.GetCultureInfo("en");

    /// <summary>
    /// Gets a read-only list containing the languages that Fergun supports.
    /// </summary>
    /// <remarks>Always includes <see cref="DefaultCulture"/> (English) plus every entry from <see cref="FergunLocalizationOptions.SupportedLocales"/>.</remarks>
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <inheritdoc/>
    /// <remarks>Setting a value won't have an effect if the value is not in <see cref="SupportedCultures"/>.</remarks>
    public CultureInfo CurrentCulture
    {
        get;
        set
        {
            if (SupportedCultures.Contains(value))
            {
                field = value;
            }
        }
    } = DefaultCulture;

    /// <inheritdoc/>
    public LocalizedString this[string name]
    {
        get
        {
            var localized = DualLocalizedString.Create(_localizer, CurrentCulture, name);
            return localized.ResourceNotFound ? DualLocalizedString.Create(_sharedLocalizer, CurrentCulture, name) : localized;
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var localized = DualLocalizedString.Create(_localizer, CurrentCulture, name, arguments);
            return localized.ResourceNotFound ? DualLocalizedString.Create(_sharedLocalizer, CurrentCulture, name, arguments) : localized;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => _localizer.GetAllStrings(includeParentCultures).Concat(_sharedLocalizer.GetAllStrings(includeParentCultures));
}

/// <summary>
/// Represents the generic variant of <see cref="FergunLocalizer"/>.
/// </summary>
/// <remarks>By default, this localizer has a dependency in the target localizer (<see cref="IStringLocalizer{T}"/>), and a shared localizer (<see cref="SharedResource"/>),
/// both of which are used to get a localized string.</remarks>
/// <typeparam name="T">The <see cref="Type"/> to provide strings for.</typeparam>
public class FergunLocalizer<T> : FergunLocalizer, IFergunLocalizer<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FergunLocalizer{T}"/> class.
    /// </summary>
    /// <param name="localizer">The target localizer.</param>
    /// <param name="sharedLocalizer">The shared localizer.</param>
    /// <param name="options">The localization options.</param>
    public FergunLocalizer(IStringLocalizer<T> localizer, IStringLocalizer<SharedResource> sharedLocalizer, IOptions<FergunLocalizationOptions> options)
        : base(localizer, sharedLocalizer, options)
    {
    }
}