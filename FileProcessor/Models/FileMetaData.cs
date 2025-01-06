namespace FileProcessor.Models
{
    public class FileMetaData
    {
        public string FileName { get; set; }
        public Hyperlink BoxLocation { get; set; }
        public string Description { get; set; }
        public string Extension { get; set; }
        public string ClientEmail { get; set; }
        public string FolderResponseType { get; set; }
        public TimeSpan Duration { get; set; }
        public double TotalMinutes { get; set; }
        public DateTime UploadDate { get; set; }
        public DateTime ExtractedDate { get; set; }
        public string UploadedBy { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCategory { get; set; }
        public Hyperlink Template { get; set; }
    }
}
