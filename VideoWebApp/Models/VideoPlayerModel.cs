using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace VideoWebApp.Models
{
    public class VideoPlayerModel
    {
        [Key]
        public int Id { get; set; }
       public string VideoUrl { get; set; } 
       public string VideoTitle { get; set; }
        public string VideoDescription { get; set; }
    }
}