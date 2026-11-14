using SkillsMarketplace.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<PackageValidator>();
builder.Services.AddSingleton<SampleSeeder>();
builder.Services.AddSingleton(_ => new SkillRepository(
    Path.Combine(builder.Environment.ContentRootPath, "App_Data")));

var app = builder.Build();
// Initialize storage and add missing bundled examples before accepting requests.
await app.Services.GetRequiredService<SampleSeeder>().SeedAsync(
    Path.Combine(app.Environment.ContentRootPath, "samples"));
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
