using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using VideoWebApp.Interface;
using Microsoft.Extensions.Configuration;
using VideoWebApp.Models;
using Azure.Storage.Blobs.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VideoWebapp.DbHelper;
using System.Net;

namespace VideoWebApp.Services
{
    public class AzureService : IAzureService
    {
        # region Dependency Injection / Constructor
        private readonly string _storageConnectionString;
        private readonly string _storageContainerName;
        private readonly ILogger<AzureService> _logger;
        private readonly DbHelper _dbHelper;

        public AzureService(IConfiguration configuration, ILogger<AzureService> Logger, DbHelper dbHelper)
        {
            _storageConnectionString = configuration.GetValue<string>("BlobConnectionString");
            _storageContainerName = configuration.GetValue<string>("BlobContainerName");
            _logger = Logger;
            _dbHelper = dbHelper;
        }
        #endregion

        public string GenerateSasToken(string container, string BlobContainerName)
        {
            var BlobServiceClient = new BlobServiceClient(_storageConnectionString);
            var blobContainerClient = BlobServiceClient.GetBlobContainerClient(container);
            var blobClient = blobContainerClient.GetBlobClient(BlobContainerName);   

            var sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = container,
                BlobName = BlobContainerName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(2)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

            var sasToken = blobClient.GenerateSasUri(sasBuilder).Query;

            return sasToken;
        }
        public async Task<string> GenerateSasUrlForBlob(string containerName, string blobName, bool write)
        {
            var blobServiceClient = new BlobServiceClient(_storageConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // 5 minutes before for clock skew
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) // 1 hour validity
            };

            if (write)
            {
                sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create | BlobSasPermissions.Read);
            }
            else
            {
                sasBuilder.SetPermissions(BlobSasPermissions.Read);
            }

            var sasToken = blobClient.GenerateSasUri(sasBuilder).Query;

            return $"{blobClient.Uri}?{sasToken}";
        }

        
        public string RetrieveFileFromStorage(string containerName, string FileName)
        {
            try {
                
                var blobServiceClient = new BlobServiceClient(_storageConnectionString);

               
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

                if (!blobContainerClient.Exists())
                {
                    _logger.LogError($"Container '{containerName}' not found.");
                    return null; 
                }

              
                var blobClient = blobContainerClient.GetBlobClient(FileName);

                
                if (!blobClient.Exists())
                {
                    _logger.LogError($"Blob '{FileName}' not found in container '{containerName}'.");
                    return null; 
                }
                 return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving file: {ex.Message}");
                return null;
            }
        }
        public string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".webm" => "video/webm",
                ".hevc" => "video/hevc",
                _ => "application/octet-stream"
            };
        }
        
        public async Task DeleteFileFromStorage(string containerName, string fileName)
        {
            try 
            {
                var blobServiceClient = new BlobServiceClient(_storageConnectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(fileName);
                await blobClient.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting file: {ex.Message}");
            }
        }

        public async Task<IEnumerable<string>> ListFilesInContainer(string containerName)

        {
            try {
               var blobServiceClient = new BlobServiceClient(_storageConnectionString);
               var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
               var blobNames = new List<string>();
               await foreach (var blobItem in blobContainerClient.GetBlobsAsync())
                {
                    blobNames.Add(blobItem.Name);
                }
                return blobNames;

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing files: {ex.Message}");
                return null;
            }
        }

        public async Task<IEnumerable<VideoPlayerModel>> ListVideoUrlsAsync(string containerName)
        {
            var blobServiceClient = new BlobServiceClient(_storageConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("processed-videos");
            var videoMetadata = await _dbHelper.GetVideoMetadataAsync();
            var videoList = new List<VideoPlayerModel>();

            await foreach (var blobItem in blobContainerClient.GetBlobsAsync())
            {
                var blobClient = blobContainerClient.GetBlobClient(blobItem.Name);
                var videoUrl = WebUtility.UrlDecode(blobClient.Uri.AbsoluteUri);

                
                _logger.LogInformation($"Blob URL: {videoUrl}");

               
                var matchVideo = videoMetadata.FirstOrDefault(v => v.VideoUrl == videoUrl);
                
                _logger.LogInformation("Listing all video metadata URLs:");
                foreach (var video in videoMetadata)
                {
                    _logger.LogInformation($"Metadata URL: {video.VideoUrl}");
                }
                if (matchVideo != null)
                {
                   
                    _logger.LogInformation($"Match found in video metadata: Title={matchVideo.VideoTitle}, URL={matchVideo.VideoUrl}");

                    var videoModel = new VideoPlayerModel
                    {
                        Id = matchVideo.Id,
                        VideoUrl = videoUrl,
                        VideoTitle = matchVideo.VideoTitle,
                        VideoDescription = matchVideo.VideoDescription,
                        ThumbnailUrl = matchVideo.ThumbnailUrl
                    };
                    videoList.Add(videoModel);
                }
                else
                {
                    
                    _logger.LogWarning($"No match found in video metadata for URL: {videoUrl}");

                    var videoModel = new VideoPlayerModel
                    {
                        VideoUrl = videoUrl,
                        VideoTitle = "UNKNOWN TITLE",
                        VideoDescription = "UNKNOWN DESCRIPTION",
                        ThumbnailUrl = "default_thumbnail_url_here" // Replace with your default thumbnail URL
                    };
                    videoList.Add(videoModel);
                }
            }

            // Return the list of video models
            return videoList;
        }

        public async Task<string> ConvertVideoFileAsync(string inputFilePath, string outputFilePath)
        {
            outputFilePath = Path.ChangeExtension(outputFilePath, ".mp4");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputFilePath}\" -c:v libx264 -c:a aac \"{outputFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var readOutputTask = process.StandardOutput.ReadToEndAsync();
            var readErrorTask = process.StandardError.ReadToEndAsync();

            
            await Task.WhenAny(Task.Run(() => process.WaitForExit()), readOutputTask, readErrorTask);

           
            string output = await readOutputTask;
            string error = await readErrorTask;

            _logger.LogInformation($"FFmpeg output: {output}");
            if (!string.IsNullOrEmpty(error) && !error.StartsWith("ffmpeg version"))
            {
                _logger.LogError($"FFmpeg error: {error}");
                throw new InvalidOperationException("FFmpeg conversion failed.");
            }


            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("FFmpeg did not exit correctly. Exit code: " + process.ExitCode);
            }

           
            Console.WriteLine("Reached line 193");
            return outputFilePath;
        }

        public async Task<string> UploadFileToBlobAsync(string containerName, string filePath, string fileName)
        {
            try 
            {
                var blobServiceClient = new BlobServiceClient(_storageConnectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await blobContainerClient.CreateIfNotExistsAsync();

                var blobClient = blobContainerClient.GetBlobClient(fileName);

                await using var fileStream = File.OpenRead(filePath);
                await blobClient.UploadAsync(fileStream, true);

                _logger.LogInformation($"Uploaded file '{fileName}' to container '{containerName}'.");
                return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return null;
            }
        }

        
        
    }
}