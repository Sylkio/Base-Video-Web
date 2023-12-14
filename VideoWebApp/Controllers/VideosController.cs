using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        
        public VideosController(ApplicationDbContext context, IAzureService azureService)
        {
            _context = context;
            _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
        }

        [HttpGet]

        public async Task<ActionResult<IEnumerable<Video>>> GetVideos()
        {
            var videos = await _context.Videos.ToListAsync();
            return Ok(videos);
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> UploadVideo([FromForm] VideoUploadDto uploadDto)
        {
            string containerName = "videos";
            try
            {
               var tempFilePath = Path.GetTempFileName();
               using (var stream = System.IO.File.Create(tempFilePath))
               {
                   await uploadDto.File.CopyToAsync(stream);
               }

                var uploadResult = await _azureService.UploadFileToBlobAsync(containerName, tempFilePath);

               //Clean up the temporary file

               System.IO.File.Delete(tempFilePath);

               //Handle the result

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
                _context.Videos.Add(video);
                await _context.SaveChangesAsync();

                return Ok(new { FileUrl = uploadResult });

            }
            catch (Exception ex)
            {
                 return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading the file.");
            }

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

        [HttpGet("Player/{fileName}")]
        public IActionResult VideoPlayer(string containerName, string fileName)
        {
            var fileUrl = _azureService.RetrieveFileFromStorage(containerName, fileName);
            if (fileUrl == null)
            {
                return NotFound();
            }

            return View("WatchVideo", new VideoPlayerModel { VideoUrl = fileUrl });
        }
        
        
        
    }

    
}