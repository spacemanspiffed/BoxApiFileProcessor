using FileProcessor.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessor.Controllers
{



    [Route("api/[controller]")]
    [ApiController]
    public class SheetController : ControllerBase
    {
        private readonly ILogger<SheetController> _logger;
        private readonly IGoogleSheetsService _googleSheetsService;
        public SheetController(IGoogleSheetsService sheetsService, ILogger<SheetController> logger)
        {
            _logger = logger;
            _googleSheetsService = sheetsService;
        }


        [HttpGet("test-google-sheets")]
        public async Task<IActionResult> TestGoogleSheets()
        {
            try
            {
                string spreadsheetId = "your-spreadsheet-id"; // Replace with your Google Sheet ID
                await _googleSheetsService.TestConnectionAsync("1T7AOVHRoNN5SCs60oRLQZUcyv7dK7ZRHAkYu6-uGe64");
                return Ok("Successfully connected to Google Sheets.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Sheets connection test failed.");
                return StatusCode(500, "Failed to connect to Google Sheets.");
            }
        }

        [HttpPost("ExpireCache")]
        public async Task<IActionResult> ExpireCustomerCache()
        {
            try
            {
                await _googleSheetsService.ExpireCache();
                return Ok("Cache expired successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring cache.");
                return StatusCode(500, "Error expiring cache.");
            }
        }

        [HttpGet("IgnoredTypes")]
        public async Task<IActionResult> GetIgnoredFileTypes()
        {
            try
            {
                var ignoredTypes = await _googleSheetsService.GetIgnoredFileTypes();
                return Ok(ignoredTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ignored file types.");
                return StatusCode(500, "Error getting ignored file types.");
            }
        }

        [HttpGet("UploadedFiles")]
        public async Task<IActionResult> GetUploadedFiles()
        {
            try
            {
                var uploadedFiles = await _googleSheetsService.GetUploadedFiles();
                return Ok(uploadedFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting uploaded files.");
                return StatusCode(500, "Error getting uploaded files.");
            }
        }
    }
}
