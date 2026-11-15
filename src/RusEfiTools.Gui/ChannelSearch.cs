using System.Text;

namespace RusEfiTools.Gui;

internal static class ChannelSearch
{
    public static int? Score(string name, string query)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return 0;
        }

        string needle = Normalize(query);
        if (needle.Length == 0)
        {
            return name.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0 : null;
        }
        string candidate = Normalize(name);
        if (candidate == needle)
        {
            return 0;
        }
        int contiguous = candidate.IndexOf(needle, StringComparison.Ordinal);
        if (contiguous >= 0)
        {
            return 10 + contiguous;
        }

        // Abbreviations match characters in order; tighter matches rank first.
        int next = 0;
        int first = -1;
        int last = -1;
        for (int index = 0; index < candidate.Length && next < needle.Length; index++)
        {
            if (candidate[index] != needle[next])
            {
                continue;
            }
            if (first < 0)
            {
                first = index;
            }
            last = index;
            next++;
        }
        return next == needle.Length ? 1000 + last - first + 1 - needle.Length : null;
    }

    private static string Normalize(string text)
    {
        var normalized = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToUpperInvariant(character));
            }
        }
        return normalized.ToString();
    }
}
