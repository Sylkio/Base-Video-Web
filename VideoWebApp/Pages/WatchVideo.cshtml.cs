using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VideoWebApp.Interface;
using VideoWebApp.Models;

namespace VideoWebapp.Pages
{
    public class WatchVideoModel : PageModel
    {
        private readonly string _storageContainerName = "videos";
        
        private readonly IAzureService _azureService;
        public List<VideoPlayerModel> Videos { get; private set; }

        public WatchVideoModel(IAzureService azureService)
        {
            _azureService = azureService;
        }

        public async Task OnGetAsync()
        {
            Videos = (await _azureService.ListVideoUrlsAsync(_storageContainerName)).ToList();
        }
    }
}