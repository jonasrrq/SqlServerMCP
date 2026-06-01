using FluentAssertions;
using SqlServerMCP;
using Xunit;

namespace SqlServerMCP.Tests;

public class QuerySecurityTests
{
    [Theory]
    [InlineData("SELECT * FROM dbo.Clientes")]
    [InlineData("WITH cte AS (SELECT 1 AS Id) SELECT * FROM cte")]
    public void ValidateReadOnlyQuery_AllowsReadOnlyStatements(string query)
    {
        var act = () => QuerySecurity.ValidateReadOnlyQuery(query);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("DROP TABLE dbo.Clientes")]
    [InlineData("DELETE FROM dbo.Clientes")]
    [InlineData("SELECT * FROM dbo.Clientes; DROP TABLE dbo.Clientes")]
    [InlineData("EXEC sp_who2")]
    public void ValidateReadOnlyQuery_BlocksDangerousStatements(string query)
    {
        var act = () => QuerySecurity.ValidateReadOnlyQuery(query);
        act.Should().Throw<InvalidOperationException>();
    }
}
