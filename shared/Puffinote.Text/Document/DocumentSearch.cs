using System.Text.RegularExpressions;

namespace Puffinote.Text.Document;

/// <summary>A single search hit: a source range <c>[Start, Start+Length)</c>.</summary>
public readonly record struct SearchMatch(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Find (and the offsets for replace) over the document text — plain or <b>regex</b> via the .NET
/// BCL <see cref="Regex"/> (PRD §7.2). Pure and dependency-free so it is unit-testable; the find
/// bar and the editor's match highlighting both consume these ranges. An invalid regex yields no
/// matches rather than throwing.
/// </summary>
public static class DocumentSearch
{
    /// <summary>
    /// Every match of <paramref name="query"/> in <paramref name="text"/>, in order and non-overlapping.
    /// <paramref name="cancellationToken"/> is checked between matches, so a search on a background thread
    /// that a newer query has replaced stops early with <see cref="OperationCanceledException"/>.
    /// </summary>
    public static IReadOnlyList<SearchMatch> FindAll(string text, string query, bool useRegex, bool caseSensitive,
        CancellationToken cancellationToken = default) =>
        FindAll(text, query, useRegex, caseSensitive, wholeWord: false, cancellationToken);

    /// <summary>
    /// As the other overload; with <paramref name="wholeWord"/> a match counts only when it doesn't continue a
    /// word on either side (see <see cref="IsWholeWord"/>).
    /// </summary>
    public static IReadOnlyList<SearchMatch> FindAll(string text, string query, bool useRegex, bool caseSensitive, bool wholeWord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(query) || text.Length == 0)
        {
            return [];
        }

        return useRegex
            ? FindRegex(text, query, caseSensitive, wholeWord, cancellationToken)
            : FindPlain(text, query, caseSensitive, wholeWord, cancellationToken);
    }

    /// <summary>
    /// True when the range <c>[start, end)</c> of <paramref name="text"/> doesn't run into a word on either side:
    /// at each edge, either the text ends there or one of the two characters meeting there isn't a word character
    /// (a letter, digit or underscore). So "cat" matches in "a cat." but not in "concat", and a query that itself
    /// starts or ends with punctuation, such as "-v", still matches next to a word.
    /// </summary>
    public static bool IsWholeWord(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        bool startOk = start <= 0 || start >= text.Length || !IsWordChar(text[start - 1]) || !IsWordChar(text[start]);
        bool endOk = end >= text.Length || end <= 0 || !IsWordChar(text[end]) || !IsWordChar(text[end - 1]);
        return startOk && endOk;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>True when <paramref name="query"/> is a syntactically valid regex.</summary>
    public static bool IsValidRegex(string query)
    {
        try
        {
            _ = new Regex(query);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static List<SearchMatch> FindPlain(string text, string query, bool caseSensitive, bool wholeWord, CancellationToken cancellationToken)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var results = new List<SearchMatch>();
        var i = 0;
        while (i <= text.Length - query.Length)
        {
            var idx = text.IndexOf(query, i, comparison);
            if (idx < 0)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (wholeWord && !IsWholeWord(text, idx, idx + query.Length))
            {
                i = idx + 1; // part of a longer word; a whole-word hit may still start inside it
                continue;
            }

            results.Add(new SearchMatch(idx, query.Length));
            i = idx + query.Length; // non-overlapping
        }

        return results;
    }

    private static List<SearchMatch> FindRegex(string text, string query, bool caseSensitive, bool wholeWord, CancellationToken cancellationToken)
    {
        var results = new List<SearchMatch>();
        Regex regex;
        try
        {
            regex = new Regex(query, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return results; // invalid pattern -> no matches
        }

        foreach (Match m in regex.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Skip zero-width matches to avoid cycling on the same spot.
            if (m.Length > 0 && (!wholeWord || IsWholeWord(text, m.Index, m.Index + m.Length)))
            {
                results.Add(new SearchMatch(m.Index, m.Length));
            }
        }

        return results;
    }
}
