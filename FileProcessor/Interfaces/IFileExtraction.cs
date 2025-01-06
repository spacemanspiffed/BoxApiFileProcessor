using Box.Sdk.Gen.Schemas;
using FileProcessor.Models;

namespace FileProcessor.Interfaces
{
    public interface IFileExtraction
    {
        public JobLog ExtractMetadata(string filePath, string fileName);    
        
        public Task<JobLog> CreateFileMetaData(FileFull file, List<string> fullPath);

    }
}
