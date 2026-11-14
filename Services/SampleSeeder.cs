namespace SkillsMarketplace.Services;

public sealed record BundledSample(string Id, string Name, string Description, string FileName);

public sealed class SampleSeeder(SkillRepository repository, PackageValidator validator)
{
    public const string Author = "SkillShelf Examples";

    // IDs are stable across releases. Matching names alone never identify samples.
    public static IReadOnlyList<BundledSample> Samples { get; } = Array.AsReadOnly(new[]
    {
        new BundledSample("531091c5d8924e5bb2101735eb0dd101", "Review a code change",
            "Review changes for correctness, boundary cases, and missing tests using a structured review outline.",
            "review-skill.zip"),
        new BundledSample("531091c5d8924e5bb2101735eb0dd102", "Plan meaningful tests",
            "Build a focused test plan covering observable behavior, boundary cases, invalid inputs, and important failures.",
            "plan-meaningful-tests.zip"),
        new BundledSample("531091c5d8924e5bb2101735eb0dd103", "Explain a codebase",
            "Trace a project's entry point, main components, and a representative workflow with references to the source.",
            "explain-codebase.zip")
    });

    public async Task SeedAsync(string samplesDirectory)
    {
        foreach (var sample in Samples)
        {
            if (repository.Find(sample.Id) is not null) continue;
            var bytes = await File.ReadAllBytesAsync(Path.Combine(samplesDirectory, sample.FileName));
            var validated = validator.Validate(bytes);
            await repository.AddSampleIfMissingAsync(sample.Id, sample.Name, sample.Description,
                Author, validated, bytes);
        }
    }
}
