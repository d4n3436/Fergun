using System;
using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Fergun;

/// <summary>
/// Represents a <see cref="IStringLocalizer{T}"/> with a current culture which can be changed.
/// </summary>
/// <typeparam name="T">The <see cref="Type"/> to provide strings for.</typeparam>
public interface IFergunLocalizer<out T> : IStringLocalizer<T>
{
    /// <summary>
    /// Gets or sets the current culture.
    /// </summary>
    CultureInfo CurrentCulture { get; set; }
}