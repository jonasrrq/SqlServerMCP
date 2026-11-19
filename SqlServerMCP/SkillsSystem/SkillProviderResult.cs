namespace SqlServerMCP.Skills;

/// <summary>
/// Result of one provider load. Invalid skills are reported per entry so the registry can
/// fail fast or skip them without discarding valid skills from the same provider.
/// </summary>
public sealed record SkillProviderResult(
    IReadOnlyList<SkillSource> Skills,
    IReadOnlyList<SkillLoadFailure> Failures)
{
    public static readonly SkillProviderResult Empty = new([], []);
}

public sealed record SkillLoadFailure(string Origin, Exception Exception);
