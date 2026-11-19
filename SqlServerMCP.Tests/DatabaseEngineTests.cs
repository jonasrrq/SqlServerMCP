using System;
using FluentAssertions;
using SqlServerMCP;
using Xunit;

namespace SqlServerMCP.Tests
{
    public class DatabaseEngineTests
    {
        [Fact]
        public void Parse_Null_DefaultsToSqlServer()
        {
            DatabaseEngineSelector.Parse(null).Should().Be(DatabaseEngine.SqlServer);
        }

        [Theory]
        [InlineData("sqlserver")]
        [InlineData("SQLSERVER")]
        [InlineData(" SqlServer ")]
        public void Parse_SupportedValues_ReturnSqlServer(string value)
        {
            DatabaseEngineSelector.Parse(value).Should().Be(DatabaseEngine.SqlServer);
        }

        [Fact]
        public void Parse_UnsupportedValue_ThrowsWithSupportedValues()
        {
            var act = () => DatabaseEngineSelector.Parse("postgresql");

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*postgresql*")
                .WithMessage("*sqlserver*");
        }
    }
}
