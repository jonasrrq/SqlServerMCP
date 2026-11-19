using Microsoft.Extensions.Configuration;

namespace SqlServerMCP.Skills;

public enum SkillConflictPolicy
{
    Fail,
    FirstWins,
    LastWins
}

/// <summary>
/// Configuration for the server-side MCP Skills distribution system.
/// </summary>
public sealed class SkillOptions
{
    public bool Enabled { get; init; } = true;
    public bool IncludeProjectContent { get; init; } = true;
    public bool IncludeEmbedded { get; init; }
    public IReadOnlyList<string> ExternalDirectories { get; init; } = [];
    public bool ReloadOnChange { get; init; }
    public int ReloadIntervalSeconds { get; init; } = 30;
    public bool StrictValidation { get; init; } = true;
    public SkillConflictPolicy ConflictPolicy { get; init; } = SkillConflictPolicy.Fail;
    public int PageSize { get; init; } = 50;
    public int MaxFilesPerSkill { get; init; } = 512;
    public long MaxTotalBytesPerSkill { get; init; } = 16 * 1024 * 1024;
    public string ProjectContentDirectory { get; init; } = "Skills";
    public string EmbeddedResourcePrefix { get; init; } = "Skills/";
    public bool IncludeHiddenAndSystemFiles { get; init; }

    public static SkillOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Skills");

        var options = new SkillOptions
        {
            Enabled = GetBool(section, "Enabled", "MCP_SKILLS_ENABLED", true),
            IncludeProjectContent = GetBool(section, "IncludeProjectContent", "MCP_SKILLS_INCLUDE_PROJECT_CONTENT", true),
            IncludeEmbedded = GetBool(section, "IncludeEmbedded", "MCP_SKILLS_INCLUDE_EMBEDDED", false),
            ExternalDirectories = GetExternalDirectories(section),
            ReloadOnChange = GetBool(section, "ReloadOnChange", "MCP_SKILLS_RELOAD_ON_CHANGE", false),
            ReloadIntervalSeconds = GetInt(section, "ReloadIntervalSeconds", "MCP_SKILLS_RELOAD_INTERVAL_SECONDS", 30),
            StrictValidation = GetBool(section, "StrictValidation", "MCP_SKILLS_STRICT_VALIDATION", true),
            ConflictPolicy = GetConflictPolicy(section),
            PageSize = GetInt(section, "PageSize", "MCP_SKILLS_PAGE_SIZE", 50),
            MaxFilesPerSkill = GetInt(section, "MaxFilesPerSkill", "MCP_SKILLS_MAX_FILES_PER_SKILL", 512),
            MaxTotalBytesPerSkill = GetLong(section, "MaxTotalBytesPerSkill", "MCP_SKILLS_MAX_TOTAL_BYTES_PER_SKILL", 16 * 1024 * 1024),
            ProjectContentDirectory = GetString(section, "ProjectContentDirectory", "MCP_SKILLS_PROJECT_CONTENT_DIRECTORY", "Skills")!,
            EmbeddedResourcePrefix = GetString(section, "EmbeddedResourcePrefix", "MCP_SKILLS_EMBEDDED_RESOURCE_PREFIX", "Skills/")!,
            IncludeHiddenAndSystemFiles = GetBool(section, "IncludeHiddenAndSystemFiles", "MCP_SKILLS_INCLUDE_HIDDEN_AND_SYSTEM_FILES", false)
        };

        return options.Normalize();
    }

    private SkillOptions Normalize()
    {
        var external = ExternalDirectories
            .Select(static directory => directory.Trim())
            .Where(static directory => directory.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SkillOptions
        {
            Enabled = Enabled,
            IncludeProjectContent = IncludeProjectContent,
            IncludeEmbedded = IncludeEmbedded,
            ExternalDirectories = external,
            ReloadOnChange = ReloadOnChange,
            ReloadIntervalSeconds = Math.Max(1, ReloadIntervalSeconds),
            StrictValidation = StrictValidation,
            ConflictPolicy = ConflictPolicy,
            PageSize = Math.Clamp(PageSize, 1, 1000),
            MaxFilesPerSkill = Math.Max(1, MaxFilesPerSkill),
            MaxTotalBytesPerSkill = Math.Max(1, MaxTotalBytesPerSkill),
            ProjectContentDirectory = string.IsNullOrWhiteSpace(ProjectContentDirectory) ? "Skills" : ProjectContentDirectory.Trim(),
            EmbeddedResourcePrefix = string.IsNullOrWhiteSpace(EmbeddedResourcePrefix) ? "Skills/" : EmbeddedResourcePrefix.Trim(),
            IncludeHiddenAndSystemFiles = IncludeHiddenAndSystemFiles
        };
    }

    private static IReadOnlyList<string> GetExternalDirectories(IConfigurationSection section)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("MCP_SKILLS_EXTERNAL_DIRECTORIES");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return section.GetSection("ExternalDirectories")
            .GetChildren()
            .Select(static child => child.Value?.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static SkillConflictPolicy GetConflictPolicy(IConfigurationSection section)
    {
        var value = GetString(section, "ConflictPolicy", "MCP_SKILLS_CONFLICT_POLICY", "Fail");
        return Enum.TryParse<SkillConflictPolicy>(value, ignoreCase: true, out var policy)
            ? policy
            : SkillConflictPolicy.Fail;
    }

    private static bool GetBool(IConfigurationSection section, string key, string environmentVariable, bool defaultValue)
    {
        var value = GetString(section, key, environmentVariable, null);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static int GetInt(IConfigurationSection section, string key, string environmentVariable, int defaultValue)
    {
        var value = GetString(section, key, environmentVariable, null);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static long GetLong(IConfigurationSection section, string key, string environmentVariable, long defaultValue)
    {
        var value = GetString(section, key, environmentVariable, null);
        return long.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string? GetString(IConfigurationSection section, string key, string environmentVariable, string? defaultValue)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        return section[key] ?? defaultValue;
    }
}
