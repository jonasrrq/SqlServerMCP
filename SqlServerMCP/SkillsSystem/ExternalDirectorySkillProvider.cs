namespace SqlServerMCP.Skills;

/// <summary>
/// Loads skills from configured external roots without recompiling the server.
/// A root may be a skill directory itself or a parent of skill directories.
/// </summary>
public sealed class ExternalDirectorySkillProvider : ISkillProvider
{
    private readonly IReadOnlyList<string> _roots;
    private readonly SkillOptions _options;

    public ExternalDirectorySkillProvider(IReadOnlyList<string> roots, SkillOptions options)
    {
        _roots = roots;
        _options = options;
    }

    public string Name => "external-directories";

    public ValueTask<SkillProviderResult> LoadAsync(CancellationToken cancellationToken)
    {
        var skills = new List<SkillSource>();
        var failures = new List<SkillLoadFailure>();

        foreach (var configuredRoot in _roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var root = Path.GetFullPath(configuredRoot);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"The configured skills directory '{root}' does not exist.");
            }

            if (File.Exists(Path.Combine(root, SkillFileReader.SkillFileName)))
            {
                try
                {
                    skills.Add(SkillFileReader.ReadDirectorySkill(root, $"external:{root}", _options));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures.Add(new SkillLoadFailure($"external:{root}", exception));
                }

                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(static path => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(Path.Combine(directory, SkillFileReader.SkillFileName)))
                {
                    continue;
                }

                try
                {
                    skills.Add(SkillFileReader.ReadDirectorySkill(directory, $"external:{directory}", _options));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures.Add(new SkillLoadFailure($"external:{directory}", exception));
                }
            }
        }

        return new(new SkillProviderResult(skills, failures));
    }
}
