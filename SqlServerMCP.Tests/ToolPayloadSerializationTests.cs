using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using SqlServerMCP;
using Xunit;

namespace SqlServerMCP.Tests
{
    /// <summary>
    /// Locks the MCP envelope emitted for the anonymous-object tool results under ModelContextProtocol 2.x.
    /// Structured content is opt-in (UseStructuredContent defaults to false), so results travel as a single
    /// JSON text block with no outputSchema and no structuredContent.
    /// </summary>
    public class ToolPayloadSerializationTests
    {
        [Fact]
        public async Task GetMetadata_EmitsJsonTextBlock_WithoutStructuredContent()
        {
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.GetTablesAsync()).ReturnsAsync(new List<string> { "dbo.Table1" });
            mock.Setup(x => x.GetViewsAsync()).ReturnsAsync(new List<string> { "dbo.View1" });
            mock.Setup(x => x.GetStoredProceduresAsync()).ReturnsAsync(new List<string> { "dbo.Proc1" });
            mock.Setup(x => x.GetForeignKeysAsync()).ReturnsAsync(new List<ForeignKeyInfo>());

            var tool = CreateTool(nameof(MetadataTool.GetMetadata), mock.Object, out var services);
            var result = await InvokeAsync(tool, services, new Dictionary<string, JsonElement>());

            result.StructuredContent.Should().BeNull();
            tool.ProtocolTool.OutputSchema.Should().BeNull();

            var text = SingleTextBlock(result);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            root.TryGetProperty("tables", out var tables).Should().BeTrue();
            tables.GetArrayLength().Should().Be(1);
            root.TryGetProperty("structured", out var structured).Should().BeTrue();
            structured.GetProperty("tables")[0].GetString().Should().Be("dbo.Table1");
        }

        [Fact]
        public async Task ExecuteQuery_EmitsJsonTextBlock_WithoutStructuredContent()
        {
            var rows = new List<Dictionary<string, object?>>
            {
                new() { { "Col1", 1 }, { "Col2", "a" } }
            };

            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(rows);
            mock.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>())).ReturnsAsync(rows);

            var tool = CreateTool(nameof(MetadataTool.ExecuteQuery), mock.Object, out var services);
            var result = await InvokeAsync(tool, services, new Dictionary<string, JsonElement>
            {
                ["query"] = JsonSerializer.SerializeToElement("SELECT Col1, Col2 FROM dbo.Table1")
            });

            result.StructuredContent.Should().BeNull();
            tool.ProtocolTool.OutputSchema.Should().BeNull();

            var text = SingleTextBlock(result);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            root.GetProperty("rows").GetArrayLength().Should().Be(1);
            root.GetProperty("columns").GetArrayLength().Should().Be(2);
            root.GetProperty("pagination").GetProperty("pageSize").GetInt32().Should().Be(100);
        }

        private static McpServerTool CreateTool(string methodName, IMetadataProvider provider, out IServiceProvider services)
        {
            services = new ServiceCollection()
                .AddSingleton(provider)
                .BuildServiceProvider();

            return McpServerTool.Create(
                typeof(MetadataTool).GetMethod(methodName)!,
                target: null,
                new McpServerToolCreateOptions { Services = services });
        }

        private static async Task<CallToolResult> InvokeAsync(McpServerTool tool, IServiceProvider services, Dictionary<string, JsonElement> arguments)
        {
            var transport = new StreamServerTransport(Stream.Null, Stream.Null, "payload-test");
            var server = McpServer.Create(transport, new McpServerOptions(), NullLoggerFactory.Instance, services);
            var request = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = arguments };
            var context = new RequestContext<CallToolRequestParams>(server, new JsonRpcRequest { Method = "tools/call" }, request);

            return await tool.InvokeAsync(context, default);
        }

        private static string SingleTextBlock(CallToolResult result)
        {
            result.Content.Should().ContainSingle();
            var block = result.Content.Single().Should().BeOfType<TextContentBlock>().Subject;
            return block.Text;
        }
    }
}
