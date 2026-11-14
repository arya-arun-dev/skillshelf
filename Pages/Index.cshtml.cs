using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkillsMarketplace.Models;
using SkillsMarketplace.Services;

namespace SkillsMarketplace.Pages;

public sealed class IndexModel(SkillRepository catalog) : PageModel
{
    // SupportsGet reads ?q=... from the URL submitted by the search form.
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    public IReadOnlyList<Skill> Skills { get; private set; } = [];

    public void OnGet()
    {
        Skills = catalog.Search(Query);
    }
}
