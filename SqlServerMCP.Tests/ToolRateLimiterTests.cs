using FluentAssertions;
using SqlServerMCP;
using Xunit;

namespace SqlServerMCP.Tests;

public class ToolRateLimiterTests
{
    [Fact]
    public void TryAcquire_RespectsWindowLimit()
    {
        var limiter = new ToolRateLimiter(2, TimeSpan.FromSeconds(60));

        limiter.TryAcquire("ExecuteQuery", out var retry1).Should().BeTrue();
        retry1.Should().Be(0);

        limiter.TryAcquire("ExecuteQuery", out var retry2).Should().BeTrue();
        retry2.Should().Be(0);

        limiter.TryAcquire("ExecuteQuery", out var retry3).Should().BeFalse();
        retry3.Should().BeGreaterThan(0);
    }
}
