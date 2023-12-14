using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VideoWebApp.Models.DTOs
{
    public class VideoUploadDto
    {
        public IFormFile File { get; set; }
        public string VideoTitle  { get; set; }
        public string VideoDescription { get; set; }
        
    }
}