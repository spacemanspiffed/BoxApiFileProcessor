namespace FileProcessor.Models
{
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
