namespace SqlServerMCP.Skills;

/// <summary>
/// One file of a skill, captured as immutable bytes with its resource URI and digest.
/// </summary>
public sealed record SkillFileSource(
    string RelativePath,
    string Uri,
    string MimeType,
    ReadOnlyMemory<byte> Content,
    string Digest,
    long Size);

/// <summary>
/// A skill discovered from a provider, before it is registered in the MCP server.
/// </summary>
public sealed record SkillSource(
    string Name,
    string Uri,
    string Origin,
    IReadOnlyList<SkillFileSource> Files);

public sealed class SkillValidationException : Exception
{
    public SkillValidationException(string message)
        : base(message)
    {
    }

    public SkillValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
