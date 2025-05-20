using SqlServerMCP;
using Xunit;
using Moq;
using FluentAssertions;
using System.Threading.Tasks;

namespace SqlServerMCP.Tests
{
    public class ProgramTests
    {
        // Los siguientes tests quedan bloqueados porque Program.Main ejecuta el servidor real (Run/RunAsync).
        // Se recomienda comentarlos o eliminarlos para evitar bloqueos en la suite de tests.
        /*
        [Fact]
        public async Task Main_StdioMode_ConfiguresStdioServer()
        {
            // Arrange
            var args = new[] { "--mode", "stdio", "--server", "localhost", "--database", "Northwind", "--user", "sa", "--password", "test" };
            // Act & Assert
            await Program.Main(args);
            // No excepción = configuración exitosa (en entorno real, se mockearía el host)
        }

        [Fact]
        public async Task Main_SseMode_ConfiguresSseServer()
        {
            // Arrange
            var args = new[] { "--mode", "sse", "--server", "localhost", "--database", "Northwind", "--user", "sa", "--password", "test" };
            // Act & Assert
            await Program.Main(args);
            // No excepción = configuración exitosa (en entorno real, se mockearía el host)
        }
        */

        [Fact]
        public void ConfigureServer_StdioMode_ConfiguresCorrectly()
        {
            // Arrange
            var args = new[] { "--mode", "stdio", "--server", "localhost", "--database", "Northwind", "--user", "sa", "--password", "test" };
            // Act
            Program.ConfigureServer(args, out var mode, out var connectionString);
            // Assert
            mode.Should().Be("stdio");
            connectionString.Should().Contain("Server=localhost");
            connectionString.Should().Contain("Database=Northwind");
            connectionString.Should().Contain("User Id=sa");
            connectionString.Should().Contain("Password=test");
        }

        [Fact]
        public void ConfigureServer_SseMode_ConfiguresCorrectly()
        {
            // Arrange
            var args = new[] { "--mode", "sse", "--server", "myserver", "--database", "MyDb", "--user", "admin", "--password", "pw" };
            // Act
            Program.ConfigureServer(args, out var mode, out var connectionString);
            // Assert
            mode.Should().Be("sse");
            connectionString.Should().Contain("Server=myserver");
            connectionString.Should().Contain("Database=MyDb");
            connectionString.Should().Contain("User Id=admin");
            connectionString.Should().Contain("Password=pw");
        }
    }
}
