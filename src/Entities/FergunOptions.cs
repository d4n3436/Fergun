using Fergun.Interactive.Pagination;

namespace Fergun;

/// <summary>
/// Represents general Fergun settings.
/// </summary>
public class FergunOptions
{
    public const string Fergun = nameof(Fergun);

    /// <summary>
    /// Gets or sets the support server URL.
    /// </summary>
    public Uri? SupportServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the default paginator timeout.
    /// </summary>
    public TimeSpan PaginatorTimeout { get; set; }

    /// <summary>
    /// Gets or sets the default selection timeout.
    /// </summary>
    public TimeSpan SelectionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of paginator emotes.
    /// </summary>
    public IDictionary<PaginatorAction, string> PaginatorEmotes { get; set; } = new Dictionary<PaginatorAction, string>();
}