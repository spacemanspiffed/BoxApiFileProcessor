using Box.Sdk.Gen.Schemas;
using FileProcessor.Controllers;
using FileProcessor.Entities;
using FileProcessor.Interfaces;
using MediaInfo;

namespace FileProcessor.Domain
{
    public class FileExtractor : IFileExtraction
    {
        private readonly ILogger<FileExtractor> _logger;
        public FileExtractor(ILogger<FileExtractor> logger)
        {
            _logger = logger;

        }       
        public TimeSpan ExtractFromStream(Stream stream)
        {
            
            MediaInfoWrapper wrapper = new MediaInfoWrapper(stream, _logger);

            TimeSpan duration = TimeSpan.FromMilliseconds(wrapper.Duration);
            return duration;
           

        }

        public FileMetaData ExtractMetadata(string filePath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("The file does not exist.");
            }

            string fileExtension = Path.GetExtension(fileName).ToLower();

            switch (fileExtension)
            {
                case ".mp4":
                case ".mkv":
                case ".avi":
                    return ExtractVideoMetadata(filePath, fileName);

                case ".mp3":
                case ".wav":
                    return ExtractAudioMetadata(filePath, fileName);

                default:
                    throw new NotSupportedException($"The file type '{fileExtension}' is not supported.");
            }
        }

        private FileMetaData ExtractVideoMetadata(string filePath, string fileName)
        {
            try
            {
                MediaInfoWrapper wrapper = new MediaInfoWrapper(filePath, _logger);                

                return new FileMetaData
                {
                    FileName = fileName,
                    Extension = Path.GetExtension(fileName),
                    Duration = TimeSpan.FromMilliseconds(wrapper.Duration),
                    ExtractedDate = DateTime.Now,
                    UploadDate = DateTime.Now, //TODO: Get Date from Box
                    Description = "This is a test real description will come from Box",
                    FolderResponseType = "",
                    ClientEmail = "sjustus@justustechsolutions.com",
                    UploadedBy = "sjustus@justustechsolutions.com"

                };
            }
            catch (Exception)
            {

                throw;
            }
        }

        private FileMetaData ExtractAudioMetadata(string filePath, string fileName)
        {
            var media = new MediaInfoWrapper(filePath);

            return new FileMetaData
            {
                FileName = fileName,
                Extension = Path.GetExtension(fileName),
                Duration = TimeSpan.FromMilliseconds(media.Duration),
                ExtractedDate = DateTime.Now,
                UploadDate = DateTime.Now, //TODO: Get Date from Box
                Description = "This is a test real description will come from Box",
                FolderResponseType = "",
                ClientEmail = "sjustus@justustechsolutions.com",
                UploadedBy = "sjustus@justustechsolutions.com"

            };
        }


    }
}
