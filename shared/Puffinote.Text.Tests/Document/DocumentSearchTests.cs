using Puffinote.Text.Document;
using Xunit;

namespace Puffinote.Text.Tests.Document;

public class DocumentSearchTests
{
    [Fact]
    public void Plain_FindsAllNonOverlapping()
    {
        var matches = DocumentSearch.FindAll("ababab", "ab", useRegex: false, caseSensitive: true, TestContext.Current.CancellationToken);
        Assert.Equal(3, matches.Count);
        Assert.Equal([0, 2, 4], matches.Select(m => m.Start));
        Assert.All(matches, m => Assert.Equal(2, m.Length));
    }

    [Fact]
    public void Plain_CaseInsensitiveByOption()
    {
        Assert.Single(DocumentSearch.FindAll("Hello", "hello", useRegex: false, caseSensitive: false, TestContext.Current.CancellationToken));
        Assert.Empty(DocumentSearch.FindAll("Hello", "hello", useRegex: false, caseSensitive: true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Regex_Matches()
    {
        var matches = DocumentSearch.FindAll("a1 b22 c333", @"\d+", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "1", "22", "333" }.Length, matches.Count);
        Assert.Equal(3, matches[2].Length);
    }

    [Fact]
    public void Regex_InvalidPattern_YieldsNoMatches_NotThrow()
    {
        Assert.Empty(DocumentSearch.FindAll("text", "(", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken));
        Assert.False(DocumentSearch.IsValidRegex("("));
        Assert.True(DocumentSearch.IsValidRegex(@"\d+"));
    }

    [Fact]
    public void Regex_SkipsZeroWidthMatches()
    {
        // "a*" can match empty strings; those must be skipped so navigation can't cycle in place.
        var matches = DocumentSearch.FindAll("baa", "a*", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken);
        Assert.All(matches, m => Assert.True(m.Length > 0));
    }

    [Fact]
    public void EmptyQuery_OrEmptyText_YieldsNothing()
    {
        Assert.Empty(DocumentSearch.FindAll("text", string.Empty, false, true, TestContext.Current.CancellationToken));
        Assert.Empty(DocumentSearch.FindAll(string.Empty, "x", false, true, TestContext.Current.CancellationToken));
    }
}
