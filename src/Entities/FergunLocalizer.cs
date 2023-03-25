using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Localization;

namespace Fergun;

/// <summary>
/// Represents the default implementation of <see cref="IFergunLocalizer{T}"/>.
/// </summary>
/// <remarks>By default, this localizer has a dependency in the target localizer (<typeparamref name="T"/>), and a shared localizer <see cref="SharedResource"/>,
/// both of which are used to get a localized string.</remarks>
/// <typeparam name="T">The <see cref="Type"/> to provide strings for.</typeparam>
public class FergunLocalizer<T> : IFergunLocalizer<T>
{
    private readonly IStringLocalizer<T> _localizer;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunLocalizer{T}"/> class.
    /// </summary>
    /// <param name="localizer">The target localizer.</param>
    /// <param name="sharedLocalizer">The shared localizer.</param>
    public FergunLocalizer(IStringLocalizer<T> localizer, IStringLocalizer<SharedResource> sharedLocalizer)
    {
        _localizer = localizer;
        _sharedLocalizer = sharedLocalizer;
    }

    /// <inheritdoc/>
    public CultureInfo CurrentCulture { get; set; } = CultureInfo.CurrentUICulture;

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => _localizer.GetAllStrings(includeParentCultures).Concat(_sharedLocalizer.GetAllStrings(includeParentCultures));

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
}