using System;
using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Fergun;

/// <summary>
/// Represents a <see cref="IStringLocalizer"/> with a current culture which can be changed.
/// </summary>
public interface IFergunLocalizer : IStringLocalizer
{
    /// <summary>
    /// Gets or sets the current culture.
    /// </summary>
    CultureInfo CurrentCulture { get; set; }
}

/// <summary>
/// Represents the generic variant of <see cref="IFergunLocalizer"/>.
/// </summary>
/// <typeparam name="T">The <see cref="Type"/> to provide strings for.</typeparam>
public interface IFergunLocalizer<out T> : IFergunLocalizer, IStringLocalizer<T>
{
}