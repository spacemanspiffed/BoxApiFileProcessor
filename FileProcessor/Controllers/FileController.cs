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


namespace FileProcessor.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;
        private readonly Interfaces.IFileExtraction _fileExtractor;
        //private readonly BoxConfig _boxConfig;
        private readonly IBoxService _boxService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly GoogleSheetConfig _googleSheetConfig;

        public FileController(ILogger<FileController> logger, Interfaces.IFileExtraction fileExtractor, IBoxService boxService, IGoogleSheetsService googleSheetsService, IOptions<GoogleSheetConfig> config)
        {
            _logger = logger;
            _fileExtractor = fileExtractor;
            _boxService = boxService;
            _googleSheetsService = googleSheetsService;
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

                FileFull file = await _boxService.GetFileByIdAsync(fileId);

                if (file == null)
                {
                    return NotFound($"File was not found for this Id -- {fileId} --");
                }
                //if (file.SharedLink == null)
                //{
                //    file = await _boxService.CreateSharedLinkAsync(file);
                //}

                var fullPath = await _boxService.GetFullPathAsync(file);

                var fileMetaData = await _fileExtractor.CreateFileMetaData(file, fullPath);

                await _googleSheetsService.AppendToSheetAsync(spreadsheetId: _googleSheetConfig.SheetId, jobLog: fileMetaData);

                return Ok(fileMetaData);

            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file.");
                return StatusCode(500, "Internal server error.");


            }

        }      
        
    }

}

