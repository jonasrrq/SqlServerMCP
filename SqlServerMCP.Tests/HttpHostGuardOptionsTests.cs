using FluentAssertions;
using SqlServerMCP;

namespace SqlServerMCP.Tests;

public class HttpHostGuardOptionsTests
{
    [Fact]
    public void FromEnvironment_Defaults_Disabled()
    {
        using var scope = new EnvScope(("MCP_HTTP_HOST_GUARD_ENABLED", null), ("MCP_ALLOWED_HOSTS", null), ("MCP_ALLOWED_ORIGINS", null));

        var opts = HttpHostGuardOptions.FromEnvironment();

        opts.Enabled.Should().BeFalse();
        opts.AllowedHosts.Should().BeEmpty();
        opts.AllowedOrigins.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenEnabledWithoutLists_Throws()
    {
        using var scope = new EnvScope(("MCP_HTTP_HOST_GUARD_ENABLED", "true"), ("MCP_ALLOWED_HOSTS", null), ("MCP_ALLOWED_ORIGINS", null));

        var opts = HttpHostGuardOptions.FromEnvironment();

        Action act = () => opts.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsHostAllowed_ExactMatch_ReturnsTrue()
    {
        var opts = new HttpHostGuardOptions { AllowedHosts = new[] { "example.com" } };
        opts.IsHostAllowed("example.com").Should().BeTrue();
    }

    [Fact]
    public void IsHostAllowed_WildcardSubdomain_ReturnsTrue()
    {
        var opts = new HttpHostGuardOptions { AllowedHosts = new[] { "*.example.com" } };
        opts.IsHostAllowed("api.example.com").Should().BeTrue();
        opts.IsHostAllowed("example.com").Should().BeTrue();
    }

    [Fact]
    public void IsOriginAllowed_WithSchemeAndHost_ReturnsTrue()
    {
        var opts = new HttpHostGuardOptions { AllowedOrigins = new[] { "https://app.example.com" } };
        opts.IsOriginAllowed("https://app.example.com").Should().BeTrue();
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _original = new();

        public EnvScope(params (string k, string? v)[] items)
        {
            foreach (var (k, v) in items)
            {
                _original[k] = Environment.GetEnvironmentVariable(k);
                Environment.SetEnvironmentVariable(k, v);
            }
        }

        public void Dispose()
        {
            foreach (var kv in _original)
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
        }
    }
}
