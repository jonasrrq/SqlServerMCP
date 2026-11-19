using System.Text.RegularExpressions;

namespace SqlServerMCP.Skills;

/// <summary>
/// Validates Agent Skills names. Names are lowercase ASCII letters, digits, and single
/// hyphens, must start and end with a letter or digit, and are limited to 64 characters.
/// </summary>
public static partial class SkillNameValidator
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Length <= 64 &&
        NamePattern().IsMatch(name);

    public static string EnsureValid(string? name, string context)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException(
                $"Invalid skill name '{name}' in {context}. Use lowercase letters, digits and single hyphens (max 64 chars).",
                nameof(name));
        }

        return name!;
    }
}
