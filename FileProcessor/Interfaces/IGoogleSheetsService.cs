using FileProcessor.Models;
using Google.Apis.Sheets.v4;

namespace FileProcessor.Interfaces
{
 
    public interface IGoogleSheetsService
    {
        Task<IList<IList<object>>> ReadSheetAsync(string spreadsheetId, string range);
        Task AppendToSheetAsync(string spreadsheetId, JobLog jobLog);
        Task TestConnectionAsync(string spreadsheetId, string range = "A1");
    }
}
