namespace SqlServerMCP.Skills;

/// <summary>
/// Loads skills copied with the server under the project content directory.
/// Each immediate child directory containing a SKILL.md is one skill.
/// </summary>
public sealed class ProjectContentSkillProvider : ISkillProvider
{
    private readonly string _root;
    private readonly SkillOptions _options;

    public ProjectContentSkillProvider(string root, SkillOptions options)
    {
        _root = root;
        _options = options;
    }

    public string Name => "project-content";

    public ValueTask<SkillProviderResult> LoadAsync(CancellationToken cancellationToken)
    {
        var root = Path.IsPathRooted(_root)
            ? Path.GetFullPath(_root)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _root));

        if (!Directory.Exists(root))
        {
            return new(SkillProviderResult.Empty);
        }

        var skills = new List<SkillSource>();
        var failures = new List<SkillLoadFailure>();
        foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(static path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(Path.Combine(directory, SkillFileReader.SkillFileName)))
            {
                continue;
            }

            try
            {
                skills.Add(SkillFileReader.ReadDirectorySkill(directory, $"project:{directory}", _options));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new SkillLoadFailure($"project:{directory}", exception));
            }
        }

        return new(new SkillProviderResult(skills, failures));
    }
}
