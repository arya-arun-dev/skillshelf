namespace SkillsMarketplace.Models;

public sealed record Skill(
    string Id, string Name, string Description, string Author,
    string Instructions, string InstructionPath, string CreatedUtc, long SizeBytes);
