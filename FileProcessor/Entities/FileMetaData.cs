namespace FileProcessor.Entities
{
    public class FileMetaData
    {
        public string FileName { get; set; }
        public string Description { get; set; }
        public string Extension { get; set; }
        public string ClientEmail { get; set; }
        public string FolderResponseType { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime UploadDate { get; set; }
        public DateTime ExtractedDate { get; set; }
        public string UploadedBy { get; set; }        
    }
}
