using System.Collections.Generic;
using Discord;

namespace Fergun.Interactive
{
    /// <summary>
    /// Represents a paginated message.
    /// </summary>
    public class PaginatedMessage : EmbedBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaginatedMessage"/> class.
        /// </summary>
        public PaginatedMessage() : base()
        {
        }

        /// <summary>
        /// Gets or sets the pages. The embeds in this collection will override the base embed.
        /// </summary>
        public IEnumerable<EmbedBuilder> Pages { get; set; } = new List<EmbedBuilder>();

        /// <summary>
        /// Gets or sets the paginator message.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the paginator options.
        /// </summary>
        public PaginatorAppearanceOptions Options { get; set; } = PaginatorAppearanceOptions.Default;
    }
}