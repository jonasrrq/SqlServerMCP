using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SqlServerMCP.Skills;
using Xunit;

namespace SqlServerMCP.Tests;

public class SkillOptionsTests
{
    [Fact]
    public void FromConfiguration_BindsSkillsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Skills:Enabled"] = "false",
                ["Skills:IncludeEmbedded"] = "true",
                ["Skills:ExternalDirectories:0"] = "C:\\skills-a",
                ["Skills:ExternalDirectories:1"] = "C:\\skills-b",
                ["Skills:ReloadOnChange"] = "true",
                ["Skills:PageSize"] = "5000",
                ["Skills:MaxFilesPerSkill"] = "0",
                ["Skills:ConflictPolicy"] = "LastWins"
            })
            .Build();

        var options = SkillOptions.FromConfiguration(configuration);

        options.Enabled.Should().BeFalse();
        options.IncludeEmbedded.Should().BeTrue();
        options.ExternalDirectories.Should().BeEquivalentTo(["C:\\skills-a", "C:\\skills-b"]);
        options.ReloadOnChange.Should().BeTrue();
        options.PageSize.Should().Be(1000);
        options.MaxFilesPerSkill.Should().Be(1);
        options.ConflictPolicy.Should().Be(SkillConflictPolicy.LastWins);
    }

    [Fact]
    public void FromConfiguration_EnvironmentAliasOverridesSection()
    {
        var previous = Environment.GetEnvironmentVariable("MCP_SKILLS_CONFLICT_POLICY");
        try
        {
            Environment.SetEnvironmentVariable("MCP_SKILLS_CONFLICT_POLICY", "FirstWins");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Skills:ConflictPolicy"] = "LastWins"
                })
                .Build();

            SkillOptions.FromConfiguration(configuration).ConflictPolicy
                .Should().Be(SkillConflictPolicy.FirstWins);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_SKILLS_CONFLICT_POLICY", previous);
        }
    }
}
