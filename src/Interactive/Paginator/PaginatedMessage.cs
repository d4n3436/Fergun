using System.Collections.Generic;
using System.Linq;
using Discord;

namespace Fergun.Interactive
{
    /// <summary>
    /// Represents a paginated message.
    /// </summary>
    public class PaginatedMessage : EmbedBuilder
    {
        /// <summary>
        /// Gets or sets the pages. The embeds in this collection will override the base embed.
        /// </summary>
        public IEnumerable<EmbedBuilder> Pages { get; set; } = new List<EmbedBuilder>();

        /// <summary>
        /// Gets or sets the paginator messages.
        /// </summary>
        public IEnumerable<string> Texts { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets or sets the paginator options.
        /// </summary>
        public PaginatorAppearanceOptions Options { get; set; } = PaginatorAppearanceOptions.Default;
    }
}