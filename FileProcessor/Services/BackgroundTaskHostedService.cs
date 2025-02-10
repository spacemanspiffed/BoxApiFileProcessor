using FileProcessor.Configuration;
using FileProcessor.Interfaces;
using Microsoft.Extensions.Options;
using System;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor.Services
{
    public class BackgroundTaskHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundTaskHostedService> _logger;
        private readonly GoogleSheetConfig _googleSheetConfig;
        private readonly int _maxRetries = 3;

        public BackgroundTaskHostedService(IBackgroundTaskQueue taskQueue, IServiceProvider serviceProvider, ILogger<BackgroundTaskHostedService> logger, IOptions<GoogleSheetConfig> googleSheetConfig)
        {
            _taskQueue = taskQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _googleSheetConfig = googleSheetConfig.Value;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Task Hosted Service is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var task = await _taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var boxConfig = scope.ServiceProvider.GetRequiredService<BoxConfig>();
                    var googleSheetConfig = scope.ServiceProvider.GetRequiredService<IOptions<GoogleSheetConfig>>();
                    var googleCredentialsConfig = scope.ServiceProvider.GetRequiredService<GoogleSheetsCredentials>();
                    var boxLogger = scope.ServiceProvider.GetRequiredService<ILogger<BoxService>>();
                    var googleLogger = scope.ServiceProvider.GetRequiredService<ILogger<GoogleSheetsService>>();
                    var fileExtractor = scope.ServiceProvider.GetRequiredService<IFileExtraction>();

                    var boxService = new BoxService(boxConfig, boxLogger);
                    var googleSheetsService = new GoogleSheetsService(
                        googleCredentialsConfig,
                        googleLogger,
                        googleSheetConfig
                    );

                    await ProcessFileInBackgroundAsync(task.FileId, boxService, fileExtractor, googleSheetsService, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing task.");
                    
                    // Sleeping and requeue the request
                    var retryDelay = TimeSpan.FromSeconds(10); // Adjust delay as needed
                    _logger.LogWarning($"Delaying {retryDelay.TotalSeconds} seconds before retrying file {task.FileId}. we have retried {task.RetryCounter} times.");
                    await Task.Delay(retryDelay, stoppingToken);


                    if (task.RetryCounter < _maxRetries)
                    {
                        _logger.LogWarning($"Requeuing file {task.FileId} for retry attempt {task.RetryCounter + 1}/{_maxRetries}.");

                        await _taskQueue.QueueBackgroundWorkItemAsync(new Models.FileProcessingTask
                        {
                            FileId = task.FileId,
                            RetryCounter = task.RetryCounter + 1
                        });
                    }
                    else
                    {
                        _logger.LogCritical(ex, $"Max retries reached for file {task.FileId}. File will not be processed.");
                    }                    
                }
            }
        }
        private async Task ProcessFileInBackgroundAsync(string fileId, BoxService boxService, IFileExtraction fileExtractor, GoogleSheetsService googleSheetsService, CancellationToken token)
        {
            try
            {
                var fileDetails = await boxService.GetFileByIdAsync(fileId);

                if (fileDetails != null)
                {
                    _logger.LogInformation("Processing file: {FileName}", fileDetails.Name);

                    var fileTypesToIgnore = await googleSheetsService.GetIgnoredFileTypes();                                      
                    
                    
                    if (fileTypesToIgnore != null && fileDetails.Extension != null)
                    {
                        if (fileTypesToIgnore.Any(type => type.Equals(fileDetails.Extension, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogWarning($"This file type is in the ignore list and should be skipped. https://dittotranscripts.app.box.com/file/{fileId}");
                            return;
                        }
                    }

                    //looks like the webhook might not include the extension in some cases!!!
                    if (fileTypesToIgnore != null && fileDetails.Extension == null && fileDetails.Name != null)
                    {
                        var extension = GetExtensionFromFileName(fileDetails.Name);
                        if (fileTypesToIgnore.Any(type => type.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogWarning($"This file type is in the ignore list and should be skipped. https://dittotranscripts.app.box.com/file/{fileId}");
                            return;
                        }

                    }

                    var fullPath = await boxService.GetFullPathAsync(fileDetails);

                    if (fullPath == null || !fullPath.Any())
                    {
                        _logger.LogWarning($"The Full Path for {fileId} is null or empty.  Please check the file in Box.com at https://dittotranscripts.app.box.com/file/{fileId}");
                        return;
                    }

                    var ignoreKeywords = new List<string> { "Archive", "Completed Transcripts" };

                    if (fullPath.Any(path =>
                        !string.IsNullOrWhiteSpace(path) &&
                        ignoreKeywords.Any(keyword =>
                            path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        _logger.LogWarning($"This file was uploaded to an archive folder or a completed transcripts folder and should be ignored. https://dittotranscripts.app.box.com/file/{fileId}");
                        return;
                    }
                    var uploadedFiles = await googleSheetsService.GetUploadedFiles();
                    if (uploadedFiles != null && uploadedFiles.Contains(fileId))
                    {
                        _logger.LogWarning($"This file has already been processed and should be skipped. https://dittotranscripts.app.box.com/file/{fileId}");
                        return; 
                    }
                    
                    var fileMetaData = await fileExtractor.CreateFileMetaData(fileDetails, fullPath);

                    await googleSheetsService.AppendToSheetAsync(_googleSheetConfig.SheetId, fileMetaData);

                    _logger.LogInformation("File processing completed for: {FileName}", fileDetails.Name);
                }
                else
                {
                    _logger.LogWarning("File details not found for file ID: {FileId}", fileId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file in background.");
            }
        }

        private string GetExtensionFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }
            var lastDotIndex = fileName.LastIndexOf('.');
            if (lastDotIndex < 0)
            {
                return string.Empty;
            }
            return fileName.Substring(lastDotIndex);
        }
    }
}