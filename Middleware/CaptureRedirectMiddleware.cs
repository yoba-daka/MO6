using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MO6.Middleware
{
    public class CaptureRedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly bool _enabled;
        private readonly string[] _capturePaths;
        private readonly string[] _allowedOrigins;
        private readonly string _connectionString;
        private readonly ILogger<CaptureRedirectMiddleware> _logger;
        private readonly BlobContainerClient _containerClient;
        private readonly bool _shortCircuitResponse;

        public CaptureRedirectMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<CaptureRedirectMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _enabled = configuration.GetValue<bool>("CaptureRedirect:Enabled");
            _shortCircuitResponse = configuration.GetValue<bool>("CaptureRedirect:ShortCircuitResponse");

            // Get connection string from Umbraco storage config
            _connectionString = configuration["Umbraco:Storage:AzureBlob:Media:ConnectionString"];

            // Initialize blob container client
            if (!string.IsNullOrEmpty(_connectionString))
            {
                _containerClient = new BlobContainerClient(_connectionString, "httpcapture");
            }

            // Parse allowed origins (empty means all origins)
            var originsConfig = configuration["CaptureRedirect:AllowedOrigins"] ?? "";
            _allowedOrigins = originsConfig
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim().ToLowerInvariant())
                .ToArray();

            // Parse capture paths
            var paths = configuration["CaptureRedirect:Paths"] ?? "";
            _capturePaths = paths
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().ToLowerInvariant())
                .ToArray();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_enabled || !_capturePaths.Any() || _containerClient == null)
            {
                await _next(context);
                return;
            }

            // Check origin if filtering is enabled
            if (_allowedOrigins.Any())
            {
                var origin = context.Request.Headers["Origin"].FirstOrDefault()?.ToLowerInvariant()
                    ?? context.Request.Headers["Referer"].FirstOrDefault()?.ToLowerInvariant()
                    ?? "";

                var host = context.Request.Host.Value?.ToLowerInvariant() ?? "";

                var isAllowed = _allowedOrigins.Any(allowed =>
                    origin.Contains(allowed) ||
                    host.Contains(allowed) ||
                    (allowed == "*" || allowed == ""));

                if (!isAllowed)
                {
                    await _next(context);
                    return;
                }
            }

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Check if this path should be captured
            var shouldCapture = _capturePaths.Any(capturePath =>
            {
                if (capturePath.EndsWith("*"))
                {
                    var prefix = capturePath.TrimEnd('*');
                    return path.StartsWith(prefix);
                }
                return path == capturePath;
            });

            if (shouldCapture)
            {
                try
                {
                    await CaptureRequestAsync(context);

                    if (_shortCircuitResponse)
                    {
                        // Debug-only behavior: swallow request after capture.
                        context.Response.StatusCode = 200;
                        context.Response.Headers.Add("X-Captured", "true");
                        context.Response.Headers.Add("X-Capture-Timestamp", DateTime.UtcNow.ToString("O"));
                        await context.Response.WriteAsync("Request captured successfully");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing request");
                    // Continue to next middleware on error
                }
            }

            await _next(context);
        }

        private async Task CaptureRequestAsync(HttpContext context)
        {
            var request = context.Request;

            _logger.LogInformation($"Capturing {request.Method} request to {request.Path}");

            // Create captured request object
            var capturedRequest = new CapturedRequest
            {
                Timestamp = DateTime.UtcNow,
                Method = request.Method,
                Path = request.Path.Value,
                QueryString = request.QueryString.Value,
                Scheme = request.Scheme,
                Host = request.Host.Value,
                Protocol = request.Protocol,
                RawUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
                IsHttps = request.IsHttps
            };

            // Capture headers
            capturedRequest.Headers = new Dictionary<string, List<string>>();
            foreach (var header in request.Headers)
            {
                capturedRequest.Headers[header.Key] = header.Value.ToList();
            }

            // Capture body
            if (request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
            {
                try
                {
                    request.EnableBuffering(); // Enable multiple reads

                    using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
                    {
                        capturedRequest.Body = await reader.ReadToEndAsync();
                        capturedRequest.ContentLength = capturedRequest.Body.Length;
                    }

                    // Reset stream position for next middleware if needed
                    request.Body.Position = 0;

                    _logger.LogInformation($"Captured body with {capturedRequest.ContentLength} chars");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading request body");
                    capturedRequest.Body = $"[Error reading body: {ex.Message}]";
                }
            }

            // Capture content type
            capturedRequest.ContentType = request.ContentType;

            // Connection info
            capturedRequest.RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString();
            capturedRequest.RemotePort = context.Connection.RemotePort;
            capturedRequest.LocalIpAddress = context.Connection.LocalIpAddress?.ToString();
            capturedRequest.LocalPort = context.Connection.LocalPort;

            // Front Door / Proxy headers
            if (request.Headers.TryGetValue("X-Forwarded-Host", out var hosts))
                capturedRequest.OriginalHost = hosts.FirstOrDefault();

            if (request.Headers.TryGetValue("X-Forwarded-For", out var ips))
                capturedRequest.ClientIp = ips.FirstOrDefault()?.Split(',')[0].Trim();

            if (string.IsNullOrEmpty(capturedRequest.ClientIp))
            {
                if (request.Headers.TryGetValue("X-Real-IP", out var realIps))
                    capturedRequest.ClientIp = realIps.FirstOrDefault();
            }

            if (request.Headers.TryGetValue("X-Azure-FDID", out var fdids))
                capturedRequest.FrontDoorId = fdids.FirstOrDefault();

            if (request.Headers.TryGetValue("X-Azure-RequestId", out var reqids))
                capturedRequest.FrontDoorRequestId = reqids.FirstOrDefault();

            // Capture cookies
            capturedRequest.Cookies = new Dictionary<string, string>();
            foreach (var cookie in request.Cookies)
            {
                capturedRequest.Cookies[cookie.Key] = cookie.Value;
            }

            // Save to blob storage
            await SaveToBlobStorageAsync(capturedRequest);
        }

        private async Task SaveToBlobStorageAsync(CapturedRequest request)
        {
            try
            {
                // Ensure container exists
                await _containerClient.CreateIfNotExistsAsync();

                // Generate blob name
                var timestamp = request.Timestamp.ToString("yyyy-MM-dd-HHmmss-fff");
                var method = request.Method?.ToLower() ?? "unknown";
                var safePath = request.Path?.Replace("/", "-").Trim('-') ?? "root";
                var blobName = $"{request.Timestamp:yyyy-MM-dd}/{timestamp}-{method}-{safePath}-{Guid.NewGuid():N}.json";

                var blobClient = _containerClient.GetBlobClient(blobName);

                // Serialize to JSON
                var json = JsonConvert.SerializeObject(request, Formatting.Indented);

                // Upload
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                // Set metadata
                var metadata = new Dictionary<string, string>
                {
                    { "Method", request.Method },
                    { "Path", request.Path?.Replace("/", "_") ?? "unknown" },
                    { "HasBody", (!string.IsNullOrEmpty(request.Body)).ToString() },
                    { "ContentType", request.ContentType ?? "none" },
                    { "ClientIP", request.ClientIp ?? request.RemoteIpAddress ?? "unknown" }
                };

                await blobClient.SetMetadataAsync(metadata);

                _logger.LogInformation($"Saved captured request to blob: {blobName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving to blob storage");
                throw;
            }
        }
    }

    // CapturedRequest class definition
    public class CapturedRequest
    {
        public DateTime Timestamp { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public Dictionary<string, List<string>> Headers { get; set; }
        public string Body { get; set; }
        public string ContentType { get; set; }
        public long? ContentLength { get; set; }
        public string Scheme { get; set; }
        public string Host { get; set; }
        public string Protocol { get; set; }

        // Connection info
        public string RemoteIpAddress { get; set; }
        public int? RemotePort { get; set; }
        public string LocalIpAddress { get; set; }
        public int? LocalPort { get; set; }

        // Front Door specific
        public string OriginalHost { get; set; }
        public string ClientIp { get; set; }
        public string FrontDoorId { get; set; }
        public string FrontDoorRequestId { get; set; }

        // Additional context
        public Dictionary<string, string> Cookies { get; set; }
        public bool IsHttps { get; set; }
        public string RawUrl { get; set; }
    }
}
