namespace FileProcessor.Models
{
    public class ClientTemplate
    {
        public string ClientName { get; set; }
        public Hyperlink Template { get; set; }  
    }

    public class Hyperlink
    {
        public string Text { get; set; } // Display text of the hyperlink
        public string Url { get; set; }  // URL of the hyperlink

        public override string ToString()
        {
            return !string.IsNullOrEmpty(Url) ? $"{Text} ({Url})" : Text;
        }
    }
}
