using System;
using System.ComponentModel.DataAnnotations;

namespace Fergun.Configuration;

/// <summary>
/// Represents general Fergun settings.
/// </summary>
public class FergunOptions
{
    /// <summary>
    /// Returns the constant "Fergun".
    /// </summary>
    public const string Fergun = nameof(Fergun);

    /// <summary>
    /// Gets the support server URL.
    /// </summary>
    [Url]
    public string? SupportServerUrl { get; init; }

    /// <summary>
    /// Gets the voting page URL.
    /// </summary>
    [Url]
    public string? VoteUrl { get; init; }

    /// <summary>
    /// Gets the donation page URL.
    /// </summary>
    [Url]
    public string? DonationUrl { get; init; }

    /// <summary>
    /// Gets the default paginator timeout.
    /// </summary>
    public TimeSpan PaginatorTimeout { get; init; }

    /// <summary>
    /// Gets the default selection timeout.
    /// </summary>
    public TimeSpan SelectionTimeout { get; init; }
}