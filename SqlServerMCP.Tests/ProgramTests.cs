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
            var oldPassword = Environment.GetEnvironmentVariable("SQLSERVER_PASSWORD");
            Environment.SetEnvironmentVariable("SQLSERVER_PASSWORD", "test-from-env");

            // Arrange
            var args = new[] { "--mode", "stdio", "--server", "localhost", "--database", "Northwind", "--user", "sa", "--password", "test" };
            try
            {
                // Act
                Program.ConfigureServer(args, out var mode, out var connectionString);

                // Assert
                mode.Should().Be("stdio");
                connectionString.Should().Contain("Data Source=localhost");
                connectionString.Should().Contain("Initial Catalog=Northwind");
                connectionString.Should().Contain("User ID=sa");
                connectionString.Should().Contain("Password=test-from-env");
            }
            finally
            {
                Environment.SetEnvironmentVariable("SQLSERVER_PASSWORD", oldPassword);
            }
        }

        [Fact]
        public void ConfigureServer_SseMode_ConfiguresCorrectly()
        {
            var oldPassword = Environment.GetEnvironmentVariable("SQLSERVER_PASSWORD");
            Environment.SetEnvironmentVariable("SQLSERVER_PASSWORD", "pw-from-env");

            // Arrange
            var args = new[] { "--mode", "sse", "--server", "myserver", "--database", "MyDb", "--user", "admin", "--password", "pw" };
            try
            {
                // Act
                Program.ConfigureServer(args, out var mode, out var connectionString);

                // Assert
                mode.Should().Be("sse");
                connectionString.Should().Contain("Data Source=myserver");
                connectionString.Should().Contain("Initial Catalog=MyDb");
                connectionString.Should().Contain("User ID=admin");
                connectionString.Should().Contain("Password=pw-from-env");
            }
            finally
            {
                Environment.SetEnvironmentVariable("SQLSERVER_PASSWORD", oldPassword);
            }
        }

        [Fact]
        public void ConfigureServer_WithoutPasswordInEnvironment_Throws()
        {
            var oldPassword = Environment.GetEnvironmentVariable("SQLSERVER_PASSWORD");
            Environment.SetEnvironmentVariable("SQLSERVER_PASSWORD", null);

            var args = new[] { "--mode", "stdio" };

            try
            {
                var act = () => Program.ConfigureServer(args, out _, out _);
                act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*SQLSERVER_PASSWORD*");
            }
            finally
            {
                Environment.SetEnvironmentVariable("SQLSERVER_PASSWORD", oldPassword);
            }
        }
    }
}
