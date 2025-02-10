namespace FileProcessor.Models
{
    public class FileProcessingTask
    {
        public string FileId { get; set; }
        public int RetryCounter { get; set; }
    }
}
