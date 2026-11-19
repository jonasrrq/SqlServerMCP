namespace SqlServerMCP.Skills;

/// <summary>
/// A source of Agent Skills. Implementations are discovered at startup and re-run on reload.
/// </summary>
public interface ISkillProvider
{
    string Name { get; }

    ValueTask<SkillProviderResult> LoadAsync(CancellationToken cancellationToken);
}
