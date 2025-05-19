using SqlServerMCP;
using Xunit;
using Moq;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlServerMCP.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }
    }

    public class SqlServerMetadataProviderTests
    {
        [Fact]
        public async Task GetTablesAsync_ReturnsTables()
        {
            // Arrange
            var provider = new SqlServerMetadataProvider("Server=localhost;Database=master;Trusted_Connection=True;");
            var result = await provider.GetTablesAsync();
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetViewsAsync_ReturnsViews()
        {
            var provider = new SqlServerMetadataProvider("Server=localhost;Database=master;Trusted_Connection=True;");
            var result = await provider.GetViewsAsync();
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetStoredProceduresAsync_ReturnsProcedures()
        {
            var provider = new SqlServerMetadataProvider("Server=localhost;Database=master;Trusted_Connection=True;");
            var result = await provider.GetStoredProceduresAsync();
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetForeignKeysAsync_ReturnsForeignKeys()
        {
            var provider = new SqlServerMetadataProvider("Server=localhost;Database=master;Trusted_Connection=True;");
            var result = await provider.GetForeignKeysAsync();
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ExecuteQueryAsync_ReturnsRows()
        {
            var provider = new SqlServerMetadataProvider("Server=localhost;Database=master;Trusted_Connection=True;");
            var result = await provider.ExecuteQueryAsync("SELECT 1 AS TestColumn");
            result.Should().NotBeNull();
            result.Count.Should().BeGreaterOrEqualTo(1);
            result[0].Should().ContainKey("TestColumn");
        }

        [Fact]
        public async Task ExecuteStoredProcedureAsync_ReturnsRows()
        {
            var provider = new SqlServerMetadataProvider("Server=localhost;Database=master;Trusted_Connection=True;");
            // Este test requiere un procedimiento almacenado de prueba, por ejemplo 'sp_who'
            var result = await provider.ExecuteStoredProcedureAsync("sp_who", null);
            result.Should().NotBeNull();
            result.Count.Should().BeGreaterOrEqualTo(1);
        }
    }

    public class MetadataToolTests
    {
        [Fact]
        public async Task GetMetadata_ReturnsAllMetadata()
        {
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.GetTablesAsync()).ReturnsAsync(new List<string> { "dbo.Table1" });
            mock.Setup(x => x.GetViewsAsync()).ReturnsAsync(new List<string> { "dbo.View1" });
            mock.Setup(x => x.GetStoredProceduresAsync()).ReturnsAsync(new List<string> { "dbo.Proc1" });
            mock.Setup(x => x.GetForeignKeysAsync()).ReturnsAsync(new List<ForeignKeyInfo>());
            var result = await MetadataTool.GetMetadata(mock.Object);
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ExecuteQuery_ReturnsRows()
        {
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>())).ReturnsAsync(new List<Dictionary<string, object?>>
            {
                new() { { "Col1", 1 } }
            });
            var result = await MetadataTool.ExecuteQuery(mock.Object, "SELECT 1");
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ExecuteStoredProcedure_ReturnsRows()
        {
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.ExecuteStoredProcedureAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(new List<Dictionary<string, object?>> { new() { { "Col1", 1 } } });
            var result = await MetadataTool.ExecuteStoredProcedure(mock.Object, "sp_test", new Dictionary<string, object> { { "param1", 1 } });
            result.Should().NotBeNull();
        }
    }
}
