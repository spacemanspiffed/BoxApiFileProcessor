namespace FileProcessor.Models
{
    public class Webhook
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Address { get; set; }
        public DateTime CreatedAt { get; set; }
        public CreatedBy CreatedBy { get; set; }
        public Target Target { get; set; }
        public string[] Triggers { get; set; }

    }

    public class CreatedBy
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
    public class Target
    {
        public string Id { get; set; }
        public string Type { get; set; }       
    }
}
