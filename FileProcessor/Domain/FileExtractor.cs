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
            if (matchedClient == null)
            {
                matchedClient = file.CreatedBy.Id;
            }

            var mapping = new List<string>()
            {
             "1-2 Business Days",
             "3-5 Business Days",
             "6-10 Business Days"
            };
            // Match TATType from the full path

            var firstMatch = fullPath.FirstOrDefault(mapping.Contains);

            //This is backwards            

            var folderResponseType = firstMatch != null && Enum.TryParse<TATType>(firstMatch, out var tatType)
                ? tatType switch
                {
                    TATType.Rushed => "1-2 Business Days",
                    TATType.Standard => "3-5 Business Days",
                    TATType.Extended => "6-10 Business Days",
                    _ => "Unknown"
                }
                : "Unknown";

            var ClientTypeMapping = new List<string>()
            {
             "Legal Clients",
             "Law Enforcement Clients",
             "Website Uploads"
            };           


            // Download file to process duration
            var downloadStream = await _boxService.DownloadFileAsync(file.Id);
            var duration = await ProcessFileWithMediaInfo(file, downloadStream);

            var linkUrl = $"https://app.box.com/file/{file.Id}";

            return new JobLog
            {
                Client = matchedClient,
                Template = hyperLinkToClientTemplate,
                Category = "Law Enforcement",
                FileName = file.Name,
                FileLink = linkUrl,
                DateReceived = TimeZoneInfo.ConvertTime(file.CreatedAt?.UtcDateTime ?? DateTime.UtcNow,
                                           TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time")),
                FileLength = duration,
                Minutes = duration.TotalMinutes,
                TAT = folderResponseType,
                NotesAndComments = file.Description
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
    }
}
