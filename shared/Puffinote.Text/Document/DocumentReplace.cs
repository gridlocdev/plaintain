using System.Text;
using System.Text.RegularExpressions;

namespace Puffinote.Text.Document;

/// <summary>
/// One replace-all as a single edit: the span from the first match's start to the last match's end, and
/// the text that span becomes. Replacing only that span (not the whole document) keeps the edit, its undo
/// and the lines it redraws as small as the matches allow.
/// </summary>
public readonly record struct ReplaceAllEdit(int Start, int Length, string Text, int Count);

/// <summary>
/// What replace writes in place of a match, over the same matches <see cref="DocumentSearch.FindAll"/>
/// finds, so the strip's count, its highlights and what Replace All changes are one answer. In regex mode
/// the replacement is a .NET substitution pattern (<c>$1</c>, <c>${name}</c>, <c>$$</c>); in plain mode it
/// is written as typed.
/// </summary>
public static class DocumentReplace
{
    /// <summary>
    /// The text that replaces <paramref name="match"/>, or null when the match no longer matches there
    /// (the document changed since it was found).
    /// </summary>
    public static string? ReplacementFor(string text, SearchMatch match, string query, string replacement, bool useRegex, bool caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);
        if (match.End > text.Length || string.IsNullOrEmpty(query))
        {
            return null;
        }

        if (!useRegex)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return match.Length == query.Length && string.Compare(text, match.Start, query, 0, query.Length, comparison) == 0
                ? replacement
                : null;
        }

        var regex = TryBuild(query, caseSensitive);

        // Matched from the match's own start over the whole text, so lookbehinds and anchors see what
        // the search saw.
        var m = regex?.Match(text, match.Start);
        while (m is { Success: true, Length: 0 } && m.Index == match.Start)
        {
            m = m.NextMatch(); // find skips zero-width matches, so a real match may follow one at the same spot
        }

        return m is { Success: true } && m.Index == match.Start && m.Length == match.Length ? m.Result(replacement) : null;
    }

    /// <summary>Every match replaced, as one span edit, or null when there is nothing to replace.</summary>
    public static ReplaceAllEdit? ReplaceAll(string text, string query, string replacement, bool useRegex, bool caseSensitive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);
        if (string.IsNullOrEmpty(query) || text.Length == 0)
        {
            return null;
        }

        var regex = useRegex ? TryBuild(query, caseSensitive) : null;
        if (useRegex && regex is null)
        {
            return null; // an invalid pattern matches nothing, as in find
        }

        // The regex path walks the matches once and substitutes as it goes, skipping zero-width matches
        // exactly as DocumentSearch does; the plain path takes find's own matches.
        var matches = regex is null
            ? DocumentSearch.FindAll(text, query, useRegex: false, caseSensitive, cancellationToken)
                .Select(m => (m.Start, m.End, Text: replacement))
            : regex.Matches(text).Where(m => m.Length > 0).Select(m => (Start: m.Index, End: m.Index + m.Length, Text: m.Result(replacement)));

        var result = new StringBuilder();
        int start = -1, end = -1, count = 0;
        foreach (var (matchStart, matchEnd, substitute) in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (start < 0)
            {
                start = end = matchStart;
            }

            result.Append(text, end, matchStart - end);
            result.Append(substitute);
            end = matchEnd;
            count++;
        }

        return count == 0 ? null : new ReplaceAllEdit(start, end - start, result.ToString(), count);
    }

    private static Regex? TryBuild(string query, bool caseSensitive)
    {
        try
        {
            return new Regex(query, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
