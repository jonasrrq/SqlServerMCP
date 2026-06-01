using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SqlServerMCP;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SqlServerMCP.Tests
{
    public class AuditLoggerTests
    {
        [Fact]
        public async Task ExecuteQuery_LogsAuditEntry_OnSuccess()
        {
            // Arrange
            var services = new ServiceCollection();
            var audit = new InMemoryAuditLogger(100);
            services.AddSingleton<IAuditLogger>(audit);
            var sp = services.BuildServiceProvider();
            ServiceProviderAccessor.Current = sp;

            var mock = new Mock<IMetadataProvider>();
            var expectedRows = new List<Dictionary<string, object?>> { new() { { "Col1", 1 } } };
            mock.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>())).ReturnsAsync(expectedRows);

            // Act
            var result = await MetadataTool.ExecuteQuery(mock.Object, "SELECT 1");

            // Assert
            var entries = audit.GetEntries();
            entries.Should().NotBeNullOrEmpty();
            entries[0].Tool.Should().Be("ExecuteQuery");
            ((string)entries[0].Parameters["query"]).Should().Contain("SELECT 1");
        }
    }
}
