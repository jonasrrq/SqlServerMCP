using System.Text;
using FluentAssertions;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Skills;
using SqlServerMCP.Skills;
using Xunit;

namespace SqlServerMCP.Tests;

public class SkillRegistryTests
{
    [Fact]
    public async Task ProjectContentProvider_LoadsSkillAndManifest()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "skill-one", "Skill one", ("references/guide.md", "# Guide\n"));
        var options = Options();

        var provider = new ProjectContentSkillProvider(temp.Path, options);
        var skills = (await provider.LoadAsync(default)).Skills;

        skills.Should().ContainSingle();
        var skill = skills[0];
        skill.Name.Should().Be("skill-one");
        skill.Uri.Should().Be("skill://skill-one/SKILL.md");
        skill.Files.Should().HaveCount(2);
        skill.Files.Should().ContainSingle(file => file.RelativePath == "SKILL.md");
        skill.Files.Should().ContainSingle(file => file.RelativePath == "references/guide.md");
        skill.Files.Should().OnlyContain(file => file.Digest.StartsWith("sha256:") && file.Size > 0);
    }

    [Fact]
    public async Task ExternalProvider_LoadsMultipleSkills()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "skill-one", "Skill one");
        CreateSkill(temp.Path, "skill-two", "Skill two");
        var options = Options();

        var provider = new ExternalDirectorySkillProvider([temp.Path], options);
        var skills = (await provider.LoadAsync(default)).Skills;

        skills.Select(skill => skill.Uri).Should().BeEquivalentTo(
            ["skill://skill-one/SKILL.md", "skill://skill-two/SKILL.md"]);
    }

    [Fact]
    public async Task EmbeddedProvider_LoadsBuiltInSkill()
    {
        var options = Options(includeEmbedded: true);
        var provider = new EmbeddedSkillProvider(typeof(SkillRegistry).Assembly, "Skills/", options);

        var skills = (await provider.LoadAsync(default)).Skills;

        skills.Should().ContainSingle();
        skills[0].Uri.Should().Be("skill://sqlservermcp/SKILL.md");
        skills[0].Files.Should().ContainSingle(file => file.RelativePath == "tools.json");
    }

    [Fact]
    public async Task Reload_UpdatesSkillContentAndManifest()
    {
        using var temp = new TempDirectory();
        var skillPath = CreateSkill(temp.Path, "skill-one", "First description");
        var options = Options();
        var registry = await SkillRegistry.CreateAsync(
            options,
            providers: [new ProjectContentSkillProvider(temp.Path, options)]);

        var first = await registry.ReadResourceAsync("skill://skill-one/SKILL.md");
        var firstDigest = registry.Skills.Single().Entry.Resources.Entries!
            .Single(file => file.Uri == "skill://skill-one/SKILL.md").Digest;

        File.WriteAllText(
            Path.Combine(skillPath, "SKILL.md"),
            Markdown("skill-one", "Second description") + "\nsecond-marker\n",
            new UTF8Encoding(false));

        await registry.ReloadAsync();

        var second = await registry.ReadResourceAsync("skill://skill-one/SKILL.md");
        var secondDigest = registry.Skills.Single().Entry.Resources.Entries!
            .Single(file => file.Uri == "skill://skill-one/SKILL.md").Digest;

        first.Contents[0].Should().BeOfType<ModelContextProtocol.Protocol.TextResourceContents>()
            .Which.Text.Should().NotContain("second-marker");
        second.Contents[0].Should().BeOfType<ModelContextProtocol.Protocol.TextResourceContents>()
            .Which.Text.Should().Contain("second-marker");
        secondDigest.Should().NotBe(firstDigest);
    }

    [Fact]
    public async Task DuplicateSkill_FailsByDefault()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        CreateSkill(first.Path, "duplicate", "First");
        CreateSkill(second.Path, "duplicate", "Second");
        var options = Options(conflictPolicy: SkillConflictPolicy.Fail);
        var provider = new ExternalDirectorySkillProvider([first.Path, second.Path], options);

        var act = async () => await SkillRegistry.CreateAsync(options, providers: [provider]);

        await act.Should().ThrowAsync<SkillValidationException>()
            .WithMessage("*duplicate*");
    }

    [Fact]
    public async Task DuplicateSkill_FirstWins()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        CreateSkill(first.Path, "duplicate", "First");
        CreateSkill(second.Path, "duplicate", "Second");
        var options = Options(conflictPolicy: SkillConflictPolicy.FirstWins);
        var provider = new ExternalDirectorySkillProvider([first.Path, second.Path], options);

        var registry = await SkillRegistry.CreateAsync(options, providers: [provider]);

        registry.Skills.Should().ContainSingle();
        registry.Skills[0].Description.Should().Be("First");
    }

    [Fact]
    public async Task DuplicateSkill_LastWins()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        CreateSkill(first.Path, "duplicate", "First");
        CreateSkill(second.Path, "duplicate", "Second");
        var options = Options(conflictPolicy: SkillConflictPolicy.LastWins);
        var provider = new ExternalDirectorySkillProvider([first.Path, second.Path], options);

        var registry = await SkillRegistry.CreateAsync(options, providers: [provider]);

        registry.Skills.Should().ContainSingle();
        registry.Skills[0].Description.Should().Be("Second");
    }

    [Fact]
    public async Task InvalidName_IsRejected()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "BadName", "Invalid");
        var options = Options();
        var provider = new ProjectContentSkillProvider(temp.Path, options);

        var act = async () => await SkillRegistry.CreateAsync(options, providers: [provider]);

        await act.Should().ThrowAsync<SkillValidationException>()
            .WithMessage("*Invalid skill name*");
    }

    [Fact]
    public void MissingSkillFile_IsRejected()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "missing-skill");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "notes.md"), "No SKILL.md");
        var options = Options();

        var act = () => SkillFileReader.ReadDirectorySkill(directory, "test", options);

        act.Should().Throw<SkillValidationException>()
            .WithMessage("*SKILL.md*");
    }

    [Fact]
    public async Task FileLimit_IsEnforced()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "limit-skill", "Limit", ("extra.md", "extra"));
        var options = Options(maxFiles: 1);
        var provider = new ProjectContentSkillProvider(temp.Path, options);

        var act = async () => await SkillRegistry.CreateAsync(options, providers: [provider]);

        await act.Should().ThrowAsync<SkillValidationException>()
            .WithMessage("*file limit*");
    }

    [Fact]
    public async Task ByteLimit_IsEnforced()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "limit-skill", "Limit");
        var options = Options(maxBytes: 10);
        var provider = new ProjectContentSkillProvider(temp.Path, options);

        var act = async () => await SkillRegistry.CreateAsync(options, providers: [provider]);

        await act.Should().ThrowAsync<SkillValidationException>()
            .WithMessage("*byte limit*");
    }

    [Fact]
    public async Task PathTraversal_IsRejected()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "skill-one", "Skill one");
        var options = Options();
        var registry = await SkillRegistry.CreateAsync(
            options,
            providers: [new ProjectContentSkillProvider(temp.Path, options)]);

        var act = async () => await registry.ReadResourceAsync("skill://skill-one/../secret");

        await act.Should().ThrowAsync<McpProtocolException>()
            .Where(exception => exception.ErrorCode == McpErrorCode.InvalidParams);

        var create = () => McpServerSkillPackage.Create(
            [
                McpServerSkillFile.FromText("SKILL.md", Markdown("skill-one", "Skill one")),
                McpServerSkillFile.FromText("../secret.md", "secret")
            ],
            new McpServerSkillPackageCreateOptions { Uri = "skill://skill-one/SKILL.md" });

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ReadResource_ReturnsSupportingFile()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "skill-one", "Skill one", ("references/guide.md", "# Guide\n"));
        var options = Options();
        var registry = await SkillRegistry.CreateAsync(
            options,
            providers: [new ProjectContentSkillProvider(temp.Path, options)]);

        var result = await registry.ReadResourceAsync("skill://skill-one/references/guide.md");

        result.Contents.Should().ContainSingle();
        result.Contents[0].Should().BeOfType<ModelContextProtocol.Protocol.TextResourceContents>()
            .Which.Text.Should().Be("# Guide\n");
    }

    [Fact]
    public async Task Pagination_ReturnsNextCursor()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "skill-one", "One");
        CreateSkill(temp.Path, "skill-two", "Two");
        CreateSkill(temp.Path, "skill-three", "Three");
        var options = Options(pageSize: 2);
        var registry = await SkillRegistry.CreateAsync(
            options,
            providers: [new ProjectContentSkillProvider(temp.Path, options)]);

        var first = await registry.ListSkillsAsync(cursor: null);
        first.Skills.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNullOrEmpty();

        var second = await registry.ListSkillsAsync(first.NextCursor);
        second.Skills.Should().ContainSingle();
        second.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task StrictValidationFalse_SkipsInvalidSkill()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "BadName", "Invalid");
        CreateSkill(temp.Path, "good-skill", "Valid");
        var messages = new List<string>();
        var options = Options(strictValidation: false);
        var registry = await SkillRegistry.CreateAsync(
            options,
            messages.Add,
            [new ProjectContentSkillProvider(temp.Path, options)]);

        registry.Skills.Should().ContainSingle(skill => skill.Id == "skill://good-skill/SKILL.md");
        messages.Should().Contain(message => message.Contains("Skipping invalid skill"));
    }

    [Fact]
    public async Task GetSkill_Unknown_ThrowsInvalidParams()
    {
        using var temp = new TempDirectory();
        CreateSkill(temp.Path, "skill-one", "Skill one");
        var options = Options();
        var registry = await SkillRegistry.CreateAsync(
            options,
            providers: [new ProjectContentSkillProvider(temp.Path, options)]);

        var act = async () => await registry.GetSkillAsync("skill://missing/SKILL.md");

        await act.Should().ThrowAsync<McpProtocolException>()
            .Where(exception => exception.ErrorCode == McpErrorCode.InvalidParams);
    }

    private static SkillOptions Options(
        bool includeEmbedded = false,
        SkillConflictPolicy conflictPolicy = SkillConflictPolicy.Fail,
        int pageSize = 50,
        int maxFiles = 512,
        long maxBytes = 16 * 1024 * 1024,
        bool strictValidation = true) =>
        new()
        {
            Enabled = true,
            IncludeProjectContent = false,
            IncludeEmbedded = includeEmbedded,
            ConflictPolicy = conflictPolicy,
            PageSize = pageSize,
            MaxFilesPerSkill = maxFiles,
            MaxTotalBytesPerSkill = maxBytes,
            StrictValidation = strictValidation
        };

    private static string CreateSkill(
        string root,
        string name,
        string description,
        params (string Path, string Content)[] files)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), Markdown(name, description), new UTF8Encoding(false));

        foreach (var (relativePath, content) in files)
        {
            var filePath = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content, new UTF8Encoding(false));
        }

        return directory;
    }

    private static string Markdown(string name, string description) =>
        $"---\nname: {name}\ndescription: {description}\n---\n# {name}\n\nRead `references/guide.md`.\n";

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mcp-skills-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
