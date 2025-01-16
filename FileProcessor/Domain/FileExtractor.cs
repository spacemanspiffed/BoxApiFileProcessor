using System.ComponentModel;
using System.Linq;
using Box.Sdk.Gen.Schemas;
using FileProcessor.Controllers;
using FileProcessor.Interfaces;
using FileProcessor.Models;
using FileProcessor.Services;
using MediaInfo;

namespace FileProcessor.Domain
{
    public class FileExtractor : IFileExtraction
    {
        private readonly ILogger<FileExtractor> _logger;
        private readonly IBoxService _boxService;
        public FileExtractor(ILogger<FileExtractor> logger, IBoxService boxService)
        {
            _logger = logger;
            _boxService = boxService;
        }

        public async Task<JobLog> CreateFileMetaData(FileFull file, List<string> fullPath)
        {
            // Match client name and template from a folder in the full path
            var matchedClient = GoogleSheetsService.GetAllClients()
                .FirstOrDefault(client => fullPath.Contains(client, StringComparer.OrdinalIgnoreCase));

            var hyperLinkToClientTemplate = matchedClient != null ? GoogleSheetsService.GetTemplateForClient(matchedClient) : null;

            //Per Alec -- If the client name is null we can go with the email address of the uploader
            //if (matchedClient == null)
            //{
            //    matchedClient = file.CreatedBy.Login;
            //}

            var folderResponseType = GetFolderResponseType(fullPath);           

            var category = GetCategory(fullPath);          

            // Download file to process duration
            var downloadStream = await _boxService.DownloadFileAsync(file.Id);
            var duration = await ProcessFileWithMediaInfo(file, downloadStream);

            var linkUrl = $"https://app.box.com/file/{file.Id}";

            return new JobLog
            {
                Client = matchedClient ?? "",
                UploadedBy = file.CreatedBy.Login,
                Category = category,
                FileName = file.Name,
                FileLink = linkUrl,
                DateReceived = TimeZoneInfo.ConvertTime(file.CreatedAt?.UtcDateTime ?? DateTime.UtcNow,
                                           TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time")),
                FileLength = duration,                
                TAT = folderResponseType,
                NotesAndComments = file.Description,
                FileId = file.Id
            };

        }        

        private async Task<TimeSpan> ProcessFileWithMediaInfo(FileFull file, System.IO.Stream downloadStream)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), file.Name);
            try
            {
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }

                _logger.LogInformation("File downloaded and saved to temporary location.");

                var mediaInfoWrapper = new MediaInfoWrapper(tempPath, _logger);
                _logger.LogDebug($"Duration: {mediaInfoWrapper.Duration}");

                return TimeSpan.FromMilliseconds(mediaInfoWrapper.Duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file with MediaInfoWrapper.");
                throw;
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        //Remove that file!!!!
                        System.IO.File.Delete(tempPath);
                        _logger.LogDebug($"Temporary file deleted: {tempPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting temporary file.");
                    }
                }
            }
        }

        JobLog IFileExtraction.ExtractMetadata(string filePath, string fileName)
        {
            throw new NotImplementedException();
        }

        private string GetCategory(List<string> fullPath)
        {
            var ClientTypeMapping = new List<string>()
            {
             "Legal Clients",
             "Law Enforcement Clients",
             "Website Uploads",
             "General Clients",
             "Medical Clients",
             "Acedemic Clients",
             "Financial Clients",
             "Unknown",
            };

            var matchedClientType = fullPath.FirstOrDefault(ClientTypeMapping.Contains);

            if (matchedClientType == null)
            {
                matchedClientType = "Unknown";
            }

            var category = "General";
            switch (matchedClientType)
            {
                case "Legal Clients":
                    category = "Legal";
                    break;
                case "Law Enforcement Clients":
                    category = "Law Enforcement";
                    break;
                case "Medical Clients":
                    category = "Medical";
                    break;
                case "General Clients":
                    category = "General";
                    break;
                case "Acedemic Clients":
                    category = "General";
                    break;
                case "Financial Clients":
                    category = "General";
                    break;
                case "Website Uploads":
                    category = "General";
                    break;
                default:
                    category = "General";
                    break;
            }
            return category;
        }
        private string GetFolderResponseType(List<string> fullPath)
        {
            var serviceResponseMapping = new List<string>()
            {
             "1-2 Business Days",
             "3-5 Business Days",
             "6-10 Business Days"
            };
            // Match TATType from the full path

            var firstMatch = fullPath.FirstOrDefault(serviceResponseMapping.Contains);

            //This is backwards            
            var folderResponseType = "Standard";
            switch (firstMatch)
            {
                case "1-2 Business Days":
                    folderResponseType = "Rushed";
                    break;
                case "3-5 Business Days":
                    folderResponseType = "Standard";
                    break;
                case "6-10 Business Days":
                    folderResponseType = "Extended";
                    break;
                default:
                    folderResponseType = "Standard";
                    break;
            }
            return folderResponseType;
        }
    }
}
