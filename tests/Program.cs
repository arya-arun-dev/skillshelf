using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using SkillsMarketplace.Services;

// Executable behavior checks without an additional test-framework dependency.
var validator = new PackageValidator();
int passed = 0;
int failed = 0;

byte[] Zip(params (string Path, byte[] Content)[] entries)
{
    using var output = new MemoryStream();
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        foreach (var entry in entries)
        {
            using var stream = zip.CreateEntry(entry.Path).Open();
            stream.Write(entry.Content);
        }
    return output.ToArray();
}

byte[] Text(string value) => Encoding.UTF8.GetBytes(value);
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Reject(byte[] bytes)
{
    try { validator.Validate(bytes); }
    catch (PackageValidationException) { return; }
    throw new Exception("Expected package rejection.");
}
async Task Check(string name, Func<Task> action)
{
    try { await action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception ex) { Console.WriteLine($"FAIL {name}: {ex.Message}"); failed++; }
}
Task Sync(Action action) { action(); return Task.CompletedTask; }

await Check("SQLite engine is at least 3.50.2", () => Sync(() =>
{
    using var db = new SqliteConnection("Data Source=:memory:");
    db.Open();
    using var command = db.CreateCommand();
    command.CommandText = "SELECT sqlite_version();";
    var engine = Convert.ToString(command.ExecuteScalar()) ?? "";
    Require(Version.TryParse(engine, out var version) && version >= new Version(3, 50, 2),
        $"SQLite {engine} is older than the minimum fixed engine version 3.50.2.");
}));

await Check("Root instructions and nested support file", () => Sync(() =>
{
    var result = validator.Validate(Zip(("SKILL.md", Text("# Review")), ("scripts/check.py", Text("print('example')"))));
    Require(result.Instructions == "# Review", "Instructions changed.");
}));
await Check("Single enclosing skill folder", () => Sync(() =>
{
    var result = validator.Validate(Zip(("review/SKILL.md", Text("# Review")), ("review/docs/notes.md", Text("Notes"))));
    Require(result.InstructionPath == "review/SKILL.md", "Wrong instruction path.");
}));
await Check("Malformed ZIP rejected", () => Sync(() => Reject(Text("not a ZIP"))));
await Check("Missing instructions rejected", () => Sync(() => Reject(Zip(("README.md", Text("Notes"))))));
await Check("Empty instructions rejected", () => Sync(() => Reject(Zip(("SKILL.md", Text("   "))))));
await Check("Non-UTF8 instructions rejected", () => Sync(() => Reject(Zip(("SKILL.md", new byte[] { 0xff, 0xfe, 0xff })))));
await Check("Traversal path rejected", () => Sync(() => Reject(Zip(("SKILL.md", Text("Review")), ("../outside", Text("bad"))))));
await Check("Absolute path rejected", () => Sync(() => Reject(Zip(("/SKILL.md", Text("Review"))))));
await Check("Backslash path rejected", () => Sync(() => Reject(Zip(("review\\SKILL.md", Text("Review"))))));
await Check("Multiple instruction files rejected", () => Sync(() => Reject(Zip(("SKILL.md", Text("Review")), ("other/SKILL.md", Text("Other"))))));
await Check("Case-colliding paths rejected", () => Sync(() => Reject(Zip(("SKILL.md", Text("Review")), ("skill.md", Text("Other"))))));
await Check("Deep instruction folder rejected", () => Sync(() => Reject(Zip(("one/two/SKILL.md", Text("Review"))))));
await Check("Files outside enclosing folder rejected", () => Sync(() => Reject(Zip(("review/SKILL.md", Text("Review")), ("outside.txt", Text("Other"))))));
await Check("Instruction size limit enforced", () => Sync(() => Reject(Zip(("SKILL.md", new byte[PackageValidator.MaxInstructionsBytes + 1])))));
await Check("Compressed file size limit enforced", () => Sync(() => Reject(new byte[PackageValidator.MaxZipBytes + 1])));
await Check("Expanded package limit enforced", () => Sync(() => Reject(Zip(("SKILL.md", Text("Review")), ("large.bin", new byte[PackageValidator.MaxExpandedBytes + 1])))));
await Check("Entry count limit enforced", () => Sync(() =>
{
    var entries = Enumerable.Range(0, 101).Select(i => ($"file-{i}.txt", Text("x"))).ToArray();
    Reject(Zip(entries));
}));
await Check("Symbolic link rejected", () => Sync(() =>
{
    using var output = new MemoryStream();
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
    {
        var entry = zip.CreateEntry("SKILL.md");
        entry.ExternalAttributes = unchecked((int)0xA1FF0000);
        using var stream = entry.Open();
        stream.Write(Text("target"));
    }
    Reject(output.ToArray());
}));

