using Microsoft.Data.Sqlite;
using SkillsMarketplace.Models;

namespace SkillsMarketplace.Services;

// Each operation opens its own connection. SQLite serializes writes.
public sealed class SkillRepository
{
    private readonly string connectionString;
    private readonly string packageDirectory;

    public SkillRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        packageDirectory = Path.Combine(dataDirectory, "packages");
        Directory.CreateDirectory(packageDirectory);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "skills.db"), Pooling = false
        }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Skills (
                Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Description TEXT NOT NULL,
                Author TEXT NOT NULL, Instructions TEXT NOT NULL, InstructionPath TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL, SizeBytes INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    public IReadOnlyList<Skill> Search(string? query)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        // instr performs a literal substring match; % and _ are not SQL wildcards.
        command.CommandText = """
            SELECT * FROM Skills
            WHERE $q = '' OR instr(lower(Name), lower($q)) > 0
                OR instr(lower(Description), lower($q)) > 0
            ORDER BY CreatedUtc DESC, Id DESC;
            """;
        command.Parameters.AddWithValue("$q", query?.Trim() ?? "");
        using var reader = command.ExecuteReader();
        var result = new List<Skill>();
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }

    public Skill? Find(string id)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT * FROM Skills WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public async Task<string> PublishAsync(string name, string description, string author,
        ValidatedPackage package, byte[] bytes)
    {
        var id = Guid.NewGuid().ToString("N");
        await StoreAsync(id, name, description, author, package, bytes, skipExisting: false);
        return id;
    }

    // Stable sample IDs make startup seeding repeatable without replacing saved entries.
    public Task<bool> AddSampleIfMissingAsync(string id, string name, string description,
        string author, ValidatedPackage package, byte[] bytes) =>
        StoreAsync(id, name, description, author, package, bytes, skipExisting: true);

    private async Task<bool> StoreAsync(string id, string name, string description,
        string author, ValidatedPackage package, byte[] bytes, bool skipExisting)
    {
        var path = PackagePath(id);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool packageWritten = false;
        using var db = Open();
        using var transaction = db.BeginTransaction();
        try
        {
            using var command = db.CreateCommand();
            command.Transaction = transaction;
            // The SQL verb is selected internally; all record values remain parameters.
            var insert = skipExisting ? "INSERT OR IGNORE" : "INSERT";
            command.CommandText = $"""
                {insert} INTO Skills (Id, Name, Description, Author, Instructions, InstructionPath, CreatedUtc, SizeBytes)
                VALUES ($id, $name, $description, $author, $instructions, $instructionPath, $created, $size);
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$name", name.Trim());
            command.Parameters.AddWithValue("$description", description.Trim());
            command.Parameters.AddWithValue("$author", author.Trim());
            command.Parameters.AddWithValue("$instructions", package.Instructions);
            command.Parameters.AddWithValue("$instructionPath", package.InstructionPath);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$size", bytes.LongLength);
            if (command.ExecuteNonQuery() == 0) return false;

            // The uncommitted insert reserves this ID. Readers only see it after
            // the complete ZIP has been moved into place and the transaction commits.
            await File.WriteAllBytesAsync(temporaryPath, bytes);
            File.Move(temporaryPath, path, overwrite: true);
            packageWritten = true;
            transaction.Commit();
            return true;
        }
        catch
        {
            // A process crash can still leave an orphan file; see README limitations.
            if (packageWritten && File.Exists(path)) File.Delete(path);
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public string PackagePath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid package ID.", nameof(id));
        return Path.Combine(packageDirectory, id + ".zip");
    }

    private static Skill Read(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7));
}
