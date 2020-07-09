namespace Discord.Addons.Interactive
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The paginated message.
    /// </summary>
    public class PaginatedMessage
    {
        /// <summary>
        /// Gets or sets the pages.
        /// </summary>
        public IEnumerable<Page> Pages { get; set; } = new List<Page>();

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the author.
        /// </summary>
        public EmbedAuthorBuilder Author { get; set; } = null;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = null;

        /// <summary>
        /// Gets or sets the url.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image url.
        /// </summary>
        public string ImageUrl { get; set; } = null;

        /// <summary>
        /// Gets or sets the thumbnail url.
        /// </summary>
        public string ThumbnailUrl { get; set; } = null;

        /// <summary>
        /// Gets or sets the fields.
        /// </summary>
        public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();

        /// <summary>
        /// Gets or sets the footer override.
        /// </summary>
        public EmbedFooterBuilder FooterOverride { get; set; } = null;

        /// <summary>
        /// Gets or sets the time stamp.
        /// </summary>
        public DateTimeOffset? TimeStamp { get; set; } = null;

        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        public Color Color { get; set; } = Color.Default;

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public PaginatedAppearanceOptions Options { get; set; } = PaginatedAppearanceOptions.Default;

        /// <summary>
        /// The page.
        /// </summary>
        public class Page
        {
            // All content in here will override the 'Default' Paginated content

            /// <summary>
            /// Gets or sets the author.
            /// </summary>
            public EmbedAuthorBuilder Author { get; set; } = null;

            /// <summary>
            /// Gets or sets the title.
            /// </summary>
            public string Title { get; set; } = null;

            /// <summary>
            /// Gets or sets the url.
            /// </summary>
            public string Url { get; set; } = null;

            /// <summary>
            /// Gets or sets the description.
            /// </summary>
            public string Description { get; set; } = null;

            /// <summary>
            /// Gets or sets the image url.
            /// </summary>
            public string ImageUrl { get; set; } = null;

            /// <summary>
            /// Gets or sets the thumbnail url.
            /// </summary>
            public string ThumbnailUrl { get; set; } = null;

            /// <summary>
            /// Gets or sets the fields.
            /// </summary>
            public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();

            /// <summary>
            /// Gets or sets the footer override.
            /// </summary>
            public EmbedFooterBuilder FooterOverride { get; set; } = null;

            /// <summary>
            /// Gets or sets the time stamp.
            /// </summary>
            public DateTimeOffset? TimeStamp { get; set; } = null;

            /// <summary>
            /// Gets or sets the color.
            /// </summary>
            public Color? Color { get; set; } = null;
        }
    }
}