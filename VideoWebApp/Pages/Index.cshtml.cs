using Microsoft.AspNetCore.Mvc.RazorPages;
using VideoWebApp.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VideoWebApp.Pages
{
    public class IndexModel : PageModel
    {        
            
        private readonly string _storageContainerName = "videos";
        
        private readonly IAzureService _azureService;
        public List<string> VideoUrls { get; private set; }

        public IndexModel(IAzureService azureService)
        {
            _azureService = azureService;
        }

        public async Task OnGetAsync()
        {
            VideoUrls = (await _azureService.ListVideoUrlsAsync(_storageContainerName)).ToList();
        }
    }
}
