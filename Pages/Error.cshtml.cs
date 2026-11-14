using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SkillsMarketplace.Pages;

[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    public int Status { get; private set; }
    public void OnGet(int? code) => SetStatus(code);
    public void OnPost(int? code) => SetStatus(code);
    private void SetStatus(int? code)
    {
        Status = code is >= 400 and <= 599 ? code.Value : 500;
        Response.StatusCode = Status;
    }
}
