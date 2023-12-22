using Microsoft.EntityFrameworkCore;
using VideoWebApp.Data;
using VideoWebApp.Models;

namespace VideoWebapp.DbHelper
{
    public class DbHelper
    {
        private readonly ApplicationDbContext _context;

        public DbHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<VideoPlayerModel>> GetVideoMetadataAsync()
        {
            // Assuming you have a DbSet<Video> in your context
            return await _context.Videos.Select(v => new VideoPlayerModel
            {
                VideoUrl = v.VideoUrl,
                VideoTitle = v.Title,
                VideoDescription = v.Description
            }).ToListAsync();
        }
    }
}
