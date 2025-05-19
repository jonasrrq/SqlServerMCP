using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.AspNetCore.Builder; // Necesario para WebApplication
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SqlServerMCP;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Parámetros: --mode stdio|sse --server <servidor> --database <db> --user <usuario> --password <clave>
        string mode = args.FirstOrDefault(a => a == "--mode") != null ? args[Array.IndexOf(args, "--mode") + 1] : "sse";
        string server = args.FirstOrDefault(a => a == "--server") != null ? args[Array.IndexOf(args, "--server") + 1] : "localhost";
        string database = args.FirstOrDefault(a => a == "--database") != null ? args[Array.IndexOf(args, "--database") + 1] : "Northwind";
        string user = args.FirstOrDefault(a => a == "--user") != null ? args[Array.IndexOf(args, "--user") + 1] : "sa";
        string password = args.FirstOrDefault(a => a == "--password") != null ? args[Array.IndexOf(args, "--password") + 1] : "jr123456789JR#";

        string connectionString = $"Server={server};Database={database};User Id={user};Password={password};TrustServerCertificate=True;";

        if (mode == "sse")
        {
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<IMetadataProvider>(_ => new SqlServerMetadataProvider(connectionString));
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly();
            var app = builder.Build();
            app.MapMcp();
            app.Run("http://localhost:5000");
        }
        else // stdio por defecto
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSingleton<IMetadataProvider>(_ => new SqlServerMetadataProvider(connectionString));
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
            await builder.Build().RunAsync();
        }
    }
}
