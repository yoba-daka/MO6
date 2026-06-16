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
        private static readonly TimeSpan DefaultMemberSasMaxLifetime = TimeSpan.FromHours(12);
        private static readonly TimeSpan SasClockSkewBuffer = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SasRefreshBuffer = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan SasExpirationReuseTolerance = TimeSpan.FromSeconds(30);

        private readonly string accountName;
        private readonly DB _dbService;
        private readonly IConfiguration _configuration;
        private readonly IContentService _contentService;
        private readonly BlobServiceClient _blobServiceClient;

        public LessonBlobService(DB dbService, IConfiguration configuration, IContentService contentService)
        {
            _configuration = configuration;
            _contentService = contentService;
            var connectionString = configuration.GetValue<string>("BlobStorage:ConnectionString");
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            accountName = (string)builder["AccountName"];

            _dbService = dbService;
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public void setDuration(dynamic ls)
        {
            if (String.IsNullOrEmpty(ls.Duration) && !String.IsNullOrEmpty(ls.ContentName))
            {
                string durationBlobName = $"duration.txt";

                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(ls.ContentName);

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
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            var blobSasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobContainerClient.Name,
                Resource = blobName == "*" ? "c" : "b", // "c" for container and "b" for blob
                StartsOn = DateTimeOffset.UtcNow.Subtract(SasClockSkewBuffer),
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
            return GenerateMemberContainerSas(containerName, memberId, DateTimeOffset.UtcNow.Add(DefaultMemberSasMaxLifetime).UtcDateTime);
        }

        public string GenerateMemberContainerSas(string containerName, string memberId, DateTime membershipExpiration)
        {
            var existingToken = _dbService.GetValidToken(containerName,"*", memberId);
            var now = DateTimeOffset.UtcNow;
            var membershipExpiresAt = NormalizeMembershipExpiration(membershipExpiration);
            var expirationTime = MinDateTimeOffset(now.Add(DefaultMemberSasMaxLifetime), membershipExpiresAt);

            if (expirationTime <= now)
            {
                return "";
            }

            var existingTokenExpiration = existingToken == null
                ? (DateTimeOffset?)null
                : ToUtc(existingToken.TokenExpiration);
            var refreshBoundary = now.Add(SasRefreshBuffer);
            var existingTokenAlreadyReachesRequestedLimit =
                existingTokenExpiration.HasValue &&
                existingTokenExpiration.Value >= expirationTime.Subtract(SasExpirationReuseTolerance);

            if (existingTokenExpiration.HasValue &&
                existingTokenExpiration.Value > now &&
                existingTokenExpiration.Value <= expirationTime &&
                (existingTokenExpiration.Value > refreshBoundary || existingTokenAlreadyReachesRequestedLimit))
            {
                return existingToken.Token;
            }

            var permissions = BlobSasPermissions.Read;

            var sasToken = GenerateSasToken(containerName, "*", expirationTime, permissions);

            var tokenEntity = new SasToken
            {
                MemberId = memberId,
                TokenExpiration = expirationTime.UtcDateTime,
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
            var now = DateTimeOffset.UtcNow;
            if (existingToken != null && ToUtc(existingToken.TokenExpiration) > now.Add(SasRefreshBuffer))
            {
                return existingToken.Token;
            }

            var expirationTime = now.AddDays(2);
            var permissions = BlobSasPermissions.Read;

            var sasToken = GenerateSasToken(containerName, blobName, expirationTime, permissions);

            var tokenEntity = new SasToken
            {
                TokenExpiration = expirationTime.UtcDateTime,
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
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(blobName);
                return blobClient.Exists();
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> BlobExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(blobName))
            {
                return false;
            }

            try
            {
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(blobName);
                return await blobClient.ExistsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public BlobClient GetBlobClient(string containerName, string blobName)
        {
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            return blobContainerClient.GetBlobClient(blobName);
        }

        public string GetBlobServiceOrigin()
        {
            return string.IsNullOrWhiteSpace(accountName)
                ? ""
                : $"https://{accountName}.blob.core.windows.net";
        }

        public string GenerateBlobUrl(string containerName,string blobName, string sasToken)
        {
            try
            {
                var token = GetTokenOnly(sasToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return "";
                }

                var encodedContainerName = Uri.EscapeDataString(containerName);
                var encodedBlobName = string.Join('/',
                    blobName
                        .Replace('\\', '/')
                        .Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(Uri.EscapeDataString));

                return $"https://{accountName}.blob.core.windows.net/{encodedContainerName}/{encodedBlobName}?{token}";
            }
            catch { return ""; }
        }

        public string GetTokenOnly(string sasToken)
        {
            if (string.IsNullOrWhiteSpace(sasToken))
            {
                return "";
            }

            var queryIndex = sasToken.IndexOf('?');
            if (queryIndex >= 0 && queryIndex < sasToken.Length - 1)
            {
                return sasToken[(queryIndex + 1)..];
            }

            return sasToken.TrimStart('?');
        }

        private static DateTimeOffset NormalizeMembershipExpiration(DateTime membershipExpiration)
        {
            return membershipExpiration.Kind switch
            {
                DateTimeKind.Utc => new DateTimeOffset(membershipExpiration, TimeSpan.Zero),
                DateTimeKind.Local => new DateTimeOffset(membershipExpiration).ToUniversalTime(),
                _ => new DateTimeOffset(DateTime.SpecifyKind(membershipExpiration, DateTimeKind.Local)).ToUniversalTime()
            };
        }

        private static DateTimeOffset ToUtc(DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => new DateTimeOffset(dateTime, TimeSpan.Zero),
                DateTimeKind.Local => new DateTimeOffset(dateTime).ToUniversalTime(),
                _ => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc), TimeSpan.Zero)
            };
        }

        private static DateTimeOffset MinDateTimeOffset(DateTimeOffset first, DateTimeOffset second)
        {
            return first <= second ? first : second;
        }
    }
}
