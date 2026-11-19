using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace SqlServerMCP.Skills;

/// <summary>
/// Reads skill directories and embedded resources into validated, immutable file snapshots.
/// </summary>
public static class SkillFileReader
{
    public const string SkillFileName = "SKILL.md";

    public static SkillSource ReadDirectorySkill(string skillDirectory, string origin, SkillOptions options)
    {
        var fullPath = Path.GetFullPath(skillDirectory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The skill directory '{fullPath}' does not exist.");
        }

        if (IsReparsePoint(fullPath))
        {
            throw new SkillValidationException(
                $"The skill directory '{fullPath}' is a symbolic link or reparse point and is not allowed.");
        }

        var skillName = new DirectoryInfo(fullPath).Name;
        SkillNameValidator.EnsureValid(skillName, origin);

        var files = new List<SkillFileSource>();
        long totalBytes = 0;
        CollectDirectory(fullPath, fullPath, skillName, files, ref totalBytes, options, origin);
        EnsureSkillFile(files, origin);

        return BuildSource(skillName, origin, files);
    }

    public static SkillSource ReadEmbeddedSkill(
        Assembly assembly,
        string resourcePrefix,
        string skillName,
        IEnumerable<string> resourceNames,
        SkillOptions options)
    {
        SkillNameValidator.EnsureValid(skillName, "embedded resources");

        var normalizedPrefix = NormalizeResourceName(resourcePrefix);
        if (!normalizedPrefix.EndsWith('/'))
        {
            normalizedPrefix += "/";
        }

        var expectedPrefix = normalizedPrefix + skillName + "/";
        var files = new List<SkillFileSource>();
        long totalBytes = 0;

        foreach (var resourceName in resourceNames)
        {
            var normalizedName = NormalizeResourceName(resourceName);
            if (!normalizedName.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = normalizedName[expectedPrefix.Length..];
            ValidateRelativePath(relativePath);

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new SkillValidationException($"Embedded resource '{resourceName}' could not be opened.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            var content = buffer.ToArray();
            AddFile(skillName, relativePath, content, files, ref totalBytes, options, $"embedded:{resourceName}");
        }

        EnsureSkillFile(files, $"embedded:{assembly.GetName().Name}/{skillName}");
        return BuildSource(skillName, $"embedded:{assembly.GetName().Name}/{skillName}", files);
    }

    public static string BuildFileUri(string skillName, string relativePath)
    {
        var escapedPath = string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString));
        return $"skill://{skillName}/{escapedPath}";
    }

    public static string GuessMimeType(string path, ReadOnlySpan<byte> content)
    {
        var dot = path.LastIndexOf('.');
        var slash = path.LastIndexOf('/');
        var extension = dot <= slash ? string.Empty : path[(dot + 1)..].ToLowerInvariant();

        return extension switch
        {
            "md" or "markdown" => "text/markdown",
            "txt" => "text/plain",
            "csv" => "text/csv",
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "js" or "mjs" => "text/javascript",
            "ts" => "text/typescript",
            "py" => "text/x-python",
            "cs" => "text/x-csharp",
            "sh" or "bash" => "application/x-sh",
            "ps1" => "text/x-powershell",
            "json" => "application/json",
            "yaml" or "yml" => "application/yaml",
            "toml" => "application/toml",
            "xml" => "application/xml",
            "svg" => "image/svg+xml",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "pdf" => "application/pdf",
            _ => TryDecodeUtf8(content, out _) ? "text/plain" : "application/octet-stream"
        };
    }

    public static bool IsTextualMimeType(string mimeType) =>
        mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        mimeType.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ||
        mimeType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/yaml", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/toml", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
        mimeType.Equals("application/x-sh", StringComparison.OrdinalIgnoreCase);

    public static bool TryDecodeUtf8(ReadOnlySpan<byte> bytes, out string? text)
    {
        try
        {
            text = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = null;
            return false;
        }
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static void CollectDirectory(
        string root,
        string directory,
        string skillName,
        List<SkillFileSource> files,
        ref long totalBytes,
        SkillOptions options,
        string origin)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            var name = Path.GetFileName(entry);

            if (!options.IncludeHiddenAndSystemFiles &&
                (name.StartsWith(".", StringComparison.Ordinal) ||
                 (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0))
            {
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new SkillValidationException(
                    $"'{entry}' is a symbolic link or reparse point. Links are not followed when loading skills.");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                CollectDirectory(root, entry, skillName, files, ref totalBytes, options, origin);
                continue;
            }

            var length = new FileInfo(entry).Length;
            if (length > int.MaxValue)
            {
                throw new SkillValidationException($"'{entry}' is too large to serve as a skill file.");
            }

            var content = ReadExactly(entry, (int)length);
            var relativePath = Path.GetRelativePath(root, entry).Replace('\\', '/');
            ValidateRelativePath(relativePath);
            AddFile(skillName, relativePath, content, files, ref totalBytes, options, $"{origin}:{relativePath}");
        }
    }

    private static void AddFile(
        string skillName,
        string relativePath,
        byte[] content,
        List<SkillFileSource> files,
        ref long totalBytes,
        SkillOptions options,
        string origin)
    {
        if (files.Count + 1 > options.MaxFilesPerSkill)
        {
            throw new SkillValidationException(
                $"The skill '{skillName}' exceeds the configured {options.MaxFilesPerSkill} file limit ({origin}).");
        }

        totalBytes += content.Length;
        if (totalBytes > options.MaxTotalBytesPerSkill)
        {
            throw new SkillValidationException(
                $"The skill '{skillName}' exceeds the configured {options.MaxTotalBytesPerSkill} byte limit ({origin}).");
        }

        var mimeType = GuessMimeType(relativePath, content);
        var uri = BuildFileUri(skillName, relativePath);
        var digest = ComputeDigest(content);
        files.Add(new SkillFileSource(relativePath, uri, mimeType, content, digest, content.Length));
    }

    private static SkillSource BuildSource(string skillName, string origin, IReadOnlyList<SkillFileSource> files)
    {
        var uri = $"skill://{skillName}/{SkillFileName}";
        return new SkillSource(skillName, uri, origin, files);
    }

    private static void EnsureSkillFile(IReadOnlyList<SkillFileSource> files, string origin)
    {
        if (!files.Any(file => string.Equals(file.RelativePath, SkillFileName, StringComparison.Ordinal)))
        {
            throw new SkillValidationException($"The skill at '{origin}' does not contain a root {SkillFileName}.");
        }
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.StartsWith('/') ||
            relativePath.EndsWith('/') ||
            relativePath.Contains('\\'))
        {
            throw new SkillValidationException($"Invalid skill file path '{relativePath}'.");
        }

        foreach (var segment in relativePath.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                throw new SkillValidationException($"Invalid skill file path '{relativePath}'. Path segments must not be empty, '.' or '..'.");
            }
        }
    }

    private static string NormalizeResourceName(string name) => name.Replace('\\', '/');

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static byte[] ReadExactly(string path, int length)
    {
        var buffer = new byte[length];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total != buffer.Length || stream.ReadByte() != -1)
        {
            throw new IOException($"'{path}' changed size while it was being read.");
        }

        return buffer;
    }

    private static string ComputeDigest(ReadOnlySpan<byte> content) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
