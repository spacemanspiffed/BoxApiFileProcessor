using Box.Sdk.Gen;
using Box.Sdk.Gen.Managers;
using Box.Sdk.Gen.Schemas;
using FileProcessor.Configuration;
using FileProcessor.Interfaces;
using FileProcessor.Models;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace FileProcessor.Services
{
    public class BoxService : IBoxService
    {
        private readonly BoxClient _client;
        private readonly ILogger<BoxService> _logger;
        private readonly BoxConfig _boxConfig;

        public BoxService(BoxConfig boxConfig, ILogger<BoxService> logger)
        {
            _logger = logger;

            // Load configuration
            _boxConfig = boxConfig;

            // Initialize BoxClient using Box.Sdk.Gen
            _client = CreateBoxClient(boxConfig);
        }

        private BoxClient CreateBoxClient(BoxConfig boxConfig)
        {
            try
            {
                //****** this is the config for the new service!!!!!!!!!!!!!!!!  MOVE TO FILE!!!!!

                var config = new CcgConfig(clientId: _boxConfig.ClientId, clientSecret: _boxConfig.ClientSecret)
                {
                    EnterpriseId = "437569",
                };
                //var config = new CcgConfig(clientId: "jvdmdnahv9fnllgxf8e3jeg0kbok366d", clientSecret: "kdiMXMslKpcaxmq7on9CeheAKQcoLG7G")
                //{
                //    EnterpriseId = "437569",
                    
                //};

                var auth = new BoxCcgAuth(config);               
                
                auth.GetType();

                auth.RetrieveTokenAsync().Wait();                

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

        public async Task<Box.Sdk.Gen.Schemas.Webhook> CreateWebHookAsync(string folderId)
        {
           var response = await _client.Webhooks.CreateWebhookAsync(requestBody: 
                                 new CreateWebhookRequestBody(target: 
                                 new CreateWebhookRequestBodyTargetField()
                                    { 
                                     Id = folderId,
                                     Type = CreateWebhookRequestBodyTargetTypeField.Folder
                                    },
                                address: "https://mighty-reindeer-thoroughly.ngrok-free.app/file/webhook",
                                triggers: Array.AsReadOnly(new[] 
                                { 
                                    new StringEnum<CreateWebhookRequestBodyTriggersField>(CreateWebhookRequestBodyTriggersField.FileUploaded)
                                })));

            return response;
        }

        public async Task<Webhooks> GetWebhooksAsync()
        {
            var response = await _client.Webhooks.GetWebhooksAsync();
            return response;
        }

        public async Task<Folder> GetFolderByIdAsync(string folderId)
        {
            FolderFull folder = await _client.Folders.GetFolderByIdAsync(folderId);
            return folder;
        }

        public async Task<bool> WebhookForFolderExists(string folderId)
        {
            var webhooks = await _client.Webhooks.GetWebhooksAsync();
            var webhookExists = webhooks.Entries.Any(webhook =>
                webhook.Target?.Id == folderId && webhook.Type.StringValue == "Folder");
            return webhookExists;
        }
    }
}
