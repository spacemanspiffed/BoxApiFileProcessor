namespace FileProcessor.Models
{
    public class JobLog
    {
        public string Status { get; set; }
        public string QADate { get; set; }
        public string Client { get; set; }
        public Hyperlink Template { get; set; }
        public string Category { get; set; } = "General";
        public string FileName { get; set; }
        public string FileLink { get; set; }
        public DateTime DateReceived { get; set; }
        public DateTime ICDueDate { get; set; }
        public DateTime FinalDueDate { get; set; }
        public DateTime SpecialDueDate { get; set; }
        public string Returned { get; set; }
        public string Transcriptionist { get; set; }
        public TimeSpan FileLength { get; set; }
        public double Minutes { get; set; }
        public string TAT { get; set; } = "Standard";

        // New Columns (defaulted to null)
        public int? NumberOfSpeakers { get; set; } = null;
        public string VerbatimOrTimestamps { get; set; } = null;
        public string TT { get; set; }
        public string Type { get; set; }
        public decimal? ICRate { get; set; } = null;
        public decimal? ICTotal { get; set; } = null;
        public decimal? Rate { get; set; } = null;
        public decimal? Pricing { get; set; } = null;
        public decimal? SpecialRate { get; set; } = null;
        public Hyperlink SpecialTemplate { get; set; } = null;
        public string Feedback { get; set; } = null;
        public string NotesAndComments { get; set; } = null;

        /// <summary>
        /// Converts the JobLog object to a Google Sheets-compatible row.
        /// </summary>
        public IList<object> ConvertToValues()
        {
            return new List<object>
        {
            Status,
            QADate,
            Client,
            Template != null ? $"=HYPERLINK(\"{Template.Url}\", \"{Template.Text}\")" : null, // Template column
            Category,
            FileName,
            FileLink != null ? $"=HYPERLINK(\"{FileLink}\", \"Link\")" : null, // File Link column
            DateReceived != default ? DateReceived.ToString("yyyy-MM-dd") : null,
            ICDueDate != default ? ICDueDate.ToString("yyyy-MM-dd") : null,
            FinalDueDate != default ? FinalDueDate.ToString("yyyy-MM-dd") : null,
            SpecialDueDate != default ? SpecialDueDate.ToString("yyyy-MM-dd") : null,
            Returned,
            Transcriptionist,
            FileLength != default ? FileLength.ToString(@"hh\:mm\:ss") : null,
            Minutes != 0 ? Minutes : null,
            JobLogValidator.ValidateTAT(TAT),
            NumberOfSpeakers,             // Null if not set
            VerbatimOrTimestamps,         // Null if not set
            TT, 
            Type,
            ICRate?.ToString("F2"),       // Null if not set
            ICTotal?.ToString("F2"),      // Null if not set
            Rate?.ToString("F2"),         // Null if not set
            Pricing?.ToString("F2"),      // Null if not set
            SpecialRate?.ToString("F2"),  // Null if not set
            SpecialTemplate != null ? $"=HYPERLINK(\"{SpecialTemplate.Url}\", \"{SpecialTemplate.Text}\")" : null,
            Feedback,
            NotesAndComments
        };
        }
    }
    public static class JobLogValidator
    {
        private static readonly List<string> ValidCategories = new()
    {
        "Legal", "Law Enforcement", "Medical", "General", "Spanish", "Copy Typing"
    };

        private static readonly List<string> ValidTATs = new()
    {
        "Rush", "Standard", "Extended", "Warrant"
    };

        public static string ValidateCategory(string category)
        {
            return !string.IsNullOrEmpty(category) && ValidCategories.Contains(category)
                ? category
                : "General"; // Default to "General" if null or invalid
        }

        public static string ValidateTAT(string tat)
        {
            return !string.IsNullOrEmpty(tat) && ValidTATs.Contains(tat)
                ? tat
                : "Standard"; // Default to "Standard" if null or invalid
        }



    }
}