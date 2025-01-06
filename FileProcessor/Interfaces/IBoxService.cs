﻿namespace FileProcessor.Interfaces
{
    using Box.Sdk.Gen;
    using Box.Sdk.Gen.Schemas;

    public interface IBoxService
    {
        Task<FileFull> GetFileByIdAsync(string fileId); 
        Task<Stream> DownloadFileAsync(string fileId);
        Task<List<string>> GetFullPathAsync(FileFull boxFileInfo);
        Task<UserFull> TestConnectionAsync();
        Task<FileFull> CreateSharedLinkAsync(FileFull file);
    }
}
