using Newtonsoft.Json;

namespace FileProcessor.Models
{
    public class BoxWebhookEvent
    {
        [JsonProperty("trigger")]
        public string Trigger { get; set; }

        [JsonProperty("source")]
        public BoxWebhookSource Source { get; set; }
    }

    public class BoxWebhookSource
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
