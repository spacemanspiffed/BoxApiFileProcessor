using Box.Sdk.Gen;
using Box.Sdk.Gen.Schemas;
using FileProcessor.Configuration;
using FileProcessor.Interfaces;

namespace FileProcessor.Domain
{
    public class BoxService : IBoxService
    {
        private BoxClient _client;
        private readonly ILogger<BoxService> _logger;
        private readonly IConfiguration _configuration;
        private string _cachedToken;
        private DateTime _tokenExpiry;
        public BoxService(BoxClient client, ILogger<BoxService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            var accessToken = GetJwtAccessToken();
            _client = new BoxClient(new BoxDeveloperTokenAuth(accessToken));
        }

         private string GetJwtAccessToken()
        {
            try
            {

                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
                {
                    return _cachedToken;
                }
                var boxConfig = _configuration.GetSection("BoxConfig").Get<BoxConfig>();

                var jwtConfig = new JwtConfig(
                    clientId: boxConfig.ClientId,
                    clientSecret: boxConfig.ClientSecret,
                    jwtKeyId: boxConfig.JwtKeyId,
                    privateKey: boxConfig.PrivateKey,
                    privateKeyPassphrase: boxConfig.PrivateKeyPassphrase)
                {
                    EnterpriseId = boxConfig.EnterpriseId
                };

                var jwtAuth = new BoxJwtAuth(jwtConfig);

                // Request an access token
                var tokenResponse = jwtAuth.RetrieveTokenAsync().Result;
                _cachedToken = tokenResponse.AccessTokenField;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600);

                return _cachedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining JWT access token.");
                throw;
            }

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

        public async Task<string> GetFullPathAsync(FileFull boxFileInfo)
        {
            try
            {
                var fullPath = new Stack<string>();
                fullPath.Push(boxFileInfo.Name);
                var currentFolderId = boxFileInfo.Parent.Id;

                while (!string.IsNullOrEmpty(currentFolderId))
                {
                    var folder = await _client.Folders.GetFolderByIdAsync(currentFolderId);

                    if (folder == null)
                    {
                        throw new Exception("Folder not found.");
                    }

                    fullPath.Push(folder.Name);

                    currentFolderId = folder.Parent?.Id;
                }

                return string.Join("/", fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Attempted to retrieve full path for fileID -- {boxFileInfo.Id} -- ");
                throw;
            }
        }

        
    }
}
