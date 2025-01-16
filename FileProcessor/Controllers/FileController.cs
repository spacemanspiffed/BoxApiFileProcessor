using Microsoft.AspNetCore.Mvc;
using MediaInfo;
using FileProcessor.Models;
using System.IO;
using System;
using FileProcessor.Configuration;
using Microsoft.Extensions.Options;
using Box.Sdk.Gen;
using Box.Sdk.Gen.Schemas;
using FileProcessor.Interfaces;
using FileProcessor.Services;
using Org.BouncyCastle.Crypto.Asymmetric;
using Newtonsoft.Json;
using FileProcessor.Domain;


namespace FileProcessor.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;
        private readonly Interfaces.IFileExtraction _fileExtractor;
        private readonly BoxConfig _boxConfig;
        private readonly IBoxService _boxService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly GoogleSheetConfig _googleSheetConfig;
        private readonly IBackgroundTaskQueue _taskQueue;

        public FileController(ILogger<FileController> logger, Interfaces.IFileExtraction fileExtractor, IBoxService boxService, IGoogleSheetsService googleSheetsService, IBackgroundTaskQueue taskQueue, IOptions<GoogleSheetConfig> config)
        {
            _logger = logger;
            _fileExtractor = fileExtractor;
            _boxService = boxService;
            _googleSheetsService = googleSheetsService;
            _taskQueue = taskQueue;
            _googleSheetConfig = config.Value;
        }

        [HttpGet("username")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var user = await _boxService.TestConnectionAsync();
                return Ok($"Successfully connected to Box. User = {user.Name}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error connecting to Box: {ex.Message}");
            }
        }


        [HttpPost("simple")]
        public IActionResult SimpleTest()
        {
            _logger.LogInformation("Simple test endpoint hit.");
            return Ok("Simple test endpoint hit.");
        }
        [HttpPost("create-web-hook")]
        public async Task<IActionResult> CreateWebHook(string folderId)
        {
            try
            {
                var webhook = await _boxService.CreateWebHookAsync(folderId);
                return Ok($"Successfully created webhook. Id = {webhook.Id}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating webhook: {ex.Message}");
            }
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessFile([FromBody] FilePathRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                _logger.LogWarning("No file path provided.");
                return BadRequest("No file path provided.");
            }
            try
            {
                string fileName = Path.GetFileName(request.FilePath);
                if (!System.IO.File.Exists(request.FilePath))
                {
                    _logger.LogWarning("File not found at path: {filePath}", request.FilePath);
                    return BadRequest("File not found.");
                }

                _logger.LogDebug($"Processing file at path: {request.FilePath}");


                var metadata = _fileExtractor.ExtractMetadata(request.FilePath, fileName);

                _logger.LogDebug($"Duration: {metadata.FileLength}");

                if (metadata.FileLength == TimeSpan.Zero)
                {
                    return BadRequest("Failed to extract duration from media file.");
                }

                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost("boxprocess/{fileId}")]
        public async Task<IActionResult> ProcessFromBox(string fileId)
        {

            try
            {
                var duration = TimeSpan.Zero;

                //FileFull file = await _boxService.GetFileByIdAsync(fileId);

                //var folder = await _boxService.GetFolderByIdAsync(file.Parent.Id);

                //var fileVersions = await _boxService.GetFileVersionsAsync(fileId);

                //var fileVersion = await _boxService.GetFileVersionAsync(fileId, file.FileVersion.Id);


                var fileDetails = await _boxService.GetFileByIdAsync(fileId);

                if (fileDetails != null)
                {
                    _logger.LogInformation("Processing file: {FileName}", fileDetails.Name);

                    var fileTypesToIgnore = await _googleSheetsService.GetIgnoredFileTypes();

                    if (fileTypesToIgnore != null && fileDetails.Extension != null)
                    {
                        if (fileTypesToIgnore.IndexOf(fileDetails.Extension, (int)StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _logger.LogWarning($"This file type is in the ignore list and should be skipped. https://dittotranscripts.app.box.com/file/{fileId}");
                            return BadRequest("This file type is ignored");
                        }
                    }

                    var fullPath = await _boxService.GetFullPathAsync(fileDetails);

                    if (fullPath == null || !fullPath.Any())
                    {
                        _logger.LogWarning($"The Full Path for {fileId} is null or empty.  Please check the file in Box.com at https://dittotranscripts.app.box.com/file/{fileId}");
                        return BadRequest("FilePath is null for this file");
                    }

                    var ignoreKeywords = new List<string> { "Archive", "Completed Transcripts" };

                    if (fullPath.Any(path =>
                        !string.IsNullOrWhiteSpace(path) &&
                        ignoreKeywords.Any(keyword =>
                            path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        _logger.LogWarning($"This file was uploaded to an archive folder or a completed transcripts folder and should be ignored. https://dittotranscripts.app.box.com/file/{fileId}");
                        return BadRequest("This file is in an archive or completed transcipts folder");
                    }
                    var uploadedFiles = await _googleSheetsService.GetUploadedFiles();
                    if (uploadedFiles != null && uploadedFiles.Contains(fileId))
                    {
                        _logger.LogWarning($"This file has already been processed and should be skipped. https://dittotranscripts.app.box.com/file/{fileId}");
                        return BadRequest($"This file has already been processed and should be skipped. https://dittotranscripts.app.box.com/file/{fileId}");
                    }

                    var fileMetaData = await _fileExtractor.CreateFileMetaData(fileDetails, fullPath);

                    await _googleSheetsService.AppendToSheetAsync(_googleSheetConfig.SheetId, fileMetaData);

                    _logger.LogInformation("File processing completed for: {FileName}", fileDetails.Name);

                    return Ok(fileMetaData);
                }
                else
                {
                    return BadRequest("File not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file.");
                return StatusCode(500, "Internal server error.");
            }

        }

        [HttpPost("CreateWebhook")]
        public async Task<IActionResult> CreateWebhook(string folderId)
        {
            try
            {
                var folder = await _boxService.GetFolderByIdAsync(folderId);
                if (folder == null)
                {
                    return BadRequest("Folder not found.");
                }

                if (await _boxService.WebhookForFolderExists(folderId))
                {
                    return BadRequest("Webhook already exists for this folder.");
                }
                var webhook = await _boxService.CreateWebHookAsync(folderId);
                return Ok($"Successfully created webhook. Id = {webhook.Id}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating webhook: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the Box Webhook events.
        /// </summary>
        /// <returns>A 200 OK response if successfully processed.</returns>
        [HttpPost("webhook")] // Endpoint: /api/File/webhook
        public async Task<IActionResult> HandleBoxWebhook()
        {
            try
            {
                // Read the request body
                using var reader = new StreamReader(Request.Body);
                var payload = await reader.ReadToEndAsync();

                _logger.LogInformation("Received webhook payload: {Payload}", payload);

                // Deserialize the payload
                var webhookEvent = JsonConvert.DeserializeObject<BoxWebhookEvent>(payload);

                if (webhookEvent == null || webhookEvent.Source == null)
                {
                    _logger.LogWarning("Invalid webhook payload received.");
                    return BadRequest("Invalid payload. Please go to Box.com and check on webhook delivery!");
                }

                // Check if the event type is FILE.UPLOADED
                if (webhookEvent.Trigger.Equals("FILE.UPLOADED", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Enqueuing processing task for file ID: {FileId}", webhookEvent.Source.Id);

                    // Enqueue the task
                    await _taskQueue.QueueBackgroundWorkItemAsync(new FileProcessingTask
                    {
                        FileId = webhookEvent.Source.Id
                    });

                    return Ok("Processing started");
                }
                else
                {
                    _logger.LogInformation("Unhandled webhook event type: {EventType}", webhookEvent.Trigger);
                    return Ok("Event ignored");
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Box webhook.");
                return StatusCode(500, "Internal server error.");
            }
        }
    }

}


