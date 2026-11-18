namespace Plaintain.Text.Document;

/// <summary>
/// How a find bar reports where it has got to: "3 of 17", "No results", or nothing at all while there is
/// no query to answer.
/// <para>
/// The wording lives here, apart from the find bar, so the words the reader sees have one source and the
/// bar stays a passive strip that raises events at a host.
/// </para>
/// </summary>
public static class SearchStatus
{
    /// <summary>
    /// The label for <paramref name="current"/> (zero-based, -1 for none) out of <paramref name="total"/>
    /// matches of <paramref name="query"/>. Empty for an empty query: a find bar you have just opened has
    /// not failed to find anything, it has not been asked yet, and "No results" would be a lie.
    /// </summary>
    public static string Describe(int current, int total, string? query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }

        return total == 0 ? "No results" : $"{Math.Max(0, current) + 1} of {total}";
    }
}
