using SqlServerMCP;
using Xunit;
using Moq;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlServerMCP.Tests
{
    public class SqlServerMetadataProviderTests
    {
        // Aquí se deben implementar los tests unitarios usando mocks para SqlConnection y SqlCommand.
        // Ejemplo de test de GetColumnsAsync con Moq (requiere refactor para inyectar dependencias):
        // [Fact]
        // public async Task GetColumnsAsync_ReturnsColumns()
        // {
        //     // Arrange: mockear SqlConnection y SqlCommand
        //     // Act: llamar a GetColumnsAsync
        //     // Assert: verificar resultado
        // }

        [Fact]
        public async Task GetColumnsAsync_ReturnsColumns_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedColumns = new List<ColumnInfo>
            {
                new ColumnInfo { Name = "Id", DataType = "int", IsNullable = false, MaxLength = null },
                new ColumnInfo { Name = "Name", DataType = "nvarchar", IsNullable = true, MaxLength = 50 }
            };
            mockProvider.Setup(x => x.GetColumnsAsync("dbo.TestTable")).ReturnsAsync(expectedColumns);

            // Act
            var result = await mockProvider.Object.GetColumnsAsync("dbo.TestTable");

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().ContainSingle(c => c.Name == "Id" && c.DataType == "int" && c.IsNullable == false);
            result.Should().ContainSingle(c => c.Name == "Name" && c.DataType == "nvarchar" && c.IsNullable == true && c.MaxLength == 50);
        }

        [Fact]
        public async Task GetColumnsAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.GetColumnsAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.GetColumnsAsync("dbo.FakeTable"));
        }

        [Fact]
        public async Task GetTablesAsync_ReturnsTables_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedTables = new List<string> { "dbo.Table1", "dbo.Table2" };
            mockProvider.Setup(x => x.GetTablesAsync()).ReturnsAsync(expectedTables);

            // Act
            var result = await mockProvider.Object.GetTablesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain("dbo.Table1");
            result.Should().Contain("dbo.Table2");
        }

        [Fact]
        public async Task GetTablesAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.GetTablesAsync()).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.GetTablesAsync());
        }

        [Fact]
        public async Task GetViewsAsync_ReturnsViews_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedViews = new List<string> { "dbo.View1" };
            mockProvider.Setup(x => x.GetViewsAsync()).ReturnsAsync(expectedViews);

            // Act
            var result = await mockProvider.Object.GetViewsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.Should().Contain("dbo.View1");
        }

        [Fact]
        public async Task GetViewsAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.GetViewsAsync()).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.GetViewsAsync());
        }

        [Fact]
        public async Task GetStoredProceduresAsync_ReturnsProcedures_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedProcs = new List<string> { "dbo.Proc1" };
            mockProvider.Setup(x => x.GetStoredProceduresAsync()).ReturnsAsync(expectedProcs);

            // Act
            var result = await mockProvider.Object.GetStoredProceduresAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.Should().Contain("dbo.Proc1");
        }

        [Fact]
        public async Task GetStoredProceduresAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.GetStoredProceduresAsync()).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.GetStoredProceduresAsync());
        }

        [Fact]
        public async Task GetForeignKeysAsync_ReturnsForeignKeys_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedFks = new List<ForeignKeyInfo>
            {
                new ForeignKeyInfo { Table = "Orders", ForeignKey = "FK_Orders_Customers", ReferencedTable = "Customers", Column = "CustomerID", ReferencedColumn = "CustomerID" }
            };
            mockProvider.Setup(x => x.GetForeignKeysAsync()).ReturnsAsync(expectedFks);

            // Act
            var result = await mockProvider.Object.GetForeignKeysAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.Should().ContainSingle(fk => fk.ForeignKey == "FK_Orders_Customers" && fk.Table == "Orders");
        }

        [Fact]
        public async Task GetForeignKeysAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.GetForeignKeysAsync()).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.GetForeignKeysAsync());
        }

        [Fact]
        public async Task ExecuteQueryAsync_ReturnsRows_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedRows = new List<Dictionary<string, object?>>
            {
                new() { { "Col1", 1 }, { "Col2", "abc" } }
            };
            mockProvider.Setup(x => x.ExecuteQueryAsync("SELECT * FROM Test")).ReturnsAsync(expectedRows);

            // Act
            var result = await mockProvider.Object.ExecuteQueryAsync("SELECT * FROM Test");

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Should().ContainKey("Col1");
            result[0]["Col1"].Should().Be(1);
            result[0]["Col2"].Should().Be("abc");
        }

        [Fact]
        public async Task ExecuteQueryAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.ExecuteQueryAsync("SELECT * FROM Test"));
        }

        [Fact]
        public async Task ExecuteStoredProcedureAsync_ReturnsRows_WithMock()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            var expectedRows = new List<Dictionary<string, object?>>
            {
                new() { { "Result", 42 } }
            };
            mockProvider.Setup(x => x.ExecuteStoredProcedureAsync("sp_test", It.IsAny<Dictionary<string, object>>())).ReturnsAsync(expectedRows);

            // Act
            var result = await mockProvider.Object.ExecuteStoredProcedureAsync("sp_test", new Dictionary<string, object> { { "param1", 1 } });

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Should().ContainKey("Result");
            result[0]["Result"].Should().Be(42);
        }

        [Fact]
        public async Task ExecuteStoredProcedureAsync_ThrowsException_ReturnsError()
        {
            // Arrange
            var mockProvider = new Mock<IMetadataProvider>();
            mockProvider.Setup(x => x.ExecuteStoredProcedureAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>())).ThrowsAsync(new System.Exception("DB error"));

            // Act & Assert
            await Assert.ThrowsAsync<System.Exception>(async () => await mockProvider.Object.ExecuteStoredProcedureAsync("sp_test", new Dictionary<string, object> { { "param1", 1 } }));
        }
    }
    // Fin de la clase SqlServerMetadataProviderTests
}
