using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Protocol;

namespace SqlServerMCP.Skills;

/// <summary>
/// A validated skill entry and the immutable file snapshot it was authored from.
/// </summary>
public sealed record RegisteredSkill(
    string Id,
    string Name,
    string Description,
    string Origin,
    Skill Entry,
    IReadOnlyList<SkillFileSource> Files);

/// <summary>
/// Discovers, validates, stores, and serves Agent Skills. Snapshots are immutable and swapped
/// atomically so request handlers can read without locking while a reload is in progress.
/// </summary>
public sealed class SkillRegistry
{
    private readonly SkillOptions _options;
    private readonly IReadOnlyList<ISkillProvider> _providers;
    private readonly Action<string>? _log;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private RegistrySnapshot _snapshot = RegistrySnapshot.Empty;

    private SkillRegistry(SkillOptions options, IReadOnlyList<ISkillProvider> providers, Action<string>? log)
    {
        _options = options;
        _providers = providers;
        _log = log;
    }

    public IReadOnlyList<RegisteredSkill> Skills => Volatile.Read(ref _snapshot).Skills;

    public static async Task<SkillRegistry> CreateAsync(
        SkillOptions options,
        Action<string>? log = null,
        IEnumerable<ISkillProvider>? providers = null,
        CancellationToken cancellationToken = default)
    {
        var registry = new SkillRegistry(
            options,
            providers?.ToArray() ?? CreateDefaultProviders(options),
            log);

        await registry.ReloadAsync(cancellationToken).ConfigureAwait(false);
        return registry;
    }

    public static IReadOnlyList<ISkillProvider> CreateDefaultProviders(SkillOptions options)
    {
        var providers = new List<ISkillProvider>();

        if (options.IncludeProjectContent)
        {
            providers.Add(new ProjectContentSkillProvider(options.ProjectContentDirectory, options));
        }

        if (options.ExternalDirectories.Count > 0)
        {
            providers.Add(new ExternalDirectorySkillProvider(options.ExternalDirectories, options));
        }

        if (options.IncludeEmbedded)
        {
            providers.Add(new EmbeddedSkillProvider(typeof(SkillRegistry).Assembly, options.EmbeddedResourcePrefix, options));
        }

        return providers;
    }

    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sources = new List<SkillSource>();
            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SkillProviderResult loaded;
                try
                {
                    loaded = await provider.LoadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    if (_options.StrictValidation)
                    {
                        throw new SkillValidationException(
                            $"Failed to load skills from provider '{provider.Name}': {exception.Message}",
                            exception);
                    }

                    _log?.Invoke($"Skipping provider '{provider.Name}': {exception.Message}");
                    continue;
                }

                sources.AddRange(loaded.Skills);

