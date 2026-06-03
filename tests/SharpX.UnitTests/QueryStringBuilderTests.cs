using FluentAssertions;
using SharpX.Serialization;
using Xunit;

namespace SharpX.UnitTests.Serialization;

public class QueryStringBuilderTests
{
    [Fact]
    public void Build_EncodesScalarValues()
    {
        var query = QueryStringBuilder.Build(new Dictionary<string, object?>
        {
            ["q"] = "hello world",
            ["page"] = 2,
        });

        query.Should().Contain("q=hello%20world").And.Contain("page=2");
    }

    [Fact]
    public void Build_ExpandsArraysWithBrackets()
    {
        var query = QueryStringBuilder.Build(new Dictionary<string, object?>
        {
            ["ids"] = new[] { 1, 2, 3 },
        });

        query.Should().Be("ids%5B%5D=1&ids%5B%5D=2&ids%5B%5D=3");
    }

    [Fact]
    public void Build_OmitsNulls()
    {
        var query = QueryStringBuilder.Build(new Dictionary<string, object?>
        {
            ["a"] = null,
            ["b"] = "x",
        });

        query.Should().Be("b=x");
    }

    [Fact]
    public void AppendQuery_HandlesExistingQuery()
    {
        var url = QueryStringBuilder.AppendQuery("/users?role=admin", new Dictionary<string, object?>
        {
            ["page"] = 2,
        });

        url.Should().Be("/users?role=admin&page=2");
    }

    [Fact]
    public void AppendQuery_AddsQuestionMarkWhenMissing()
    {
        var url = QueryStringBuilder.AppendQuery("/users", new Dictionary<string, object?>
        {
            ["q"] = "x",
        });

        url.Should().Be("/users?q=x");
    }

    [Fact]
    public void Build_FormatsBoolAndDateInvariantly()
    {
        var query = QueryStringBuilder.Build(new Dictionary<string, object?>
        {
            ["flag"] = true,
            ["when"] = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        });

        query.Should().Contain("flag=true");
        query.Should().Contain("when=2024-01-02T03%3A04%3A05.0000000Z");
    }
}
