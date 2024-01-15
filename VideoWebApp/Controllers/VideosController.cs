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
                if (uploadDto.File.Length > 200 * 1024 * 1024)
                {
                    return BadRequest("File size should not exceed 200 MB.");
                }

                // Check file type
                string[] allowedTypes = { "video/mp4", "video/quicktime", "video/hevc", "video/webm" };
                if (!allowedTypes.Contains(uploadDto.File.ContentType))
                {
                    return BadRequest("Invalid file type. Allowed types are MP4, MOV, HEVC, WebM");
                }
                        string containerName = "videos";

                var tempFilePath = Path.GetTempFileName();
                using (var stream = System.IO.File.Create(tempFilePath))
                    {
                        await uploadDto.File.CopyToAsync(stream);
                    }

                string convertedFileName = Path.GetFileNameWithoutExtension(uploadDto.File.FileName) + ".mp4";
                string convertedFilePath = Path.Combine(Path.GetTempPath(), convertedFileName);
                await _azureService.ConvertVideoFileAsync(tempFilePath, convertedFilePath);

                var uploadResult = await _azureService.UploadFileToBlobAsync(containerName, convertedFilePath);

                // Clean up the temporary files
                System.IO.File.Delete(tempFilePath);
                System.IO.File.Delete(convertedFilePath);

                if (uploadResult == null)
                {
                    return BadRequest("Could not upload the file");
                }
                var video = new Video
                {
                    Title = uploadDto.VideoTitle,
                    Description = uploadDto.VideoDescription,
                    VideoUrl = uploadResult
                };
            if (uploadDto.Thumbnail != null)
            {
                var thumbnailPath = Path.GetTempFileName(); // Create tmppath
                using (var stream = System.IO.File.Create(thumbnailPath))
                {
                    await uploadDto.Thumbnail.CopyToAsync(stream);
                }

                string thumbnailContainer = "thumbnails";
                var thumbnailUploadResult = await _azureService.UploadFileToBlobAsync(thumbnailContainer, thumbnailPath);
                System.IO.File.Delete(thumbnailPath);

                if (thumbnailUploadResult == null)
                {
                    return BadRequest("Could not upload the thumbnail");
                }
                video.ThumbnailUrl = thumbnailUploadResult;
            }
                

               
                _context.Videos.Add(video);
                await _context.SaveChangesAsync();

                return Ok(new { FileUrl = uploadResult });
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

            var model = new VideoPlayerModel
            {
                VideoUrl = video.VideoUrl,
                VideoTitle = video.Title,
                VideoDescription = video.Description,
                ThumbnailUrl = video.ThumbnailPath  // Ensure this property exists in your Video model
            };

            return View("WatchVideo", model);
        }




    }

}
    
