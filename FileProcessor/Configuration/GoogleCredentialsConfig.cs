using System.Text.Json;
using Newtonsoft.Json;

namespace FileProcessor.Configuration
{
    public class GoogleCredentialsConfig
    {
        [JsonProperty("universe_domain")]
        public string UniverseDomain { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("audience")]
        public string Audience { get; set; }

        [JsonProperty("subject_token_type")]
        public string SubjectTokenType { get; set; }

        [JsonProperty("token_url")]
        public string TokenUrl { get; set; }

        [JsonProperty("service_account_impersonation_url")]
        public string ServiceAccountImpersonationUrl { get; set; }

        [JsonProperty("credential_source")]
        public CredentialSourceConfig CredentialSource { get; set; }
    }

    public class CredentialSourceConfig
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("format")]
        public CredentialSourceFormat Format { get; set; }
    }

    public class CredentialSourceFormat
    {
        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
