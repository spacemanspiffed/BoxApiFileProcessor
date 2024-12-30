using FileProcessor.Entities;

namespace FileProcessor.Interfaces
{
    public interface IFileExtraction
    {
        public FileMetaData ExtractMetadata(string filePath, string fileName);      
        public TimeSpan ExtractFromStream(Stream stream);
        

    }
}
