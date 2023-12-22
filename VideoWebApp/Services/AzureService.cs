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
        /*public async Task<IEnumerable<string>> ListVideoUrlsAsync(string containerName)
        {
            var blobServiceClient = new BlobServiceClient(_storageConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var videoUrls = new List<string>();

            await foreach (var blobItem in blobContainerClient.GetBlobsAsync())
            {
                var blobClient = blobContainerClient.GetBlobClient(blobItem.Name);
                videoUrls.Add(blobClient.Uri.AbsoluteUri);
            }

            return videoUrls;
        }*/

        public async Task<IEnumerable<VideoPlayerModel>> ListVideoUrlsAsync(string containerName)
        {
            var blobServiceClient = new BlobServiceClient(_storageConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var videoMetadata = await _dbHelper.GetVideoMetadataAsync();
            var videoList = new List<VideoPlayerModel>();

            await foreach (var blobItem in blobContainerClient.GetBlobsAsync())
            {
                var blobClient = blobContainerClient.GetBlobClient(blobItem.Name);
                var videoUrl = blobClient.Uri.AbsoluteUri;

                var matchVideo = videoMetadata.FirstOrDefault(v => v.VideoUrl == videoUrl);
                if (matchVideo != null)
                {
                    var videoModel = new VideoPlayerModel
                    {
                        VideoUrl = videoUrl,
                        VideoTitle = matchVideo.VideoTitle,
                        VideoDescription = matchVideo.VideoDescription
                    };
                    videoList.Add(videoModel);
                }
                else
                {
                    var videoModel = new VideoPlayerModel
                    {
                        VideoUrl = videoUrl,
                        VideoTitle = "UNKNOWN TITLE",
                        VideoDescription = "UNKNOWN DESCRIPTION"
                    };
                    videoList.Add(videoModel);
                }
            }

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

            // Start FFmpeg process
            process.Start();

            // Read output and error asynchronously
            var readOutputTask = process.StandardOutput.ReadToEndAsync();
            var readErrorTask = process.StandardError.ReadToEndAsync();

            // Wait for FFmpeg process to complete
            await Task.WhenAny(Task.Run(() => process.WaitForExit()), readOutputTask, readErrorTask);

            // Ensure both stdout and stderr have been read
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

            // Log and return the output file path
            Console.WriteLine("Reached line 193");
            return outputFilePath;
        }

        public async Task<string> UploadFileToBlobAsync(string containerName, string convertedFilePath )
        {
            try 
            {
                var blobServiceClient = new BlobServiceClient(_storageConnectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await blobContainerClient.CreateIfNotExistsAsync();

                var fileName = Path.GetFileName(convertedFilePath );
                var blobClient = blobContainerClient.GetBlobClient(fileName);

                // checks if it's type MP4, MOV, HEVC and WebM up to 200 MB
                // Upload file
                await using var fileStream = File.OpenRead(convertedFilePath );
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