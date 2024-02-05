using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VideoWebApp.Interface;
using VideoWebApp.Models;
using VideoWebApp.Services;

namespace VideoWebapp.Pages
{
    public class WatchVideoModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public int VideoId { get; set; }

        public VideoPlayerModel SelectedVideo;

        private readonly string _storageContainerName = "input-videos";

        private readonly IAzureService _azureService;
        public List<VideoPlayerModel> Videos { get; private set; }

        public WatchVideoModel(IAzureService azureService)
        {
            _azureService = azureService;
        }

        public async Task OnGetAsync()
        {
            Videos = (await _azureService.ListVideoUrlsAsync(_storageContainerName)).ToList();

            SelectedVideo = Videos.FirstOrDefault(video => video.Id == VideoId);
        }
    }
}