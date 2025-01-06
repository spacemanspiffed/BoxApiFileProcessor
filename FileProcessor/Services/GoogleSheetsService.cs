using System;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using FileProcessor.Configuration;
using FileProcessor.Interfaces;
using FileProcessor.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using static Org.BouncyCastle.Utilities.Test.FixedSecureRandom;

namespace FileProcessor.Services
{
    public enum TATType
    {
        Rushed,
        Standard,
        Extended
    }    
    public class GoogleSheetsService : IGoogleSheetsService
    {


        private readonly string _serviceAccountKeyPath = "C:\\code\\Ditto\\resources\\dittointakesheet-key.json";
        private SheetsService _sheetsService;

        public static List<ClientTemplate> _templateCache = new List<ClientTemplate>();
        public static List<string> _clientListCache = new List<string>();
        private readonly ILogger<GoogleSheetsService> _logger;
        private readonly GoogleCredentialsConfig _config;
        private readonly GoogleSheetConfig _sheetConfig;

        public GoogleSheetsService(GoogleCredentialsConfig googleCredentialsConfig, ILogger<GoogleSheetsService> logger, IOptions<GoogleSheetConfig> sheetConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = googleCredentialsConfig ?? throw new ArgumentNullException(nameof(googleCredentialsConfig));

            //_logger.LogInformation($"UniverseDomain: {_config.UniverseDomain}");
            //_logger.LogInformation($"Credential File Path: {_config.CredentialSource.File}");
            //_logger.LogInformation($"Token URL: {_config.TokenUrl}");

            //_logger.LogInformation($"ServiceAccountImpersonationUrl: {_config.ServiceAccountImpersonationUrl}");
            //_logger.LogInformation($"Audience: {_config.Audience}");
            //_logger.LogInformation($"Credential Source File: {_config.CredentialSource.File}");

            _sheetConfig = sheetConfig.Value;

            InitializeGoogleSheetService().Wait();

        }

        private async Task InitializeGoogleSheetService()
        {

            try
            {
                using (var stream = new FileStream(_serviceAccountKeyPath, FileMode.Open, FileAccess.Read))
                {
                    var credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(SheetsService.Scope.Spreadsheets);
                    _sheetsService = new SheetsService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "FileProcessor App"
                    });

                }                          

