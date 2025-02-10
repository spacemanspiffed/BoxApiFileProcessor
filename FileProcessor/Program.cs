
using FileProcessor.Configuration;
using FileProcessor.Domain;
using FileProcessor.Interfaces;
using FileProcessor.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging.AzureAppServices;

namespace FileProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            var keyVaultUri = "https://dittovault.vault.azure.net/";

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddAzureWebAppDiagnostics(); // Enables logging for Azure App Service
            builder.Services.Configure<AzureFileLoggerOptions>(options =>
            {
                options.FileName = "azure-diagnostics-";
                options.FileSizeLimit = 50 * 1024; // 50 KB per log file
                options.RetainedFileCountLimit = 5; // Retain 5 log files
            });
            builder.Services.Configure<AzureBlobLoggerOptions>(options =>
            {
                options.BlobName = "log.txt";
            });

            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var tenant = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var sec = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

            builder.Services.AddApplicationInsightsTelemetry();

            //var keyVaultCredential = new AzureCliCredential();

            var credentialOptions = new DefaultAzureCredentialOptions
            {
                AdditionallyAllowedTenants = { "*" }
            };
            
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential(credentialOptions));

            //var googleCredentialsPath = "c:\\code\\ditto\\resources\\wif-config.json";
            //GoogleCredentialsConfig googleCredentialsConfig;

            builder.Logging.AddConsole();
            
            //Google and Box Credentials from Keyvault
            var googleSheetsJson = builder.Configuration["GoogleSheetsCredentials"];
            if (string.IsNullOrEmpty(googleSheetsJson))
            {
                throw new Exception("Google Sheets credentials not found in Key Vault");
            }
            GoogleSheetsCredentials googleCredentialsConfig = JsonConvert.DeserializeObject<GoogleSheetsCredentials>(googleSheetsJson);

            builder.Services.AddSingleton(googleCredentialsConfig);

            var boxConfigJson = builder.Configuration["BoxConfig"];
            if (string.IsNullOrEmpty(boxConfigJson))
            {
                throw new Exception("Box config not found in Key Vault");
            }
            var boxConfig = JsonConvert.DeserializeObject<BoxConfig>(boxConfigJson);

            builder.Services.AddSingleton(boxConfig);

            //Google Sheet and Sheet info from json
            builder.Configuration.AddEnvironmentVariables();            
            builder.Services.Configure<GoogleSheetConfig>(builder.Configuration.GetSection("GoogleSheetConfig"));

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 5368709120; // 5 GB                
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 5368709120; // 5 GB
            });
            // Add services to the container.

            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            //Register configuration
            //builder.Services.Configure<BoxConfig>(builder.Configuration.GetSection("BoxConfig"));
            
            
            //builder.Services.AddSingleton(googleCredentialsConfig);

            builder.Services.AddScoped<IFileExtraction, Domain.FileExtractor>();
            builder.Services.AddScoped<IBoxService, BoxService>();
            builder.Services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();
            builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            builder.Services.AddHostedService<BackgroundTaskHostedService>();

            var app = builder.Build();
            app.UseMiddleware<RequestLoggingMiddleware>();

            app.MapGet("/", () => "Welcome to the base address!");
            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();            

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
