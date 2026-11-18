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

    [Fact]
    public void Whole_word_skips_matches_inside_longer_words()
    {
        var ct = TestContext.Current.CancellationToken;
        var matches = DocumentSearch.FindAll("cat concat cat_x cat. 2cat cat", "cat", useRegex: false, caseSensitive: true, wholeWord: true, ct);
        Assert.Equal<int>([0, 17, 27], matches.Select(m => m.Start));
        Assert.Equal(6, DocumentSearch.FindAll("cat concat cat_x cat. 2cat cat", "cat", false, true, ct).Count); // off: every one
    }

    [Fact]
    public void Whole_word_finds_a_word_that_follows_a_rejected_hit_inside_it()
    {
        // "aa" in "aaa aa": the hits at 0 and 1 run into a word; the one at 4 is whole.
        var matches = DocumentSearch.FindAll("aaa aa", "aa", useRegex: false, caseSensitive: true, wholeWord: true, TestContext.Current.CancellationToken);
        Assert.Equal(4, Assert.Single(matches).Start);
    }

    [Fact]
    public void Whole_word_lets_punctuation_at_the_query_edge_sit_next_to_a_word()
    {
        var matches = DocumentSearch.FindAll("run -v now", "-v", useRegex: false, caseSensitive: true, wholeWord: true, TestContext.Current.CancellationToken);
        Assert.Equal(4, Assert.Single(matches).Start);
        Assert.Empty(DocumentSearch.FindAll("run -verbose", "-v", false, true, wholeWord: true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Whole_word_filters_regex_matches_too()
    {
        var matches = DocumentSearch.FindAll("width id7 idx id", @"id\d?", useRegex: true, caseSensitive: true, wholeWord: true, TestContext.Current.CancellationToken);
        Assert.Equal<(int, int)>([(6, 3), (14, 2)], matches.Select(m => (m.Start, m.Length)));
    }
}
