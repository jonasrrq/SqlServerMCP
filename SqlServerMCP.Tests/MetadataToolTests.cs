using SqlServerMCP;
using Xunit;
using Moq;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlServerMCP.Tests
{
    public class MetadataToolTests
    {
        [Fact]
        public async Task GetMetadata_ReturnsAllMetadata_WithMock()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.GetTablesAsync()).ReturnsAsync(new List<string> { "dbo.Table1" });
            mock.Setup(x => x.GetViewsAsync()).ReturnsAsync(new List<string> { "dbo.View1" });
            mock.Setup(x => x.GetStoredProceduresAsync()).ReturnsAsync(new List<string> { "dbo.Proc1" });
            mock.Setup(x => x.GetForeignKeysAsync()).ReturnsAsync(new List<ForeignKeyInfo>());

            // Act
            var result = await MetadataTool.GetMetadata(mock.Object);

            // Assert
            result.Should().NotBeNull();
            // Validar que el resultado tiene las propiedades esperadas
            var resultType = result.GetType();
            resultType.GetProperty("tables").Should().NotBeNull();
            resultType.GetProperty("views").Should().NotBeNull();
            resultType.GetProperty("procedures").Should().NotBeNull();
            resultType.GetProperty("foreignKeys").Should().NotBeNull();
        }

        [Fact]
        public async Task GetMetadata_ThrowsException_ReturnsError()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.GetTablesAsync()).ThrowsAsync(new System.Exception("DB error"));

            // Act
            var result = await MetadataTool.GetMetadata(mock.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeAssignableTo<object>();
            result.ToString().Should().Contain("error");
        }

        [Fact]
        public async Task ExecuteQuery_ReturnsRows_WithMock()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            var expectedRows = new List<Dictionary<string, object?>>
            {
                new() { { "Col1", 1 } }
            };
            mock.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>())).ReturnsAsync(expectedRows);

            // Act
            var result = await MetadataTool.ExecuteQuery(mock.Object, "SELECT 1");

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ExecuteQuery_ThrowsException_ReturnsError()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("DB error"));

            // Act
            var result = await MetadataTool.ExecuteQuery(mock.Object, "SELECT 1");

            // Assert
            result.Should().NotBeNull();
            result.ToString().Should().Contain("error");
        }

        [Fact]
        public async Task ExecuteStoredProcedure_ReturnsRows_WithMock()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            var expectedRows = new List<Dictionary<string, object?>>
            {
                new() { { "Col1", 1 } }
            };
            mock.Setup(x => x.ExecuteStoredProcedureAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>())).ReturnsAsync(expectedRows);

            // Act
            var result = await MetadataTool.ExecuteStoredProcedure(mock.Object, "sp_test", new Dictionary<string, object> { { "param1", 1 } });

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ExecuteStoredProcedure_ThrowsException_ReturnsError()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.ExecuteStoredProcedureAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>())).ThrowsAsync(new System.Exception("DB error"));

            // Act
            var result = await MetadataTool.ExecuteStoredProcedure(mock.Object, "sp_test", new Dictionary<string, object> { { "param1", 1 } });

            // Assert
            result.Should().NotBeNull();
            result.ToString().Should().Contain("error");
        }

        [Fact]
        public async Task GetColumns_ReturnsColumns_WithMock()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            var expectedColumns = new List<ColumnInfo>
            {
                new ColumnInfo { Name = "Id", DataType = "int", IsNullable = false, MaxLength = null }
            };
            mock.Setup(x => x.GetColumnsAsync(It.IsAny<string>())).ReturnsAsync(expectedColumns);

            // Act
            var result = await MetadataTool.GetColumns(mock.Object, "dbo.Table1");

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetColumns_ThrowsException_ReturnsError()
        {
            // Arrange
            var mock = new Mock<IMetadataProvider>();
            mock.Setup(x => x.GetColumnsAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("DB error"));

            // Act
            var result = await MetadataTool.GetColumns(mock.Object, "dbo.Table1");

            // Assert
            result.Should().NotBeNull();
            result.ToString().Should().Contain("error");
        }
    }
}
