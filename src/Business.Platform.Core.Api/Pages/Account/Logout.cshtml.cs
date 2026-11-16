using Business.Platform.Core.Api.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Business.Platform.Core.Api.Pages.Account;
[AllowAnonymous]
public sealed class LogoutModel:PageModel
{
    public bool Ended {get;private set;}
    public async Task OnGetAsync()=>Ended=!(await HttpContext.AuthenticateAsync("Web")).Succeeded;
    public async Task<IActionResult> OnPostAsync()
    {
        var auth=await HttpContext.AuthenticateAsync("Web");
        await HttpContext.SignOutAsync("Web");Response.Headers["Clear-Site-Data"]="\"cache\", \"storage\"";
        if(auth.Succeeded) {
            try {await HttpContext.SignOutAsync("WebOidc",new AuthenticationProperties{RedirectUri="/account/logout"});return new EmptyResult();}
            catch(Exception error) when(error is HttpRequestException or InvalidOperationException) {return RedirectToPage();}
        }
        return RedirectToPage();
    }
}
