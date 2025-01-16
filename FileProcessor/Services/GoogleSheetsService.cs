﻿using System;
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



        private SheetsService _sheetsService;

        private readonly ILogger<GoogleSheetsService> _logger;
        private readonly GoogleSheetsCredentials _credentials;
        private readonly GoogleSheetConfig _sheetConfig;
        public static List<ClientTemplate> _templateCache = new List<ClientTemplate>();
        public static List<string> _clientListCache = new List<string>();

        public GoogleSheetsService(GoogleSheetsCredentials googleCredentialsConfig, ILogger<GoogleSheetsService> logger, IOptions<GoogleSheetConfig> sheetConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _credentials = googleCredentialsConfig ?? throw new ArgumentNullException(nameof(googleCredentialsConfig));

            _sheetConfig = sheetConfig.Value;

            InitializeGoogleSheetService().Wait();

        }

        private async Task InitializeGoogleSheetService()
        {

            try
            {

                var credentialsJson = JsonConvert.SerializeObject(_credentials);
                var credential = GoogleCredential.FromJson(credentialsJson)
                        .CreateScoped(SheetsService.Scope.Spreadsheets);
                _sheetsService = new SheetsService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "FileProcessor App"
                });



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
            "Status", "QA Date","UploadedBy", "Client", "Template", "Category", "File Name",
            "File Link", "Date Received", "IC Due Date", "Final Due Date", "Special Due Date",
            "Returned", "Transcriptionist", "Duration", "Minutes", "TAT",
            "Number of Speakers", "Verbatim or Timestamps","TT", "Type", "IC Rate", "IC Total",
            "Rate", "Pricing", "Special Rate", "Special Template", "Feedback",
            "Notes and Comments", "FileId"
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
            //****When we are ready to reintroduce caching uncomment these lines and comment the lines below!!!!****

            //if (_templateCache.Count == 0)
            //{
            //    // Read the data and cache it as a List<ClientTemplate>
            //    _templateCache = await ReadTemplateSheet(spreadsheetId, range);
            //}

            //if (_clientListCache.Count == 0)
            //{
            //    // Cache the list of Client names separately
            //    _clientListCache = _templateCache
            //        .Select(template => template.ClientName)
            //        .ToList();
            //}

            _templateCache = await ReadTemplateSheet(spreadsheetId, range);
            _clientListCache = _templateCache
                .Select(template => template.ClientName)
                .ToList();
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
           
        }
       

        public async Task<List<string>> GetIgnoredFileTypes()
        {
            var sheetId = _sheetConfig.SheetId;
            var range = _sheetConfig.IgnoredFileTypes;
            _logger.LogInformation($"Looking for this spreadsheet {sheetId}, and this range -- {range}");
            if (string.IsNullOrWhiteSpace(sheetId))
                throw new ArgumentException("Spreadsheet ID cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(range))
                throw new ArgumentException("Range cannot be null or empty.");
            
            var request = _sheetsService.Spreadsheets.Values.Get(sheetId, range);

            try
            {
                ValueRange response = await request.ExecuteAsync();
                var values = response.Values;

                if (values == null || values.Count == 0)
                {
                    Console.WriteLine("No data found in the 'Ignored_Types' tab.");
                    return new List<string>();
                }

                // Convert the data to a list of strings
                var ignoredTypes = new List<string>();
                foreach (var row in values)
                {
                    if (row.Count > 0 && row[0] is string cellValue)
                    {
                        ignoredTypes.Add(cellValue.Trim()); // Trim whitespace
                    }
                }

                return ignoredTypes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data from Google Sheets: {ex.Message}");
                throw;
            }

        }

        public async Task ExpireCache()
        {
            _logger.LogInformation("Expiring cache...");
            _templateCache.Clear();
             _clientListCache.Clear();
            await CacheTemplatesAsync(_sheetConfig.SheetId, _sheetConfig.Customers);
        }

        public async Task<List<string>> GetUploadedFiles()
        {
            var headerName = "FileId";
            var headerRange = $"{_sheetConfig.JobLog}!1:1";
            var headerRequest = _sheetsService.Spreadsheets.Values.Get(_sheetConfig.SheetId, headerRange);

            var headerResponse = await headerRequest.ExecuteAsync();

            var headers = headerResponse.Values?[0];

            if (headers == null || !headers.Contains(headerName))
            {
                throw new ArgumentException($"Header '{headerName}' not found in sheet '{_sheetConfig.JobLog}'.");
            }

            var columnIndex = headers.IndexOf(headerName); // 0-based index

            // Convert the column index to A1 notation
            var columnLetter = ConvertIndexToColumnLetter(columnIndex);

            // Fetch the entire column data
            var columnRange = $"{_sheetConfig.JobLog}!{columnLetter}:{columnLetter}";
            var columnRequest = _sheetsService.Spreadsheets.Values.Get(_sheetConfig.SheetId, columnRange);
            var columnResponse = await columnRequest.ExecuteAsync();

            // Process the column data (skip the header row)
            var columnData = new List<string>();
            foreach (var row in columnResponse.Values?.Skip(1) ?? new List<IList<object>>())
            {
                if (row.Count > 0 && row[0] is string cellValue)
                {
                    columnData.Add(cellValue.Trim()); // Trim whitespace
                }
            }

            return columnData;

        }

        private string ConvertIndexToColumnLetter(int index)
        {
            string column = string.Empty;
            index++; // Convert to 1-based index

            while (index > 0)
            {
                index--;
                column = (char)('A' + (index % 26)) + column;
                index /= 26;
            }

            return column;
        }
    }


    public class OidcTokenResponse
    {
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }


}
