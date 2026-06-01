using System.Text.RegularExpressions;

namespace SqlServerMCP;

public static class DiagnosticSanitizer
{
    private static readonly Regex PasswordRegex = new(@"(?i)password\s*=\s*[^;\s]+", RegexOptions.Compiled);
    private static readonly Regex UserIdRegex = new(@"(?i)(user\s*id|uid)\s*=\s*[^;\s]+", RegexOptions.Compiled);

    public static string BuildDebugDetail(Exception exception, int maxLength = 300)
    {
        if (exception is null) return string.Empty;

        try
        {
            var messages = new List<string>();
            var current = exception;
            while (current is not null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    messages.Add(current.Message.Trim());
                }

                current = current.InnerException;
            }

            var combined = string.Join(" | ", messages.Distinct(StringComparer.Ordinal));
            combined = PasswordRegex.Replace(combined, "Password=***");
            combined = UserIdRegex.Replace(combined, "User ID=***");
            combined = Regex.Replace(combined, "\\s+", " ").Trim();

            return combined.Length <= maxLength ? combined : combined[..maxLength] + "…";
        }
        catch
        {
            // best-effort fallback
            var text = exception.ToString() ?? string.Empty;
            text = PasswordRegex.Replace(text, "Password=***");
            text = UserIdRegex.Replace(text, "User ID=***");
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text.Length <= maxLength ? text : text[..maxLength] + "…";
        }
    }
}
