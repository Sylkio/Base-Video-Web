using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VideoWebapp.Pages
{
    public class LiveRoomModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string RoomId { get; set; }

        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(RoomId))
            {
                return RedirectToPage("/Index");
            }

            return Page();
        }
    }
}
