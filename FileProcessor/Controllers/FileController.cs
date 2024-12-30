using Microsoft.AspNetCore.Mvc;
using FileProcessor.Entities;
using MediaInfo;
using FileProcessor.Models;
using System.IO;
using System;
using FileProcessor.Configuration;
using Microsoft.Extensions.Options;
using Box.Sdk.Gen;
using Box.Sdk.Gen.Schemas;
using FileProcessor.Interfaces;


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

        public FileController(ILogger<FileController> logger, Interfaces.IFileExtraction fileExtractor, IBoxService boxService)
        {
            _logger = logger;
            _fileExtractor = fileExtractor;
            _boxService = boxService;
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

                _logger.LogDebug($"Duration: {metadata.Duration}");

                if (metadata.Duration == TimeSpan.Zero)
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

        [HttpPost("boxprocess/{fileId:long}")]
        public async Task<IActionResult> ProcessFromBox(long fileId)
        {
            try
            {
                //Authenticate with Box and get the file

                var auth = new BoxDeveloperTokenAuth(token: "TTkOZ9h6Nh5JDagr5kiSvrcLoIXfl4U8");
                var client = new BoxClient(auth: auth);

                var duration = TimeSpan.Zero;

                var listofMetadata = new List<FileMetaData>();

                FileFull file = await client.Files.GetFileByIdAsync(fileId.ToString());

                //Navigate up the file system to the customer
                var fileHierarchy = _boxService.GetFullPathAsync(file);


                System.IO.Stream download = null;
                try
                {
                    download = await client.Downloads.DownloadFileAsync(file.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error downloading file.");
                    return StatusCode(500, "Error downloading file.");
                }

                // Ensure the download is complete and pass the stream to MediaInfoWrapper 
                string tempPath = null;
                try
                {
                    tempPath = Path.Combine(Path.GetTempPath(), file.Name);
                    using (var filestream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        await download.CopyToAsync(filestream);
                    }
                    _logger.LogInformation($"File Downloaded and saved to temp location");
                    var mediaInfoWrapper = new MediaInfoWrapper(tempPath, _logger);
                    _logger.LogDebug($"Duration: {mediaInfoWrapper.Duration}");

                    //set it for distribution!
                    duration = TimeSpan.FromMilliseconds(mediaInfoWrapper.Duration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file with MediaInfoWrapper.");
                    return StatusCode(500, "Error processing file.");
                }
                finally
                {
                    // Ensure the temporary file is deleted
                    if (tempPath != null && System.IO.File.Exists(tempPath))
                    {
                        try
                        {
                            System.IO.File.Delete(tempPath);
                            _logger.LogDebug($"Temporary file deleted: {tempPath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting temporary file.");
                        }
                    }
                }

                var crap = new FileMetaData
                {
                    ClientEmail = file.CreatedBy?.Id ?? "N/A",
                    FileName = file.Name,
                    Description = file.Description,
                    Duration = duration,
                    Extension = file.Extension,
                    ExtractedDate = DateTime.Now,
                    UploadDate = DateTime.Now,
                    UploadedBy = file.UploaderDisplayName,
                    FolderResponseType = "what"
                };
                listofMetadata.Add(crap);


                return Ok(listofMetadata);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file.");
                return StatusCode(500, "Internal server error.");


            }

        }
    }

}

