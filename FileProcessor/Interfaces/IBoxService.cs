namespace FileProcessor.Interfaces
{
    using Box.Sdk.Gen;
    using Box.Sdk.Gen.Schemas;

    public interface IBoxService
    {
        Task<FileFull> GetFileByIdAsync(string fileId); 
        Task<Stream> DownloadFileAsync(string fileId);
        Task<string> GetFullPathAsync(FileFull boxFileInfo);
    }
}
