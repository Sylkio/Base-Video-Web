using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace VideoWebApp.Models
{
    public class Video
    {
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; } 
        public string? VideoUrl { get; set; }
        public string? Description { get; set; }
        public string? ThumbnailPath { get; set; }
        public string? Category { get; set; }
        public TimeSpan? Duration { get; set; }
        public int Views { get; set; }
        public DateTime UploadDate { get; set; }
        public string? Uploader { get; set; }
        public string? BlobStorageUri { get; set; }

    }
}