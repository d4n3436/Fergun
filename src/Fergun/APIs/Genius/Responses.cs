using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.Genius
{
    public class GeniusResponse
    {
        [JsonProperty("meta")]
        public Meta Meta { get; set; }

        [JsonProperty("response")]
        public Response Response { get; set; }
    }

    public class Meta
    {
        [JsonProperty("status")]
        public long Status { get; set; }
    }

    public class Response
    {
        [JsonProperty("hits")]
        public List<Hit> Hits { get; set; }
    }

    public class Hit
    {
        [JsonProperty("highlights")]
        public List<object> Highlights { get; set; }

        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("result")]
        public Result Result { get; set; }
    }

    public class Result
    {
        [JsonProperty("annotation_count")]
        public long AnnotationCount { get; set; }

        [JsonProperty("api_path")]
        public string ApiPath { get; set; }

        [JsonProperty("full_title")]
        public string FullTitle { get; set; }

        [JsonProperty("header_image_thumbnail_url")]
        public Uri HeaderImageThumbnailUrl { get; set; }

        [JsonProperty("header_image_url")]
        public Uri HeaderImageUrl { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("lyrics_owner_id")]
        public long LyricsOwnerId { get; set; }

        [JsonProperty("lyrics_state")]
        public string LyricsState { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("pyongs_count")]
        public long? PyongsCount { get; set; }

        [JsonProperty("song_art_image_thumbnail_url")]
        public Uri SongArtImageThumbnailUrl { get; set; }

        [JsonProperty("song_art_image_url")]
        public Uri SongArtImageUrl { get; set; }

        [JsonProperty("stats")]
        public Stats Stats { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("title_with_featured")]
        public string TitleWithFeatured { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("primary_artist")]
        public PrimaryArtist PrimaryArtist { get; set; }
    }

    public class PrimaryArtist
    {
        [JsonProperty("api_path")]
        public string ApiPath { get; set; }

        [JsonProperty("header_image_url")]
        public Uri HeaderImageUrl { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("image_url")]
        public Uri ImageUrl { get; set; }

        [JsonProperty("is_meme_verified")]
        public bool IsMemeVerified { get; set; }

        [JsonProperty("is_verified")]
        public bool IsVerified { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("iq", NullValueHandling = NullValueHandling.Ignore)]
        public long? Iq { get; set; }
    }

    public class Stats
    {
        [JsonProperty("unreviewed_annotations")]
        public long UnreviewedAnnotations { get; set; }

        [JsonProperty("concurrents", NullValueHandling = NullValueHandling.Ignore)]
        public long? Concurrents { get; set; }

        [JsonProperty("hot")]
        public bool Hot { get; set; }

        [JsonProperty("pageviews", NullValueHandling = NullValueHandling.Ignore)]
        public long? Pageviews { get; set; }
    }
}