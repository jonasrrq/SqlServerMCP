using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SqlServerMCP;

namespace SqlServerMCP.Tests;

public class HttpAuthOptionsTests
{
    [Fact]
    public void FromEnvironment_WithoutFlags_DisablesAuth()
    {
        using var scope = new EnvironmentVariableScope(
            ("MCP_HTTP_AUTH_ENABLED", null),
            ("MCP_AUTH_TOKEN", null),
            ("MCP_AUTH_HEADER_NAME", null));

        var options = HttpAuthOptions.FromEnvironment();

        options.Enabled.Should().BeFalse();
        options.HeaderName.Should().Be(HttpAuthOptions.DefaultHeaderName);
        options.Token.Should().BeNull();
    }

    [Fact]
    public void Validate_WhenEnabledWithoutToken_Throws()
    {
        using var scope = new EnvironmentVariableScope(
            ("MCP_HTTP_AUTH_ENABLED", "true"),
            ("MCP_AUTH_TOKEN", null));

        var options = HttpAuthOptions.FromEnvironment();

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MCP_AUTH_TOKEN*");
    }

    [Fact]
    public void IsAuthorized_WhenDisabled_ReturnsTrue()
    {
        var headers = new HeaderDictionary();
        var options = new HttpAuthOptions { Enabled = false };

        HttpAuthEvaluator.IsAuthorized(options, headers).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_WithCustomHeaderToken_ReturnsTrue()
    {
        var headers = new HeaderDictionary
        {
            ["X-MCP-Auth"] = "secret-token"
        };

        var options = new HttpAuthOptions
        {
            Enabled = true,
            HeaderName = "X-MCP-Auth",
            Token = "secret-token"
        };

        HttpAuthEvaluator.IsAuthorized(options, headers).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_WithBearerToken_ReturnsTrue()
    {
        var headers = new HeaderDictionary
        {
            ["Authorization"] = "Bearer secret-token"
        };

        var options = new HttpAuthOptions
        {
            Enabled = true,
            HeaderName = "X-MCP-Auth",
            Token = "secret-token"
        };

        HttpAuthEvaluator.IsAuthorized(options, headers).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_WithWrongToken_ReturnsFalse()
    {
        var headers = new HeaderDictionary
        {
            ["X-MCP-Auth"] = "wrong-token"
        };

        var options = new HttpAuthOptions
        {
            Enabled = true,
            HeaderName = "X-MCP-Auth",
            Token = "secret-token"
        };

        HttpAuthEvaluator.IsAuthorized(options, headers).Should().BeFalse();
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public EnvironmentVariableScope(params (string Key, string? Value)[] values)
        {
            foreach (var (key, value) in values)
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var item in _originalValues)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }
    }
}