using Shelfy.Core.UseCases.Search;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class SearchQueryTests
{
    [Fact]
    public void Parse_WithEmptyString_ReturnsEmptyQuery()
    {
        // Act
        var query = SearchQuery.Parse("");

        // Assert
        Assert.Empty(query.FreeText);
        Assert.Null(query.ShelfFilter);
        Assert.Null(query.TypeFilter);
        Assert.Null(query.InShelfFilter);
    }

    [Fact]
    public void Parse_WithNull_ReturnsEmptyQuery()
    {
        // Act
        var query = SearchQuery.Parse(null);

        // Assert
        Assert.Empty(query.FreeText);
    }

    [Fact]
    public void Parse_WithFreeTextOnly_ReturnsFreeText()
    {
        // Act
        var query = SearchQuery.Parse("hello world");

        // Assert
        Assert.Equal("hello world", query.FreeText);
        Assert.Null(query.ShelfFilter);
        Assert.Null(query.TypeFilter);
        Assert.Null(query.InShelfFilter);
    }

    [Fact]
    public void Parse_WithBoxPrefix_ExtractsShelfFilter()
    {
        // Act
        var query = SearchQuery.Parse("document box:Work");

        // Assert
        Assert.Equal("document", query.FreeText);
        Assert.Equal("Work", query.ShelfFilter);
    }

    [Fact]
    public void Parse_WithTypePrefix_ExtractsTypeFilter()
    {
        // Act
        var query = SearchQuery.Parse("readme type:file");

        // Assert
        Assert.Equal("readme", query.FreeText);
        Assert.Equal("file", query.TypeFilter);
    }

    [Fact]
    public void Parse_WithInPrefix_ExtractsInShelfFilter()
    {
        // Act
        var query = SearchQuery.Parse("config in:Projects");

        // Assert
        Assert.Equal("config", query.FreeText);
        Assert.Equal("Projects", query.InShelfFilter);
    }

    [Fact]
    public void Parse_WithMultipleFilters_ExtractsAll()
    {
        // Act
        var query = SearchQuery.Parse("readme box:Work type:file in:Documents");

        // Assert
        Assert.Equal("readme", query.FreeText);
        Assert.Equal("Work", query.ShelfFilter);
        Assert.Equal("file", query.TypeFilter);
        Assert.Equal("Documents", query.InShelfFilter);
    }

    [Fact]
    public void Parse_WithOnlyFilters_ReturnsEmptyFreeText()
    {
        // Act
        var query = SearchQuery.Parse("type:folder");

        // Assert
        Assert.Empty(query.FreeText);
        Assert.Equal("folder", query.TypeFilter);
    }

    [Theory]
    [InlineData("BOX:Work", "Work")]
    [InlineData("Box:Projects", "Projects")]
    [InlineData("box:PERSONAL", "PERSONAL")]
    public void Parse_BoxPrefix_IsCaseInsensitive(string input, string expected)
    {
        // Act
        var query = SearchQuery.Parse(input);

        // Assert
        Assert.Equal(expected, query.ShelfFilter);
    }
}
