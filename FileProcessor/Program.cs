
using FileProcessor.Configuration;
using FileProcessor.Interfaces;
using FileProcessor.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace FileProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            var googleCredentialsPath = "c:\\code\\ditto\\resources\\wif-config.json";
            GoogleCredentialsConfig googleCredentialsConfig;

            if (File.Exists(googleCredentialsPath))
            {
                var jsonContent = File.ReadAllText(googleCredentialsPath);
                googleCredentialsConfig = JsonConvert.DeserializeObject<GoogleCredentialsConfig>(jsonContent);
                if (googleCredentialsConfig == null)
                {
                    throw new InvalidOperationException("Failed to deserialize Google credentials JSON.");
                }
            }
            else
            {
                throw new FileNotFoundException($"Google credentials file not found: {googleCredentialsPath}");
            }

            

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
            builder.Services.Configure<BoxConfig>(builder.Configuration.GetSection("BoxConfig"));
            builder.Services.Configure<GoogleSheetConfig>(builder.Configuration.GetSection("GoogleSheetConfig"));
            
            builder.Services.AddSingleton(googleCredentialsConfig);

            builder.Services.AddScoped<IFileExtraction, Domain.FileExtractor>();
            builder.Services.AddScoped<IBoxService, BoxService>();
            builder.Services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();

            var app = builder.Build();
            app.UseMiddleware<RequestLoggingMiddleware>();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
