using Newtonsoft.Json;

namespace Fergun.APIs.WaybackMachine
{
    public class WaybackResponse
    {
        [JsonProperty("archived_snapshots")]
        public ArchivedSnapshots ArchivedSnapshots { get; private set; }
    }

    public class ArchivedSnapshots
    {
        [JsonProperty("closest", NullValueHandling = NullValueHandling.Ignore)]
        public Snapshot Closest { get; private set; }
    }

    public class Snapshot
    {
        [JsonProperty("available")]
        public bool Available { get; private set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; private set; }

        [JsonProperty("status")]
        public string Status { get; private set; }
    }
}