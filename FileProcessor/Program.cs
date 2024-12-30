
using FileProcessor.Configuration;
using FileProcessor.Domain;
using FileProcessor.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;

namespace FileProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            builder.Services.AddScoped<Interfaces.IFileExtraction, Domain.FileExtractor>();
            builder.Services.AddScoped<IBoxService, BoxService>();

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
