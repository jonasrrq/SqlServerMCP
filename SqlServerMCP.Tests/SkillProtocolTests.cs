using System.IO.Pipelines;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SqlServerMCP.Skills;
using Xunit;

namespace SqlServerMCP.Tests;

public class SkillProtocolTests
{
    [Fact]
    public async Task InMemoryClient_CanListGetAndReadSkills_AndStillUseTools()
    {
        using var temp = new TempDirectory();
        var skillDirectory = Path.Combine(temp.Path, "skill-one");
        Directory.CreateDirectory(Path.Combine(skillDirectory, "references"));
        File.WriteAllText(
            Path.Combine(skillDirectory, "SKILL.md"),
            Markdown("skill-one", "Protocol skill"),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(skillDirectory, "references", "guide.md"),
            "# Guide\n",
            new UTF8Encoding(false));

        var options = new SkillOptions
        {
            Enabled = true,
            IncludeProjectContent = false,
            PageSize = 50,
            MaxFilesPerSkill = 512,
            MaxTotalBytesPerSkill = 16 * 1024 * 1024
        };

        var registry = await SkillRegistry.CreateAsync(
            options,
            providers: [new ProjectContentSkillProvider(temp.Path, options)]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMcpServer().WithSkillDistribution(registry, options);
        var provider = services.BuildServiceProvider();

        var serverOptions = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        serverOptions.ToolCollection =
        [
            McpServerTool.Create(
                (string argument) => $"Echo: {argument}",
                new McpServerToolCreateOptions { Name = "echo" })
        ];

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var server = McpServer.Create(
            new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream()),
            serverOptions,
            NullLoggerFactory.Instance,
            provider);

        _ = server.RunAsync();

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()));

        client.ServerCapabilities.SupportsSkills().Should().BeTrue();

        var listed = await client.ListSkillsAsync();
        listed.Should().ContainSingle();
        listed[0].Uri.Should().Be("skill://skill-one/SKILL.md");

        var skill = await client.GetSkillAsync("skill://skill-one/SKILL.md");
        skill.Name.Should().Be("skill-one");
        skill.Resources.Entries.Should().HaveCount(2);

        var skillFile = await client.ReadSkillResourceAsync(skill, skill.Uri);
        skillFile.Contents.Should().ContainSingle();
        skillFile.Contents[0].Should().BeOfType<TextResourceContents>()
            .Which.Text.Should().Contain("# skill-one");

        var supportingFile = await client.ReadSkillResourceAsync(skill, "skill://skill-one/references/guide.md");
        supportingFile.Contents.Should().ContainSingle();
        supportingFile.Contents[0].Should().BeOfType<TextResourceContents>()
            .Which.Text.Should().Be("# Guide\n");

        var resources = await client.ListResourcesAsync();
        resources.Select(resource => resource.Uri).Should().BeEquivalentTo(
            ["skill://skill-one/SKILL.md", "skill://skill-one/references/guide.md"]);

        var tools = await client.ListToolsAsync();
        tools.Should().ContainSingle(tool => tool.Name == "echo");

        var missingSkill = async () => await client.GetSkillAsync("skill://missing/SKILL.md");
        await missingSkill.Should().ThrowAsync<McpException>();

        var missingResource = async () => await client.ReadResourceAsync("skill://skill-one/missing.md");
        await missingResource.Should().ThrowAsync<McpException>();
    }

    private static string Markdown(string name, string description) =>
        $"---\nname: {name}\ndescription: {description}\n---\n# {name}\n";

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mcp-skills-protocol-tests", Guid.NewGuid().ToString("N"));
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
