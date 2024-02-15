    using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebApp.Data;
using VideoWebApp.Interface;
using VideoWebApp.Models;
using VideoWebApp.Models.DTOs;
using System.Net.Http.Json;
using Microsoft.VisualBasic.FileIO;

namespace VideoWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideosController : Controller
    {
        private readonly IAzureService _azureService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VideosController> _logger;

        
        public VideosController(ApplicationDbContext context, IAzureService azureService, ILogger<VideosController> logger)
        {
            _context = context;
            _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
            _logger = logger;
        }

        [HttpGet]

        public async Task<ActionResult<IEnumerable<Video>>> GetVideos()
        {
            var videos = await _context.Videos.ToListAsync();
            return Ok(videos);
        }
        [HttpPost("Upload")] 
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadVideo([FromForm] VideoUploadDto uploadDto)
        {
            if (uploadDto.File == null || uploadDto.File.Length > 200 * 1024 * 1024)
            {
                return BadRequest("File size should not exceed 200 MB.");
            }

            string[] allowedTypes = { "video/mp4", "video/quicktime", "video/hevc", "video/webm" };
            if (!allowedTypes.Contains(uploadDto.File.ContentType))
            {
                return BadRequest("Invalid file type. Allowed types are MP4, MOV, HEVC, WebM");
            }

            var tempFilePath = Path.GetTempFileName();
            using (var stream = System.IO.File.Create(tempFilePath))
            {
                await uploadDto.File.CopyToAsync(stream);
            }

            var uploadResult = await _azureService.UploadFileToBlobAsync("input-videos", tempFilePath, uploadDto.File.FileName);
            if (uploadResult == null)
            {
                System.IO.File.Delete(tempFilePath);
                return BadRequest("Could not upload the file.");
            }

            System.IO.File.Delete(tempFilePath);
            string thumbnailUploadResult = null;
            if (uploadDto.Thumbnail != null)
            {
                var thumbnailTempFilePath = Path.GetTempFileName();
                using (var stream = System.IO.File.Create(thumbnailTempFilePath))
                {
                    await uploadDto.Thumbnail.CopyToAsync(stream);
                }

               
                thumbnailUploadResult = await _azureService.UploadFileToBlobAsync("thumbnails", thumbnailTempFilePath, uploadDto.Thumbnail.FileName);
                System.IO.File.Delete(thumbnailTempFilePath);

                if (thumbnailUploadResult == null)
                {
                    return BadRequest("Could not upload the thumbnail.");
                }
            }
            else
            {
                return BadRequest("Thumbnail file is missing.");
            }



            string fileType = uploadDto.FileType ?? "video";
            string azureFunctionUrl = "https://c2ca-2001-2042-300d-6400-45dd-15b7-856-b40d.ngrok-free.app/api/Function1"; 
            string processedVideoUrl = string.Empty; 
            using (HttpClient httpClient = new HttpClient())
            {
                var response = await httpClient.PostAsJsonAsync(
                azureFunctionUrl,
                new
                {
                    videoUrl = uploadResult,
                    name = Path.GetFileNameWithoutExtension(uploadDto.File.FileName),
                    fileType = fileType
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error calling Azure Function");
                    return StatusCode(500, "Error processing the video");
                }

                var functionResponse = await response.Content.ReadFromJsonAsync<AzureFunctionResponse>();
                processedVideoUrl = functionResponse?.Url;
            }
            
            var video = new Video
            {
                Title = uploadDto.VideoTitle,
                Description = uploadDto.VideoDescription,
                VideoUrl = processedVideoUrl,
                ThumbnailUrl = thumbnailUploadResult
            };

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            return Ok(new { processedVideoUrl, thumbnailUrl = thumbnailUploadResult });

        }
        private string DetermineContainer(string fileType)
        {
            return fileType switch
            {
                "video" => "videos",
                "thumbnail" => "thumbnails",
                "recording" => "recordings", 
                _ => throw new ArgumentException("Invalid file type", nameof(fileType))
            };
        }

        private async Task<string> ProcessFileUpload(IFormFile file, string fileType)
        {
            _logger.LogInformation($"Processing file upload. File type: {fileType}.");

            string containerName;
            try
            {
                containerName = DetermineContainer(fileType);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid file type encountered in ProcessFileUpload.");
                throw; 
            }

            var tempFilePath = Path.GetTempFileName();
            using (var stream = System.IO.File.Create(tempFilePath))
            {
                await file.CopyToAsync(stream);
            }

            var convertedFileName = Path.GetFileNameWithoutExtension(file.FileName) + ".mp4";
            var convertedFilePath = Path.Combine(Path.GetTempPath(), convertedFileName);
            await _azureService.ConvertVideoFileAsync(tempFilePath, convertedFilePath);

            var uploadResult = await _azureService.UploadFileToBlobAsync(containerName, convertedFilePath, convertedFileName);

            System.IO.File.Delete(tempFilePath);
            System.IO.File.Delete(convertedFilePath);

            return uploadResult;
        }


        [HttpGet("Retrieve/{fileName}")]
        public IActionResult RetrieveVideo(string containerName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("Container name and file name must be provided.");
            }

            var fileUrl = _azureService.RetrieveFileFromStorage(containerName, fileName);

            if (fileUrl == null)
            {
                return NotFound($"File '{fileName}' not found in container '{containerName}'.");
            }

            return Ok(new { Url = fileUrl });
        }

        [HttpGet("Player/{id}")]    
        public IActionResult VideoPlayer(int id)
        {
            var video = _context.Videos.FirstOrDefault(v => v.Id == id);
            if (video == null)
            {
                return NotFound();
            }

            if (video.ThumbnailUrl == null)
            {
                return BadRequest("Thumbnail is null.");
            }

            var model = new VideoPlayerModel
            {
                VideoUrl = video.VideoUrl,
                VideoTitle = video.Title,
                VideoDescription = video.Description,
                ThumbnailUrl = video.ThumbnailUrl  
            };

            return View("WatchVideo", model);
        }
        public class AzureFunctionResponse
        {
            public string Url { get; set; }
        }




    }

}
    
