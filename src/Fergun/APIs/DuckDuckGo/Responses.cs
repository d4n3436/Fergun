using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.DuckDuckGo
{
    public class DdgResponse
    {
        [JsonProperty("next")]
        public string Next { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("vqd")]
        public dynamic Vqd { get; set; } // idk how to deserialize this

        [JsonProperty("response_type")]
        public string ResponseType { get; set; }

        [JsonProperty("ads")]
        public string Ads { get; set; }

        [JsonProperty("results")]
        public List<Result> Results { get; set; }

        [JsonProperty("queryEncoded")]
        public string QueryEncoded { get; set; }
    }

    public class Result
    {
        [JsonProperty("width")]
        public long Width { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }
}