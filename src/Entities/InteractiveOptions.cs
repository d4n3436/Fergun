using Fergun.Interactive;
using Fergun.Interactive.Pagination;

namespace Fergun;

/// <summary>
/// Represents the settings related to <see cref="InteractiveService"/>.
/// </summary>
public class InteractiveOptions
{
    public const string Interactive = nameof(Interactive);
    
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