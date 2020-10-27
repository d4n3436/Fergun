using System;
using System.Collections.Generic;
using Discord;

namespace Fergun.Interactive
{
    /// <summary>
    /// The paginated message.
    /// </summary>
    public class PaginatedMessage
    {
        /// <summary>
        /// Gets or sets the pages.
        /// </summary>
        public IEnumerable<PaginatorPage> Pages { get; set; } = new List<PaginatorPage>();

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the author.
        /// </summary>
        public EmbedAuthorBuilder Author { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the url.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image url.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail url.
        /// </summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// Gets or sets the fields.
        /// </summary>
        public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();

        /// <summary>
        /// Gets or sets the footer override.
        /// </summary>
        public EmbedFooterBuilder FooterOverride { get; set; }

        /// <summary>
        /// Gets or sets the time stamp.
        /// </summary>
        public DateTimeOffset? TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        public Color Color { get; set; } = Color.Default;

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public PaginatedAppearanceOptions Options { get; set; } = PaginatedAppearanceOptions.Default;
    }

    /// <summary>
    /// The page.
    /// </summary>
    public class PaginatorPage
    {
        // All content in here will override the 'Default' Paginated content

        /// <summary>
        /// Gets or sets the author.
        /// </summary>
        public EmbedAuthorBuilder Author { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the url.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the image url.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail url.
        /// </summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// Gets or sets the fields.
        /// </summary>
        public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();

        /// <summary>
        /// Gets or sets the footer override.
        /// </summary>
        public EmbedFooterBuilder FooterOverride { get; set; }

        /// <summary>
        /// Gets or sets the time stamp.
        /// </summary>
        public DateTimeOffset? TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        public Color? Color { get; set; }
    }

}