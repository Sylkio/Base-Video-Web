using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebApp.Data;
using VideoWebApp.Interface;
using VideoWebApp.Models;
using VideoWebApp.Models.DTOs;


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

            // Check file type
            string[] allowedTypes = { "video/mp4", "video/quicktime", "video/hevc", "video/webm" };
            if (!allowedTypes.Contains(uploadDto.File.ContentType))
            {
                return BadRequest("Invalid file type. Allowed types are MP4, MOV, HEVC, WebM");
            }

            var video = new Video
            {
                Title = uploadDto.VideoTitle,
                Description = uploadDto.VideoDescription
            };

            // Assuming FileType is being passed correctly from the frontend
            _logger.LogInformation($"Received FileType: {uploadDto.FileType}");

            // Process file upload
            string containerName = DetermineContainer(uploadDto.FileType);
            var fileUploadResult = await ProcessFileUpload(uploadDto.File, uploadDto.FileType);
            if (fileUploadResult == null)
            {
                return BadRequest("Could not upload the file.");
            }

            if (uploadDto.FileType == "video")
            {
                video.VideoUrl = fileUploadResult;
            }
            else if (uploadDto.FileType == "thumbnail")
            {
                video.ThumbnailUrl = fileUploadResult;
            }
            else if (uploadDto.FileType == "recording")
            {
                video.RecordingUrl = fileUploadResult;
            }

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            return Ok(new { VideoUrl = video.VideoUrl, ThumbnailUrl = video.ThumbnailUrl, RecordingUrl = video.RecordingUrl });
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
                throw; // or handle this scenario more gracefully if appropriate
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

            // Clean up the temporary files
            System.IO.File.Delete(tempFilePath);
            System.IO.File.Delete(convertedFilePath);

            return uploadResult;
        }


        [HttpGet("Retrieve/{fileName}")]
        public IActionResult RetrieveVideo(string containerName, string fileName)
        {
            // Step 1: Validate input parameters
            if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("Container name and file name must be provided.");
            }

            // Step 2: Call _azureService.RetrieveFileFromStorage
            var fileUrl = _azureService.RetrieveFileFromStorage(containerName, fileName);

            // Step 3: Check if the returned URI is null
            if (fileUrl == null)
            {
                return NotFound($"File '{fileName}' not found in container '{containerName}'.");
            }

            // Step 4: Return the file URI
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




    }

}
    
