using System.IO.Compression;
using System.Text;

namespace SkillsMarketplace.Services;

public sealed class PackageValidationException(string message) : Exception(message) { }
public sealed record ValidatedPackage(string Instructions, string InstructionPath);

// Packages are read in memory within fixed limits. Never extract or execute them.
public sealed class PackageValidator
{
    public const int MaxZipBytes = 5 * 1024 * 1024;
    public const int MaxExpandedBytes = 20 * 1024 * 1024;
    public const int MaxInstructionsBytes = 256 * 1024;
    public const int MaxEntries = 100;

    public ValidatedPackage Validate(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxZipBytes)
            throw new PackageValidationException("Choose a nonempty ZIP of at most 5 MiB.");
        try
        {
            using var input = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntries)
                throw new PackageValidationException("A package must contain 1–100 entries.");

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<ZipArchiveEntry>();
            long declaredTotal = 0;
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName;
                var parts = path.TrimEnd('/').Split('/');
                if (string.IsNullOrEmpty(path) || path.StartsWith('/') || path.Contains('\\') ||
                    path.Any(c => char.IsControl(c) || ":*?\"<>|".Contains(c)) ||
                    parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ')))
                    throw new PackageValidationException("The ZIP contains an unsafe or invalid path.");
                if (!paths.Add(path.TrimEnd('/')))
                    throw new PackageValidationException("The ZIP contains duplicate paths.");
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new PackageValidationException("Symbolic links are not accepted.");
                if (entry.Length > MaxExpandedBytes || declaredTotal > MaxExpandedBytes - entry.Length)
                    throw new PackageValidationException("Expanded package size must not exceed 20 MiB.");
                declaredTotal += entry.Length;
                if (!path.EndsWith('/')) files.Add(entry);
            }

            var instructions = files.Where(e => e.FullName.Split('/').Last() == "SKILL.md").ToArray();
            if (instructions.Length != 1)
                throw new PackageValidationException("Include exactly one file named SKILL.md (case sensitive).");
            var instruction = instructions[0];
            var segments = instruction.FullName.Split('/');
            if (segments.Length > 2 || (segments.Length == 2 &&
                files.Any(e => !e.FullName.StartsWith(segments[0] + "/", StringComparison.Ordinal))))
                throw new PackageValidationException("Put SKILL.md at the ZIP root, or inside one skill folder containing all files.");
            if (instruction.Length > MaxInstructionsBytes)
                throw new PackageValidationException("SKILL.md must be at most 256 KiB.");

            long actualTotal = 0;
            byte[] buffer = new byte[8192];
            using var instructionBytes = new MemoryStream();
            foreach (var entry in files)
            {
                using var stream = entry.Open();
                int count;
                long entryTotal = 0;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    actualTotal += count;
                    entryTotal += count;
                    if (actualTotal > MaxExpandedBytes || entryTotal > entry.Length)
                        throw new PackageValidationException("The package exceeds its allowed expanded size.");
                    if (ReferenceEquals(entry, instruction))
                    {
                        if (entryTotal > MaxInstructionsBytes)
                            throw new PackageValidationException("SKILL.md must be at most 256 KiB.");
                        instructionBytes.Write(buffer, 0, count);
                    }
                }
                if (entryTotal != entry.Length)
                    throw new PackageValidationException("An entry in the ZIP is incomplete.");
            }

            string text = new UTF8Encoding(false, true).GetString(instructionBytes.ToArray()).TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
                throw new PackageValidationException("SKILL.md must contain nonempty UTF-8 text.");
            return new ValidatedPackage(text, instruction.FullName);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or DecoderFallbackException or NotSupportedException)
        {
            throw new PackageValidationException("The ZIP could not be read. Use a valid, unencrypted ZIP with UTF-8 instructions.");
        }
    }
}
