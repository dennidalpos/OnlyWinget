namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetTableParser
{
    private static readonly Dictionary<string, string> HeaderTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Name", "Name" },
        { "Nome", "Name" },
        { "Nom", "Name" },
        { "Nombre", "Name" },

        { "Id", "Id" },
        { "ID.", "Id" },

        { "Version", "Version" },
        { "Versione", "Version" },
        { "Versión", "Version" },

        { "Available", "Available" },
        { "Disponibile", "Available" },
        { "Disponible", "Available" },
        { "Verfügbar", "Available" },

        { "Source", "Source" },
        { "Origine", "Source" },
        { "Quelle", "Source" },
        { "Origen", "Source" },

        { "Match", "Match" },
        { "Corrispondenza", "Match" },
        { "Treffer", "Match" },

        { "Argument", "Argument" },
        { "Argomento", "Argument" },

        { "Explicit", "Explicit" },
        { "Specificato", "Explicit" }
    };

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var lines = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        var separatorIndex = FindSeparatorIndex(lines);
        if (separatorIndex <= 0)
        {
            return [];
        }

        var header = lines[separatorIndex - 1];
        var starts = GetColumnStarts(header).ToArray();
        if (starts.Length == 0)
        {
            return [];
        }

        var headers = starts
            .Select((start, index) => SliceColumn(header, starts, index).Trim())
            .ToArray();

        return lines
            .Skip(separatorIndex + 1)
            .Select(line => ParseRow(line, starts, headers))
            .Where(row => row.Count > 0)
            .ToArray();
    }

    private static int FindSeparatorIndex(IReadOnlyList<string> lines)
    {
        for (var index = 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Count(character => character == '-') < 3 ||
                !line.All(character => character == '-' || char.IsWhiteSpace(character)))
            {
                continue;
            }

            var header = lines[index - 1];
            if (GetColumnStarts(header).Skip(1).Any())
            {
                return index;
            }
        }

        return -1;
    }

    private static IEnumerable<int> GetColumnStarts(string header)
    {
        var yielded = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(header))
        {
            yielded.Add(0);
            yield return 0;
        }

        for (var index = 1; index < header.Length; index++)
        {
            var isFixedWidthStart = index >= 2 &&
                !char.IsWhiteSpace(header[index]) &&
                char.IsWhiteSpace(header[index - 1]) &&
                char.IsWhiteSpace(header[index - 2]);
            var isCompactKnownStart = IsCompactKnownHeaderStart(header, index);

            if ((isFixedWidthStart || isCompactKnownStart) && yielded.Add(index))
            {
                yield return index;
            }
        }
    }

    private static bool IsCompactKnownHeaderStart(string header, int index)
    {
        if (index == 0 || !char.IsWhiteSpace(header[index - 1]))
        {
            return false;
        }

        foreach (var key in HeaderTranslations.Keys)
        {
            if (StartsWithToken(header, index, key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithToken(string text, int index, string token)
    {
        if (text.Length - index < token.Length ||
            !text.AsSpan(index, token.Length).Equals(token, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var end = index + token.Length;
        return text.Length == end || char.IsWhiteSpace(text[end]);
    }

    private static Dictionary<string, string> ParseRow(string line, int[] starts, string[] headers)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < starts.Length && index < headers.Length; index++)
        {
            var rawHeader = headers[index];
            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                continue;
            }

            var value = SliceColumn(line, starts, index).Trim();
            var normalizedHeader = HeaderTranslations.TryGetValue(rawHeader, out var translated)
                ? translated
                : rawHeader;

            row[normalizedHeader] = value;
        }

        return row;
    }

    private static string SliceColumn(string line, int[] starts, int index)
    {
        var start = starts[index];
        if (start >= line.Length)
        {
            return string.Empty;
        }

        var end = index + 1 < starts.Length ? Math.Min(starts[index + 1], line.Length) : line.Length;
        return line[start..end];
    }
}
