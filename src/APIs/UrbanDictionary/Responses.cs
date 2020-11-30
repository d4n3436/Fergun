using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.UrbanDictionary
{
    public class UrbanResponse
    {
        [JsonProperty("list")]
        public List<DefinitionInfo> Definitions { get; set; }
    }

    public class DefinitionInfo
    {
        [JsonProperty("definition")]
        public string Definition { get; set; }

        [JsonProperty("permalink")]
        public string Permalink { get; set; }

        [JsonProperty("thumbs_up")]
        public int ThumbsUp { get; set; }

        [JsonProperty("sound_urls", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> SoundUrls { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("word")]
        public string Word { get; set; }

        [JsonProperty("defid")]
        public int DefinitionId { get; set; }

        [JsonProperty("current_vote")]
        public string CurrentVote { get; set; }

        [JsonProperty("written_on")]
        public DateTimeOffset WrittenOn { get; set; }

        [JsonProperty("example")]
        public string Example { get; set; }

        [JsonProperty("thumbs_down")]
        public int ThumbsDown { get; set; }
    }
}