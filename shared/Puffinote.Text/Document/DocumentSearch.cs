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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(query) || text.Length == 0)
        {
            return [];
        }

        return useRegex
            ? FindRegex(text, query, caseSensitive, cancellationToken)
            : FindPlain(text, query, caseSensitive, cancellationToken);
    }

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

    private static List<SearchMatch> FindPlain(string text, string query, bool caseSensitive, CancellationToken cancellationToken)
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
            results.Add(new SearchMatch(idx, query.Length));
            i = idx + query.Length; // non-overlapping
        }

        return results;
    }

    private static List<SearchMatch> FindRegex(string text, string query, bool caseSensitive, CancellationToken cancellationToken)
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
            if (m.Length > 0) // skip zero-width matches to avoid cycling on the same spot
            {
                results.Add(new SearchMatch(m.Index, m.Length));
            }
        }

        return results;
    }
}