                await CacheTemplatesAsync(_sheetConfig.SheetId, _sheetConfig.Customers);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not auth with the key either.");
                throw;
            }
        }

        public async Task TestConnectionAsync(string spreadsheetId, string range = "A1")
        {
            try
            {
                _logger.LogInformation("Testing Google Sheets connection...");

                var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                var response = await request.ExecuteAsync();

                if (response.Values != null && response.Values.Count > 0)
                {
                    _logger.LogInformation("Successfully connected to Google Sheets. First cell value: " + response.Values[0][0]);
                }
                else
                {
                    _logger.LogInformation("Connected to Google Sheets, but the range is empty.");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Google Sheets connection.");
                throw;
            }
        }

        public async Task<IList<IList<object>>> ReadSheetAsync(string spreadsheetId, string range)
        {
            var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
            ValueRange response = await request.ExecuteAsync();
            return response.Values ?? new List<IList<object>>();
        }

        public async Task AppendToSheetAsync(string spreadsheetId, JobLog jobLog)
        {
            // Define column headers
            var columnHeaders = new List<object>
        {
            "Status", "QA Date", "Client", "Template", "Category", "File Name",
            "File Link", "Date Received", "IC Due Date", "Final Due Date", "Special Due Date",
            "Returned", "Transcriptionist", "Duration", "Minutes", "TAT",
            "Number of Speakers", "Verbatim or Timestamps","TT", "Type", "IC Rate", "IC Total",
            "Rate", "Pricing", "Special Rate", "Special Template", "Feedback",
            "Notes and Comments"
        };



            // Check if headers exist in the sheet
            var headerRange = $"Job Log!1:1"; // First row in the sheet
            var headerRequest = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, headerRange);
            var headerResponse = await headerRequest.ExecuteAsync();
            bool headersExist = headerResponse.Values != null && headerResponse.Values.Any();

            // Prepare the rows to append
            var rowsToAppend = new List<IList<object>>();

            // Add headers if they don't exist
            if (!headersExist)
            {
                rowsToAppend.Add(columnHeaders);
            }
            
            if (columnHeaders.Count != jobLog.ConvertToValues().Count)
            {
                throw new InvalidOperationException("Mismatch between column headers and JobLog values.");
            }

            // Add the JobLog data row
            rowsToAppend.Add(jobLog.ConvertToValues());

            // Append rows to the sheet
            var valueRange = new ValueRange { Values = rowsToAppend };
            var appendRequest = _sheetsService.Spreadsheets.Values.Append(valueRange, spreadsheetId, "Job Log");
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            await appendRequest.ExecuteAsync();
            Console.WriteLine("Data appended successfully.");
        }

        public async Task CacheTemplatesAsync(string spreadsheetId, string range)
        {
            if (_templateCache.Count == 0)
            {
                // Read the data and cache it as a List<ClientTemplate>
                _templateCache = await ReadTemplateSheet(spreadsheetId, range);
            }

            if (_clientListCache.Count == 0)
            {
                // Cache the list of Client names separately
                _clientListCache = _templateCache
                    .Select(template => template.ClientName)
                    .ToList();
            }
        }

        public static Hyperlink GetTemplateForClient(string clientName)
        {
            return _templateCache.Where(x => x.ClientName == clientName)
                .Select(x => x.Template)
                .FirstOrDefault() ?? new Hyperlink();


        }

        public static List<string> GetAllClients()
        {
            return _clientListCache;
        }

        public async Task<List<ClientTemplate>> ReadTemplateSheet(string spreadsheetId, string range)
        {
            _logger.LogInformation($"Looking for this spreadsheet {spreadsheetId}, and this range -- {range}");
            if (string.IsNullOrWhiteSpace(spreadsheetId))
                throw new ArgumentException("Spreadsheet ID cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(range))
                throw new ArgumentException("Range cannot be null or empty.");

            // Log the inputs for debugging
            Console.WriteLine($"Fetching data from SpreadsheetId: {spreadsheetId}, Range: {range}");

            // Set up the request
            // Set up the request         

            // Execute the request

            var request = _sheetsService.Spreadsheets.Get(spreadsheetId);
            request.Ranges = new List<string>() { range }; // Set the range
            request.IncludeGridData = true; // This is essential to get hyperlinks

            var response = await request.ExecuteAsync();
            var clientTemplates = new List<ClientTemplate>();

            // Process the response (which now includes grid data)
            if (response.Sheets != null && response.Sheets.Count > 0)
            {
                var sheet = response.Sheets[0]; // Assuming you only have one sheet in the response

                if (sheet.Data != null && sheet.Data.Count > 0 &&
                    sheet.Data[0].RowData != null)
                {
                    foreach (var row in sheet.Data[0].RowData)
                    {
                        if (row.Values != null && row.Values.Count > 1) // Ensure enough values in the row
                        {
                            var clientName = row.Values[0].FormattedValue ?? "No Client"; // Use FormattedValue
                            var templateCell = row.Values[1]; // Get the cell with the potential hyperlink

                            string hyperlinkUrl = templateCell.Hyperlink ?? ""; // Get the hyperlink directly
                            string hyperlinkText = templateCell.FormattedValue ?? "No Template";

                            var hyperLink = new Hyperlink
                            {
                                Url = hyperlinkUrl,
                                Text = hyperlinkText,
                            };
                            clientTemplates.Add(new ClientTemplate
                            {
                                ClientName = clientName,
                                Template = hyperLink,
                            });
                        }
                    }
                }
            }

            return clientTemplates;

            //var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

            //var response = await request.ExecuteAsync();

            // Process response
            //if (response.Values == null || !response.Values.Any())
            //{
            //    Console.WriteLine("No data found in the specified range.");

            //}
            //foreach (var row in response.Values)
            //{
            //    // Handle rows and map hyperlink text and URLs manually
            //    var clientName = row.ElementAtOrDefault(0)?.ToString() ?? "No Client";
            //    var templateValue = row.ElementAtOrDefault(1)?.ToString() ?? "No Template";


            //    // Attempt to parse hyperlink formula if present
            //    string hyperlinkUrl = ExtractHyperlink(templateValue, out string hyperlinkText);

            //    Console.WriteLine($"Client: {clientName}, Template Text: {hyperlinkText}, URL: {hyperlinkUrl}");




            //return null;
        }

        private string ExtractHyperlink(string cellValue, out string hyperlinkText)
        {
            // Default values
            string hyperlinkUrl = ""; // Define hyperlinkUrl here
            hyperlinkText = cellValue;

            string pattern = @"=HYPERLINK\(\s*(""([^""]+)"")?\s*,\s*(""([^""]+)"")?\s*\)"; // Matches =HYPERLINK("URL", "Text")
            Match match = Regex.Match(cellValue, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                hyperlinkUrl = match.Groups[1].Value;  // Extract the URL
                hyperlinkText = match.Groups[2].Value; // Extract the link text
            }
            else
            {
                // If no HYPERLINK formula, try to detect a plain URL
                pattern = @"^(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-]*)*\/?$";
                match = Regex.Match(cellValue, pattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    hyperlinkUrl = match.Value; // The cell value is the URL
                }
                else
                {
                    hyperlinkUrl = ""; // No URL found
                }
            }

            return hyperlinkUrl;
            //if (string.IsNullOrEmpty(cellValue) || !cellValue.StartsWith("=HYPERLINK("))
            //    return null;

            //try
            //{
            //    // Parse formula: =HYPERLINK("URL", "Text")
            //    int startUrl = cellValue.IndexOf('"') + 1;
            //    int endUrl = cellValue.IndexOf('"', startUrl);
            //    string url = cellValue.Substring(startUrl, endUrl - startUrl);

            //    int startText = cellValue.IndexOf('"', endUrl + 1) + 1;
            //    int endText = cellValue.LastIndexOf('"');
            //    hyperlinkText = cellValue.Substring(startText, endText - startText);

            //    return url;
            //}
            //catch
            //{
            //    // Return null if parsing fails
            //    return null;
            //}
        }
    }


    public class OidcTokenResponse
    {
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }


}
