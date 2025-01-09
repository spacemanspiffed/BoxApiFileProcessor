using FileProcessor.Configuration;
using FileProcessor.Interfaces;
using Microsoft.Extensions.Options;
using System;
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

                    var fullPath = await boxService.GetFullPathAsync(fileDetails);
                    
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
    }
}