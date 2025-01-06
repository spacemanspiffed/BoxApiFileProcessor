namespace FileProcessor.Configuration
{
    public class BoxConfig
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string JwtKeyId { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string Passphrase { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DeveloperToken { get; set; }
    }
}
