using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;

namespace VideoWebappFfmpeg
{
    public static class Function1
    {
        private static readonly string BlobStorageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        private static readonly string FfmpegExecutablePath = Environment.GetEnvironmentVariable("FfmpegPath");

        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string videoUrl = data?.videoUrl;
            string originalName = data?.name;
            string fileType = data?.fileType;

            if (string.IsNullOrEmpty(videoUrl) || string.IsNullOrEmpty(originalName))
            {
                log.LogError("Video URL or original name is not provided in the request body.");
                return new BadRequestObjectResult("Video URL or original name is not provided in the request body.");
            }

            string localInputPath = Path.GetTempFileName();
            string outputFileName = $"{originalName}.mp4";
            //string outputFileName = $"{Guid.NewGuid()}_{originalName}.mp4";
            string localOutputPath = Path.Combine(Path.GetTempPath(), outputFileName);

            try
            {
                log.LogInformation($"Processing video from URL: {videoUrl}");
                await DownloadVideoFromBlobAsync(videoUrl, localInputPath, log);

                if (!await ExecuteFfmpegAsync(FfmpegExecutablePath, localInputPath, localOutputPath, log))
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                string containerName = DetermineContainer(fileType);
                string processedVideoUrl = await UploadFileToBlobAsync(localOutputPath, containerName, outputFileName, log);

                return new OkObjectResult(new { url = processedVideoUrl, name = originalName });
            }
            catch (Exception ex)
            {
                log.LogError($"Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            finally
            {
                CleanupTemporaryFiles(new[] { localInputPath, localOutputPath }, log);
            }
        }

        private static string DetermineContainer(string fileType)
        {
            switch (fileType.ToLower())
            {
                case "video":
                    return "processed-videos";
                case "recording":
                    return "recordings";
                default:
                    throw new ArgumentException("Unsupported file type");
            }
        }

        private static async Task<string> DownloadVideoFromBlobAsync(string videoUrl, string tempPath, ILogger log)
        {
            try
            {
                
                Uri blobUri = new Uri(videoUrl);
                BlobServiceClient blobServiceClient = new BlobServiceClient(BlobStorageConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobUri.LocalPath.Split('/')[1]);
                BlobClient blobClient = containerClient.GetBlobClient(blobUri.LocalPath.Substring(blobUri.LocalPath.IndexOf('/', 1)).TrimStart('/'));

                await blobClient.DownloadToAsync(tempPath);
                log.LogInformation($"Successfully downloaded video from {videoUrl} to {tempPath}.");
                return tempPath;
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to download video from {videoUrl}: {ex.Message}");
                throw;
            }
        }

        private static async Task<bool> ExecuteFfmpegAsync(string ffmpegPath, string inputPath, string outputPath, ILogger log)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = $"-i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 \"{outputPath}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        log.LogInformation(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                        log.LogError(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var exited = await Task.Run(() => process.WaitForExit(30000)); // 30-second timeout
                if (!exited)
                {
                    log.LogError("FFmpeg processing timed out.");
                    process.Kill();
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    log.LogError($"FFmpeg processing failed with exit code {process.ExitCode}.");
                    log.LogError($"FFmpeg error: {error.ToString()}");
                    return false;
                }

                log.LogInformation($"FFmpeg processing completed successfully.");
                return true;
            }
        }

        private static async Task<string> UploadFileToBlobAsync(string filePath, string containerName, string blobName, ILogger log)
        {
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(BlobStorageConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.UploadAsync(filePath, overwrite: true);
                log.LogInformation("Processed file uploaded successfully.");
                return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                log.LogError($"Error uploading to container '{containerName}' and blob '{blobName}': {ex.Message}");
                throw;
            }
        }

        private static void CleanupTemporaryFiles(string[] filePaths, ILogger log)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        log.LogInformation($"Deleted temporary file: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    log.LogError($"Failed to delete temporary file '{filePath}': {ex.Message}");
                }
            }
        }
    }
}
