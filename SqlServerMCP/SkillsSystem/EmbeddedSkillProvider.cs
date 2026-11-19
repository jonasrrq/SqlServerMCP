using System.Reflection;

namespace SqlServerMCP.Skills;

/// <summary>
/// Loads skills embedded in an assembly under a logical resource prefix.
/// </summary>
public sealed class EmbeddedSkillProvider : ISkillProvider
{
    private readonly Assembly _assembly;
    private readonly string _prefix;
    private readonly SkillOptions _options;

    public EmbeddedSkillProvider(Assembly assembly, string prefix, SkillOptions options)
    {
        _assembly = assembly;
        _prefix = prefix;
        _options = options;
    }

    public string Name => "embedded";

    public ValueTask<SkillProviderResult> LoadAsync(CancellationToken cancellationToken)
    {
        var prefix = _prefix.Replace('\\', '/').TrimEnd('/') + "/";
        var resources = _assembly.GetManifestResourceNames()
            .Select(name => (Original: name, Normalized: name.Replace('\\', '/')))
            .Where(resource => resource.Normalized.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        if (resources.Length == 0)
        {
            return new(SkillProviderResult.Empty);
        }

        var groups = resources
            .Select(resource =>
            {
                var rest = resource.Normalized[prefix.Length..];
                var separator = rest.IndexOf('/');
                return new
                {
                    SkillName = separator > 0 ? rest[..separator] : null,
                    Resource = resource
                };
            })
            .Where(item => item.SkillName is not null)
            .GroupBy(item => item.SkillName!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        var skills = new List<SkillSource>(groups.Length);
        var failures = new List<SkillLoadFailure>();
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resourceNames = group.Select(item => item.Resource.Original).ToArray();
            try
            {
                skills.Add(SkillFileReader.ReadEmbeddedSkill(_assembly, prefix, group.Key, resourceNames, _options));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new SkillLoadFailure($"embedded:{_assembly.GetName().Name}/{group.Key}", exception));
            }
        }

        return new(new SkillProviderResult(skills, failures));
    }
}
