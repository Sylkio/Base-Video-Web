using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebApp.Data;
using VideoWebApp.Interface;
using VideoWebApp.Models;


namespace VideoWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideosController : ControllerBase
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
        public async Task<IActionResult> UploadVideo(string containerName, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
            return BadRequest("No file selected");
            }
            try
            {
               var tempFilePath = Path.GetTempFileName();
               using (var stream = System.IO.File.Create(tempFilePath))
               {
                   await file.CopyToAsync(stream);
               }

               var uploadResult = await _azureService.UploadFileToBlobAsync(containerName, tempFilePath);

               //Clean up the temporary file

               System.IO.File.Delete(tempFilePath);

               //Handle the result

                if (uploadResult == null)
                {
                     return BadRequest("Could not upload the file");
                }
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
            if (String.IsNullOrEmpty(containerName) || String.IsNullOrEmpty(fileName));
            {
                return BadRequest("Container name or file name is null or empty.");
            }
        }
        

        
        
    }

    
}