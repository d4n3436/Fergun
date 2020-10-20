using System;
using Newtonsoft.Json;

namespace Fergun.Responses
{
    public class WikiArticle
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("displaytitle")]
        public string Displaytitle { get; set; }

        [JsonProperty("namespace")]
        public NamespaceData Namespace { get; set; }

        [JsonProperty("wikibase_item")]
        public string WikibaseItem { get; set; }

        [JsonProperty("titles")]
        public Titles Titles { get; set; }

        [JsonProperty("pageid")]
        public long Pageid { get; set; }

        [JsonProperty("thumbnail")]
        public ImageData Thumbnail { get; set; }

        [JsonProperty("originalimage")]
        public ImageData Originalimage { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }

        [JsonProperty("dir")]
        public string Dir { get; set; }

        [JsonProperty("revision")]
        public string Revision { get; set; }

        [JsonProperty("tid")]
        public Guid Tid { get; set; }

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("description_source")]
        public string DescriptionSource { get; set; }

        [JsonProperty("content_urls")]
        public ContentUrls ContentUrls { get; set; }

        [JsonProperty("api_urls")]
        public ApiUrls ApiUrls { get; set; }

        [JsonProperty("extract")]
        public string Extract { get; set; }

        [JsonProperty("extract_html")]
        public string ExtractHtml { get; set; }
    }

    public class ApiUrls
    {
        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("metadata")]
        public string Metadata { get; set; }

        [JsonProperty("references")]
        public string References { get; set; }

        [JsonProperty("media")]
        public string Media { get; set; }

        [JsonProperty("edit_html")]
        public string EditHtml { get; set; }

        [JsonProperty("talk_page_html")]
        public string TalkPageHtml { get; set; }
    }

    public class ContentUrls
    {
        [JsonProperty("desktop")]
        public Urls Desktop { get; set; }

        [JsonProperty("mobile")]
        public Urls Mobile { get; set; }
    }

    public class Urls
    {
        [JsonProperty("page")]
        public string Page { get; set; }

        [JsonProperty("revisions")]
        public string Revisions { get; set; }

        [JsonProperty("edit")]
        public string Edit { get; set; }

        [JsonProperty("talk")]
        public string Talk { get; set; }
    }

    public class NamespaceData
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class ImageData
    {
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("width")]
        public long Width { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }
    }

    public class Titles
    {
        [JsonProperty("canonical")]
        public string Canonical { get; set; }

        [JsonProperty("normalized")]
        public string Normalized { get; set; }

        [JsonProperty("display")]
        public string Display { get; set; }
    }
}
