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
    }
}
