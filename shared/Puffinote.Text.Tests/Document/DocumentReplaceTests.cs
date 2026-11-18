using Puffinote.Text.Document;
using Xunit;

namespace Puffinote.Text.Tests.Document;

public class DocumentReplaceTests
{
    [Fact]
    public void Replace_all_edits_only_the_span_between_the_first_and_last_match()
    {
        var edit = DocumentReplace.ReplaceAll("keep cat dog cat keep", "cat", "bird", useRegex: false, caseSensitive: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(new ReplaceAllEdit(5, 11, "bird dog bird", 2), edit);
    }

    [Fact]
    public void Replace_all_without_matches_is_null()
    {
        Assert.Null(DocumentReplace.ReplaceAll("abc", "x", "y", useRegex: false, caseSensitive: true, TestContext.Current.CancellationToken));
        Assert.Null(DocumentReplace.ReplaceAll("abc", "(", "y", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Replace_all_follows_the_case_option()
    {
        var edit = DocumentReplace.ReplaceAll("Cat cat", "cat", "x", useRegex: false, caseSensitive: false, TestContext.Current.CancellationToken);
        Assert.Equal("x x", edit?.Text);

        edit = DocumentReplace.ReplaceAll("Cat cat", "cat", "x", useRegex: false, caseSensitive: true, TestContext.Current.CancellationToken);
        Assert.Equal(new ReplaceAllEdit(4, 3, "x", 1), edit);
    }

    [Fact]
    public void Regex_replacements_expand_groups()
    {
        var edit = DocumentReplace.ReplaceAll("a=1, b=2", @"(\w)=(\d)", "$2:$1", useRegex: true, caseSensitive: true,
            TestContext.Current.CancellationToken);

        Assert.Equal("1:a, 2:b", edit?.Text);
        Assert.Equal(2, edit?.Count);
    }

    [Fact]
    public void Regex_replace_all_skips_zero_width_matches_as_find_does()
    {
        const string text = "a1b22";
        var edit = DocumentReplace.ReplaceAll(text, @"\d*", "#", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken);

        Assert.Equal(DocumentSearch.FindAll(text, @"\d*", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken).Count, edit?.Count);
        Assert.Equal("#b#", edit?.Text);
    }

    [Fact]
    public void Plain_replacement_is_written_as_typed_even_with_dollar_signs()
    {
        var edit = DocumentReplace.ReplaceAll("price", "price", "$1", useRegex: false, caseSensitive: true, TestContext.Current.CancellationToken);
        Assert.Equal("$1", edit?.Text);
    }

    [Fact]
    public void One_replacement_expands_groups_with_lookbehind_context()
    {
        const string text = "x=1 y=2";
        var match = DocumentSearch.FindAll(text, @"(?<=y=)\d", useRegex: true, caseSensitive: true, TestContext.Current.CancellationToken)[0];

        Assert.Equal("[2]", DocumentReplace.ReplacementFor(text, match, @"(?<=y=)(\d)", "[$1]", useRegex: true, caseSensitive: true));
    }

    [Fact]
    public void One_replacement_is_null_when_the_text_moved_under_the_match()
    {
        var stale = new SearchMatch(0, 3);
        Assert.Null(DocumentReplace.ReplacementFor("dog", stale, "cat", "x", useRegex: false, caseSensitive: true));
        Assert.Null(DocumentReplace.ReplacementFor("dog", stale, "c.t", "x", useRegex: true, caseSensitive: true));
        Assert.Null(DocumentReplace.ReplacementFor("ca", stale, "cat", "x", useRegex: false, caseSensitive: true));
    }

    [Fact]
    public void Whole_word_replace_all_leaves_longer_words_alone()
    {
        var edit = DocumentReplace.ReplaceAll("cat concat cat", "cat", "dog", useRegex: false, caseSensitive: true, wholeWord: true,
            TestContext.Current.CancellationToken);
        Assert.Equal(new ReplaceAllEdit(0, 14, "dog concat dog", 2), edit);

        var regex = DocumentReplace.ReplaceAll("i in i", "i", "x", useRegex: true, caseSensitive: true, wholeWord: true,
            TestContext.Current.CancellationToken);
        Assert.Equal(new ReplaceAllEdit(0, 6, "x in x", 2), regex);
    }

    [Fact]
    public void Whole_word_replacement_is_null_for_a_match_inside_a_word()
    {
        var inside = new SearchMatch(3, 3); // "cat" in "concat"
        Assert.Null(DocumentReplace.ReplacementFor("concat", inside, "cat", "x", useRegex: false, caseSensitive: true, wholeWord: true));
        Assert.Equal("x", DocumentReplace.ReplacementFor("concat", inside, "cat", "x", useRegex: false, caseSensitive: true));
    }
}
