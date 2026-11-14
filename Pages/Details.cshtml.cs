using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkillsMarketplace.Models;
using SkillsMarketplace.Services;

namespace SkillsMarketplace.Pages;

public sealed class DetailsModel(SkillRepository repository) : PageModel
{
    public Skill Skill { get; private set; } = null!;

    public IActionResult OnGet(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) return NotFound();
        var skill = repository.Find(id);
        if (skill is null) return NotFound();
        Skill = skill;
        return Page();
    }

    public IActionResult OnGetDownload(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) return NotFound();
        var skill = repository.Find(id);
        if (skill is null) return NotFound();
        try
        {
            var stream = new FileStream(repository.PackagePath(id), FileMode.Open, FileAccess.Read, FileShare.Read);
            // Server-generated name avoids reflecting the uploaded filename into headers.
            return File(stream, "application/zip", $"skill-{id}.zip");
        }
        catch (FileNotFoundException) { return NotFound(); }
        catch (DirectoryNotFoundException) { return NotFound(); }
    }
}
