using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using MyProject12.Models;
using System;
using System.Data.Common;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;

namespace MyProject12
{
    public class LessonBlobService
    {
        private readonly string _connectionString;
        private readonly string accountName;
        private readonly DB _dbService;
        private readonly IConfiguration _configuration;
        private readonly IContentService _contentService;

        public LessonBlobService(DB dbService, IConfiguration configuration, IContentService contentService)
        {
            _configuration = configuration;
            _contentService = contentService;
            _connectionString = configuration.GetValue<string>("BlobStorage:ConnectionString");
            var builder = new DbConnectionStringBuilder { ConnectionString = _connectionString };
            accountName = (string)builder["AccountName"];

            _dbService = dbService;
        }

        public void setDuration(dynamic ls)
        {
            if (String.IsNullOrEmpty(ls.Duration) && !String.IsNullOrEmpty(ls.ContentName))
            {
                string durationBlobName = $"duration.txt";

                var blobServiceClient = new BlobServiceClient(_connectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(ls.ContentName);

                var durationBlobClient = blobContainerClient.GetBlobClient(durationBlobName);

                if (durationBlobClient.Exists().Value) // Check if duration.txt exists
                {
                    using (BlobDownloadInfo response = durationBlobClient.Download())
                    using (var sr = new StreamReader(response.Content))
                    {
                        var durationText = sr.ReadToEnd();

                        dynamic node = _contentService.GetById(ls.Id);
                        node.SetValue("duration", durationText);
                        _contentService.SaveAndPublish(node);
                    }
                }
                else
                {
                    dynamic node = _contentService.GetById(ls.Id);
                    node.SetValue("duration", "00:00");
                    _contentService.SaveAndPublish(node);
                    Console.WriteLine("duration.txt doesn't exist in the blob!");
                }
            }
        }

        private string GenerateSasToken(string containerName, string blobName, DateTimeOffset expirationTime, BlobSasPermissions permissions)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            var blobSasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobContainerClient.Name,
                Resource = blobName == "*" ? "c" : "b", // "c" for container and "b" for blob
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = expirationTime
            };

            if (blobName != "*")
            {
                var blobClient = blobContainerClient.GetBlobClient(blobName);
                blobSasBuilder.BlobName = blobClient.Name;
            }

            blobSasBuilder.SetPermissions(permissions);

            var sasToken = blobName == "*" ? blobContainerClient.GenerateSasUri(blobSasBuilder) : blobContainerClient.GetBlobClient(blobName).GenerateSasUri(blobSasBuilder);
            return sasToken.ToString();
        }


        public string GenerateMemberContainerSas(string containerName, string memberId)
        {
            var existingToken = _dbService.GetValidToken(containerName,"*", memberId);
            if (existingToken != null)
            {
                return existingToken.Token;
            }

            var expirationTime = DateTimeOffset.UtcNow.AddHours(3);
            var permissions = BlobSasPermissions.Read;

            var sasToken = GenerateSasToken(containerName, "*", expirationTime, permissions);

            var tokenEntity = new SasToken
            {
                MemberId = memberId,
                TokenExpiration = expirationTime.DateTime,
                Token = sasToken,
                ContainerName = containerName,
                BlobName = "*"
            };

            _dbService.AddToken(tokenEntity);

            return sasToken;
        }

        public string GenerateUnlimitedSas(string containerName, string blobName)
        {
            var existingToken = _dbService.GetValidToken(containerName, blobName, "*");
            if (existingToken != null)
            {
                return existingToken.Token;
            }

            var expirationTime = DateTimeOffset.UtcNow.AddDays(2);
            var permissions = BlobSasPermissions.Read;

            var sasToken = GenerateSasToken(containerName, blobName, expirationTime, permissions);

            var tokenEntity = new SasToken
            {
                TokenExpiration = expirationTime.DateTime,
                Token = sasToken,
                ContainerName = containerName,
                BlobName = blobName,
                MemberId = "*"
            };

            _dbService.AddToken(tokenEntity);

            return sasToken;
        }

        public bool BlobExists(string containerName, string blobName)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(blobName);
                return blobClient.Exists();
            }
            catch
            {
                return false;
            }
        }

        public string GenerateBlobUrl(string containerName,string blobName, string sasToken)
        {
            try
            {
                var token = sasToken.Split('?')[1];
                return $"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{token}";
            }
            catch { return ""; }
        }

        public string GetTokenOnly(string sasToken)
        {
            try
            {
                return sasToken.Split('?')[1];
            }
            catch { return ""; }
        }
    }
}
