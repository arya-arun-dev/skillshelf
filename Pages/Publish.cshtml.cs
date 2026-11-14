using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using SkillsMarketplace.Services;

namespace SkillsMarketplace.Pages;

[RequestSizeLimit(6 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
public sealed class PublishModel(SkillRepository repository, PackageValidator validator,
    ILogger<PublishModel> logger) : PageModel
{
    [BindProperty, Required, StringLength(80)]
    public string Name { get; set; } = "";
    [BindProperty, Required, StringLength(500)]
    public string Description { get; set; } = "";
    [BindProperty, Required, StringLength(80)]
    public string Author { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Choose a ZIP package.")]
    public IFormFile? Package { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (Package is null || Package.Length == 0 || Package.Length > PackageValidator.MaxZipBytes ||
            !Path.GetExtension(Package.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Package), "Choose a nonempty .zip file of at most 5 MiB.");
            return Page();
        }

        try
        {
            // Bound the copy independently of the client-supplied file length.
            using var input = Package.OpenReadStream();
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = await input.ReadAsync(buffer)) != 0)
            {
                if (output.Length + count > PackageValidator.MaxZipBytes)
                    throw new PackageValidationException("ZIP size must not exceed 5 MiB.");
                await output.WriteAsync(buffer.AsMemory(0, count));
            }
            var bytes = output.ToArray();
            var validated = validator.Validate(bytes);
            var id = await repository.PublishAsync(Name, Description, Author, validated, bytes);
            return RedirectToPage("/Details", new { id });
        }
        catch (PackageValidationException ex)
        {
            ModelState.AddModelError(nameof(Package), ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            logger.LogError(ex, "Failed to store an uploaded skill.");
            ModelState.AddModelError("", "The skill could not be saved. Check available disk space and folder permissions, then try again.");
        }
        return Page();
    }
}
