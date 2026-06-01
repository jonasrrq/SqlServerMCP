using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.Text;

namespace SqlServerMCP;

public class Program
{
    private const int DefaultConnectTimeoutSeconds = 30;

    public static void ConfigureServer(string[] args, out string mode, out string connectionString)
    {
        mode = GetArgValue(args, "--mode")
            ?? Environment.GetEnvironmentVariable("MCP_MODE")
            ?? "http";

        var fromEnvironment = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            var existingBuilder = new SqlConnectionStringBuilder(fromEnvironment)
            {
                Encrypt = ParseEncryptOption(Environment.GetEnvironmentVariable("SQLSERVER_ENCRYPT")),
                TrustServerCertificate = ParseBool(Environment.GetEnvironmentVariable("SQLSERVER_TRUST_SERVER_CERTIFICATE"), false),
                ConnectTimeout = ParseInt(Environment.GetEnvironmentVariable("SQLSERVER_CONNECT_TIMEOUT"), DefaultConnectTimeoutSeconds)
            };
            connectionString = existingBuilder.ConnectionString;
            return;
        }

        var server = GetArgValue(args, "--server")
            ?? Environment.GetEnvironmentVariable("SQLSERVER_SERVER")
            ?? "localhost";

        var database = GetArgValue(args, "--database")
            ?? Environment.GetEnvironmentVariable("SQLSERVER_DATABASE")
            ?? "Northwind";

        var user = GetArgValue(args, "--user")
            ?? Environment.GetEnvironmentVariable("SQLSERVER_USER")
            ?? "sa";

        // Por seguridad no aceptamos --password en CLI para evitar historial y exposición en procesos.
        var password = Environment.GetEnvironmentVariable("SQLSERVER_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Falta SQLSERVER_PASSWORD en variables de entorno. Usa .env/secrets manager y evita --password.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = ParseEncryptOption(Environment.GetEnvironmentVariable("SQLSERVER_ENCRYPT")),
            TrustServerCertificate = ParseBool(Environment.GetEnvironmentVariable("SQLSERVER_TRUST_SERVER_CERTIFICATE"), false),
            ConnectTimeout = ParseInt(Environment.GetEnvironmentVariable("SQLSERVER_CONNECT_TIMEOUT"), DefaultConnectTimeoutSeconds)
        };

        connectionString = builder.ConnectionString;
    }

    public static async Task Main(string[] args)
    {
        LoadDotEnvIfPresent();

        ConfigureServer(args, out var mode, out var connectionString);
        var isHttpMode = mode.Equals("http", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("sse", StringComparison.OrdinalIgnoreCase);

        if (isHttpMode)
        {
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

            // Register audit logger
            builder.Services.AddSingleton<IAuditLogger>(_ => new InMemoryAuditLogger(ParseInt(Environment.GetEnvironmentVariable("MCP_AUDIT_MAX_ENTRIES"), 1000)));

            var defaultMaxRows = ParseInt(Environment.GetEnvironmentVariable("MCP_QUERY_MAX_ROWS"), 1000);
            var defaultTimeout = ParseInt(Environment.GetEnvironmentVariable("MCP_QUERY_TIMEOUT_SECONDS"), 30);
            var metadataCacheTtl = ParseInt(Environment.GetEnvironmentVariable("MCP_METADATA_CACHE_TTL_SECONDS"), 60);
            var enableLegacySse = ParseBool(Environment.GetEnvironmentVariable("MCP_ENABLE_LEGACY_SSE"), false);

            builder.Services.AddSingleton<IMetadataProvider>(_ =>
                new SqlServerMetadataProvider(() => new SqlConnection(connectionString), defaultTimeout, defaultMaxRows, metadataCacheTtl));

            builder.Services.AddMcpServer()
                .WithHttpTransport(options =>
                {
                    // Streamable HTTP stateless por defecto.
                    options.Stateless = true;

                    if (enableLegacySse)
                    {
                        // Si se habilita SSE legado, requiere modo stateful.
                        options.Stateless = false;
#pragma warning disable MCP9004
                        options.EnableLegacySse = true;
#pragma warning restore MCP9004
                    }
                })
                .WithToolsFromAssembly();

            var app = builder.Build();
            // expose service provider for static access to best-effort loggers
            ServiceProviderAccessor.Current = app.Services;
            app.MapMcp();
            await app.RunAsync(Environment.GetEnvironmentVariable("MCP_HTTP_URL") ?? "http://localhost:5000");
        }
        else // stdio por defecto
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSingleton<IAuditLogger>(_ => new InMemoryAuditLogger(ParseInt(Environment.GetEnvironmentVariable("MCP_AUDIT_MAX_ENTRIES"), 1000)));
            var defaultMaxRows = ParseInt(Environment.GetEnvironmentVariable("MCP_QUERY_MAX_ROWS"), 1000);
            var defaultTimeout = ParseInt(Environment.GetEnvironmentVariable("MCP_QUERY_TIMEOUT_SECONDS"), 30);
            var metadataCacheTtl = ParseInt(Environment.GetEnvironmentVariable("MCP_METADATA_CACHE_TTL_SECONDS"), 60);

            builder.Services.AddSingleton<IMetadataProvider>(_ =>
                new SqlServerMetadataProvider(() => new SqlConnection(connectionString), defaultTimeout, defaultMaxRows, metadataCacheTtl));

            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            var host = builder.Build();
            ServiceProviderAccessor.Current = host.Services;
            await host.RunAsync();
        }
    }

    private static string? GetArgValue(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return null;
    }

    private static bool ParseBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;

    private static SqlConnectionEncryptOption ParseEncryptOption(string? value)
    {
        if (value is null)
            return SqlConnectionEncryptOption.Strict;

        return value.Trim().ToLowerInvariant() switch
        {
            "mandatory" => SqlConnectionEncryptOption.Mandatory,
            "optional" => SqlConnectionEncryptOption.Optional,
            "strict" => SqlConnectionEncryptOption.Strict,
            _ => SqlConnectionEncryptOption.Strict
        };
    }

    private static void LoadDotEnvIfPresent()
    {
        if (ParseBool(Environment.GetEnvironmentVariable("MCP_DISABLE_DOTENV"), false))
            return;

        var envPath = FindFileInParents(Directory.GetCurrentDirectory(), ".env")
            ?? FindFileInParents(AppContext.BaseDirectory, ".env");

        if (envPath is null)
            return;

        foreach (var rawLine in File.ReadLines(envPath, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            // No sobreescribir variables que ya estén definidas en el entorno.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindFileInParents(string startDirectory, string fileName)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }
}