                foreach (var failure in loaded.Failures)
                {
                    if (_options.StrictValidation)
                    {
                        throw new SkillValidationException(
                            $"Failed to load skill from '{failure.Origin}': {failure.Exception.Message}",
                            failure.Exception);
                    }

                    _log?.Invoke($"Skipping invalid skill from '{failure.Origin}': {failure.Exception.Message}");
                }
            }

            var registered = new Dictionary<string, RegisteredSkill>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var package = McpServerSkillPackage.Create(
                        source.Files
                            .Select(file => new McpServerSkillFile
                            {
                                Path = file.RelativePath,
                                Content = file.Content,
                                MimeType = file.MimeType
                            })
                            .ToArray(),
                        new McpServerSkillPackageCreateOptions { Uri = source.Uri });

                    var entry = package.Skill.ProtocolSkill
                        ?? throw new SkillValidationException($"The package for '{source.Uri}' produced no skill entry.");

                    if (!string.Equals(entry.Uri, source.Uri, StringComparison.Ordinal))
                    {
                        throw new SkillValidationException(
                            $"The package for '{source.Uri}' produced the entry URI '{entry.Uri}'.");
                    }

                    if (!string.Equals(entry.Name, source.Name, StringComparison.Ordinal))
                    {
                        throw new SkillValidationException(
                            $"The skill directory '{source.Name}' does not match the frontmatter name '{entry.Name}'.");
                    }

                    var registeredSkill = new RegisteredSkill(
                        entry.Uri,
                        entry.Name ?? source.Name,
                        entry.Description ?? string.Empty,
                        source.Origin,
                        entry,
                        source.Files);

                    if (registered.TryGetValue(entry.Uri, out var existing))
                    {
                        switch (_options.ConflictPolicy)
                        {
                            case SkillConflictPolicy.Fail:
                                throw new SkillValidationException(
                                    $"Duplicate skill URI '{entry.Uri}' from '{existing.Origin}' and '{source.Origin}'.");
                            case SkillConflictPolicy.FirstWins:
                                _log?.Invoke($"Skipping duplicate skill '{entry.Uri}' from '{source.Origin}'; keeping '{existing.Origin}'.");
                                continue;
                            case SkillConflictPolicy.LastWins:
                                _log?.Invoke($"Replacing duplicate skill '{entry.Uri}' from '{existing.Origin}' with '{source.Origin}'.");
                                registered[entry.Uri] = registeredSkill;
                                continue;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(_options.ConflictPolicy));
                        }
                    }

                    registered[entry.Uri] = registeredSkill;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    if (_options.StrictValidation)
                    {
                        throw new SkillValidationException(
                            $"Failed to load skill from '{source.Origin}': {exception.Message}",
                            exception);
                    }

                    _log?.Invoke($"Skipping invalid skill from '{source.Origin}': {exception.Message}");
                }
            }

            var filesByUri = new Dictionary<string, SkillFileSource>(StringComparer.Ordinal);
            var resources = new List<Resource>();

            foreach (var skill in registered.Values.OrderBy(static skill => skill.Id, StringComparer.Ordinal))
            {
                var manifest = skill.Entry.Resources.Entries;
                if (manifest is null)
                {
                    throw new SkillValidationException($"Skill '{skill.Id}' has no static file manifest.");
                }

                if (manifest.Count != skill.Files.Count)
                {
                    throw new SkillValidationException(
                        $"Skill '{skill.Id}' manifest has {manifest.Count} files but the source has {skill.Files.Count}.");
                }

                long totalBytes = 0;
                foreach (var file in skill.Files)
                {
                    if (!filesByUri.TryAdd(file.Uri, file))
                    {
                        throw new SkillValidationException($"Duplicate resource URI '{file.Uri}'.");
                    }

                    totalBytes += file.Size;
                    var isSkillFile = string.Equals(
                        file.RelativePath,
                        SkillFileReader.SkillFileName,
                        StringComparison.Ordinal);
                    resources.Add(new Resource
                    {
                        Uri = file.Uri,
                        Name = isSkillFile ? skill.Name : file.RelativePath,
                        Description = isSkillFile ? skill.Description : null,
                        MimeType = file.MimeType
                    });
                }

                if (totalBytes > _options.MaxTotalBytesPerSkill)
                {
                    throw new SkillValidationException(
                        $"Skill '{skill.Id}' exceeds the configured {_options.MaxTotalBytesPerSkill} byte limit.");
                }
            }

            var snapshot = new RegistrySnapshot(
                registered.Values.OrderBy(static skill => skill.Id, StringComparer.Ordinal).ToArray(),
                registered,
                filesByUri,
                resources.OrderBy(static resource => resource.Uri, StringComparer.Ordinal).ToArray());

            Interlocked.Exchange(ref _snapshot, snapshot);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public ValueTask<ListSkillsResult> ListSkillsAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref _snapshot);
        var page = PageByUri(snapshot.Skills, static skill => skill.Id, cursor, _options.PageSize);

        return new(new ListSkillsResult
        {
            Skills = page.Items.Select(static skill => skill.Entry).ToList(),
            NextCursor = page.NextCursor,
            TimeToLive = TimeSpan.Zero,
            CacheScope = CacheScope.Private
        });
    }

    public ValueTask<GetSkillResult> GetSkillAsync(string uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref _snapshot);
        if (string.IsNullOrWhiteSpace(uri) || !snapshot.ByUri.TryGetValue(uri, out var skill))
        {
            throw new McpProtocolException($"No skill is served at '{uri}'.", McpErrorCode.InvalidParams);
        }

        return new(new GetSkillResult
        {
            Skill = skill.Entry,
            TimeToLive = TimeSpan.Zero,
            CacheScope = CacheScope.Private
        });
    }

    public ValueTask<ListResourcesResult> ListResourcesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref _snapshot);
        var page = PageByUri(snapshot.Resources, static resource => resource.Uri, cursor, _options.PageSize);

        return new(new ListResourcesResult
        {
            Resources = page.Items.ToList(),
            NextCursor = page.NextCursor
        });
    }

    public ValueTask<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref _snapshot);
        if (string.IsNullOrWhiteSpace(uri) || !snapshot.FilesByUri.TryGetValue(uri, out var file))
        {
            throw new McpProtocolException($"Resource '{uri}' is not available.", McpErrorCode.InvalidParams);
        }

        ResourceContents contents = SkillFileReader.IsTextualMimeType(file.MimeType) &&
                                    SkillFileReader.TryDecodeUtf8(file.Content.Span, out var text)
            ? new TextResourceContents
            {
                Uri = file.Uri,
                MimeType = file.MimeType,
                Text = text!
            }
            : BlobResourceContents.FromBytes(file.Content, file.Uri, file.MimeType);

        return new(new ReadResourceResult { Contents = [contents] });
    }

    private static (IReadOnlyList<T> Items, string? NextCursor) PageByUri<T>(
        IReadOnlyList<T> items,
        Func<T, string> uriSelector,
        string? cursor,
        int pageSize)
    {
        var start = 0;
        if (!string.IsNullOrEmpty(cursor))
        {
            var lastUri = DecodeCursor(cursor);
            var index = -1;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(uriSelector(items[i]), lastUri, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                throw new McpProtocolException("Invalid cursor.", McpErrorCode.InvalidParams);
            }

            start = index + 1;
        }

        var page = items.Skip(start).Take(pageSize).ToArray();
        var nextCursor = start + page.Length < items.Count
            ? EncodeCursor(uriSelector(items[start + page.Length - 1]))
            : null;

        return (page, nextCursor);
    }

    private static string EncodeCursor(string uri) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(uri));

    private static string DecodeCursor(string cursor)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(Convert.FromBase64String(cursor));
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            throw new McpProtocolException("Invalid cursor.", McpErrorCode.InvalidParams);
        }
    }

    private sealed record RegistrySnapshot(
        IReadOnlyList<RegisteredSkill> Skills,
        IReadOnlyDictionary<string, RegisteredSkill> ByUri,
        IReadOnlyDictionary<string, SkillFileSource> FilesByUri,
        IReadOnlyList<Resource> Resources)
    {
        public static readonly RegistrySnapshot Empty = new(
            [],
            new Dictionary<string, RegisteredSkill>(StringComparer.Ordinal),
            new Dictionary<string, SkillFileSource>(StringComparer.Ordinal),
            []);
    }
}
