using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VideoWebapp.Pages
{
    public class LiveStreamModel : PageModel
    {
        public IActionResult OnPost()
        {
            return Redirect($"/LiveRoom/{Guid.NewGuid()}");
        }
    }
}