await Check("Storage restart, literal search, and byte-identical package", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "skillshelf-check-" + Guid.NewGuid().ToString("N"));
    try
    {
        var bytes = Zip(("SKILL.md", Text("# Review\nCheck boundary cases.")));
        var first = new SkillRepository(directory);
        var id = await first.PublishAsync("Review 100%", "Check unfamiliar code", "Example author", validator.Validate(bytes), bytes);
        var reopened = new SkillRepository(directory);
        Require(reopened.Find(id)?.Name == "Review 100%", "Record did not survive reopening.");
        Require(reopened.Search("REVIEW").Count == 1, "Name search failed.");
        Require(reopened.Search("unfamiliar").Count == 1, "Description search failed.");
        Require(reopened.Search("%").Count == 1, "Percent should match literally.");
        Require(reopened.Search("_").Count == 0, "Underscore should match literally.");
        Require(reopened.Search("' OR 1=1 --").Count == 0, "SQL-like query should remain data.");
        Require(reopened.Find(Guid.NewGuid().ToString("N")) is null, "Unknown ID should be missing.");
        var storedBytes = await File.ReadAllBytesAsync(reopened.PackagePath(id));
        Require(bytes.SequenceEqual(storedBytes), "Stored ZIP bytes changed.");
        try { reopened.PackagePath("../outside"); throw new Exception("Unsafe ID accepted."); }
        catch (ArgumentException) { }
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
});

// Exercise the actual bundled packages, not synthetic substitutes.
var samplesDirectory = Path.Combine(AppContext.BaseDirectory, "samples");

async Task InTemporaryDirectory(Func<string, Task> action)
{
    var directory = Path.Combine(Path.GetTempPath(), "skillshelf-seed-check-" + Guid.NewGuid().ToString("N"));
    try { await action(directory); }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}

await Check("All three bundled ZIPs contain valid instructions and supporting files", async () =>
{
    Require(SampleSeeder.Samples.Count == 3, "Expected three sample definitions.");
    Require(SampleSeeder.Samples.Select(s => s.Id).Distinct().Count() == 3, "Sample IDs must be unique.");
    foreach (var sample in SampleSeeder.Samples)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(samplesDirectory, sample.FileName));
        var validated = validator.Validate(bytes);
        Require(validated.Instructions.Contains("##"), "Sample instructions should have useful sections.");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Require(zip.Entries.Count(e => !e.FullName.EndsWith('/')) >= 2, "Sample needs a supporting document.");
    }
});

await Check("Fresh startup seeds three real downloadable packages", () => InTemporaryDirectory(async directory =>
{
    var repository = new SkillRepository(directory);
    await new SampleSeeder(repository, validator).SeedAsync(samplesDirectory);
    Require(repository.Search(null).Count == 3, "Fresh collection should contain three samples.");
    foreach (var sample in SampleSeeder.Samples)
    {
        var stored = repository.Find(sample.Id);
        Require(stored is not null && stored.Name == sample.Name && stored.Author == SampleSeeder.Author,
            "Sample metadata is missing or incorrect.");
        var original = await File.ReadAllBytesAsync(Path.Combine(samplesDirectory, sample.FileName));
        var downloaded = await File.ReadAllBytesAsync(repository.PackagePath(sample.Id));
        Require(original.SequenceEqual(downloaded), "Sample ZIP bytes changed during seeding.");
    }
}));

await Check("Restarting does not duplicate or replace sample records", () => InTemporaryDirectory(async directory =>
{
    var first = new SkillRepository(directory);
    await new SampleSeeder(first, validator).SeedAsync(samplesDirectory);
    var before = first.Search(null).ToDictionary(skill => skill.Id);
    var reopened = new SkillRepository(directory);
    await new SampleSeeder(reopened, validator).SeedAsync(samplesDirectory);
    Require(reopened.Search(null).Count == 3, "Restart created duplicate samples.");
    foreach (var sample in SampleSeeder.Samples)
    {
        Require(reopened.Find(sample.Id) == before[sample.Id], "Restart replaced an existing sample record.");
        var original = await File.ReadAllBytesAsync(Path.Combine(samplesDirectory, sample.FileName));
        var stored = await File.ReadAllBytesAsync(reopened.PackagePath(sample.Id));
        Require(original.SequenceEqual(stored), "Restart changed a stored sample ZIP.");
    }
}));

await Check("Seeding an existing collection preserves user uploads", () => InTemporaryDirectory(async directory =>
{
    var repository = new SkillRepository(directory);
    var bytes = Zip(("SKILL.md", Text("# My existing skill\nKeep this package.")));
    var id = await repository.PublishAsync("My existing skill", "An existing upload", "Existing author",
        validator.Validate(bytes), bytes);
    var before = repository.Find(id);
    await new SampleSeeder(repository, validator).SeedAsync(samplesDirectory);
    await new SampleSeeder(repository, validator).SeedAsync(samplesDirectory);
    Require(repository.Search(null).Count == 4, "Expected the existing upload plus three samples.");
    Require(repository.Find(id) == before, "Existing uploaded metadata changed.");
    var after = await File.ReadAllBytesAsync(repository.PackagePath(id));
    Require(bytes.SequenceEqual(after), "Existing uploaded bytes changed.");
}));

Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
