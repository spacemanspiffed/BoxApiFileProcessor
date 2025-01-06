using Box.Sdk.Gen;
using Box.Sdk.Gen.Managers;
using Box.Sdk.Gen.Schemas;
using FileProcessor.Configuration;
using FileProcessor.Interfaces;
using Microsoft.Extensions.Options;

namespace FileProcessor.Services
{
    public class BoxService : IBoxService
    {
        private readonly BoxClient _client;
        private readonly ILogger<BoxService> _logger;

        public BoxService(IOptions<BoxConfig> boxConfigOptions, ILogger<BoxService> logger)
        {
            _logger = logger;

            // Load configuration
            var boxConfig = boxConfigOptions.Value;

            // Initialize BoxClient using Box.Sdk.Gen
            _client = CreateBoxClient(boxConfig);
        }

        private BoxClient CreateBoxClient(BoxConfig boxConfig)
        {
            try
            {
                var jwtAuthConfig = new JwtConfig(
                    clientId: boxConfig.ClientId,
                    clientSecret: boxConfig.ClientSecret,
                    jwtKeyId: boxConfig.JwtKeyId,
                    privateKey: boxConfig.PrivateKey,
                    privateKeyPassphrase: boxConfig.Passphrase)
                {
                    UserId = boxConfig.UserId
                };

                // **** Use the JWT config to authenticate and create the BoxClient
                //var jwtAuth = new BoxJwtAuth(jwtAuthConfig);
                //var userToken = jwtAuth.RetrieveTokenAsync().Result; // Admin token for enterprise access
                //var client = new BoxClient(new BoxDeveloperTokenAuth(userToken.AccessTokenField));

                //Developer Token Path
                var auth = new BoxDeveloperTokenAuth(token: "uKAvLoAK7OniOCgYM7ZUCIY59ITkZWXI");
                var client = new BoxClient(auth: auth);
                
                _logger.LogInformation("Successfully authenticated Box client.");
                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Box client.");
                throw;
            }
        }

        public async Task<UserFull> TestConnectionAsync()
        {
            try
            {
                var currentUser = await _client.Users.GetUserMeAsync();
                _logger.LogInformation($"Successfully connected to Box as {currentUser.Login}");
                return currentUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Box connection.");
                throw;
            }
        }

        public BoxClient GetClient()
        {
            return _client;
        }

        public async Task<Stream> DownloadFileAsync(string fileId)
        {
            try
            {
                return await _client.Downloads.DownloadFileAsync(fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading file by ID.  -- Go find this file -- {fileId} --");
                throw;
            }
        }

        public async Task<FileFull> GetFileByIdAsync(string fileId)
        {
            try
            {
                return await _client.Files.GetFileByIdAsync(fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve data from box for fileID -- {fileId} -- ");
                throw;
            }
        }

        public async Task<List<string>> GetFullPathAsync(FileFull boxFileInfo)
        {
            try
            {
                var fullPath = new List<string>
                {
                    boxFileInfo.Name ?? ""
                };
                var currentFolderId = boxFileInfo.Parent?.Id;

                while (!string.IsNullOrEmpty(currentFolderId))
                {
                    
                    var folder = await _client.Folders.GetFolderByIdAsync(currentFolderId);

                    if (folder == null)
                    {
                        throw new Exception("Folder not found.");
                    }

                    fullPath.Add(folder.Name);

                    currentFolderId = folder.Parent?.Id;
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Attempted to retrieve full path for fileID -- {boxFileInfo.Id} -- ");
                throw;
            }
        }

        public async Task<FileFull> CreateSharedLinkAsync(FileFull file)
        {
                                    
            return await _client.SharedLinksFiles.AddShareLinkToFileAsync(file.Id, 
                requestBody: new AddShareLinkToFileRequestBody() 
                {SharedLink = new AddShareLinkToFileRequestBodySharedLinkField() {Access = AddShareLinkToFileRequestBodySharedLinkAccessField.Company}},
                queryParams: new AddShareLinkToFileQueryParams(fields: "shared_link" ));

            
           
        }
    }
}
