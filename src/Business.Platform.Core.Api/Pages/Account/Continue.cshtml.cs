using Business.Platform.Core.Api.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Business.Platform.Core.Api.Pages.Account;
[Authorize(Policy=WebComposition.Policy)]
public sealed class ContinueModel:PageModel
{
    public IActionResult OnGet()=>StatusCode(405);
    public async Task<IActionResult> OnPostAsync()
    {
        var auth=await HttpContext.AuthenticateAsync("Web");
        if(!auth.Succeeded)return Challenge("Web");
        var properties=auth.Properties!;properties.IsPersistent=false;properties.IssuedUtc=DateTimeOffset.UtcNow;properties.ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(30);
        await HttpContext.SignInAsync("Web",auth.Principal!,properties);
        return LocalRedirect("/app");
    }
}
