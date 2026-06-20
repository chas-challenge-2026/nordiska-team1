using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NordiskaPortal.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        // Clear session and redirect — no server-side session invalidation
        // Session token remains valid until it expires (set to 365 days in Program.cs)
        HttpContext.Session.Clear();
        return RedirectToPage("/Index");
    }
}
