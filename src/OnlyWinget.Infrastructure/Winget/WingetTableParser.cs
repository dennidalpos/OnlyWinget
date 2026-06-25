namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetTableParser
{
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

        var separatorIndex = Array.FindIndex(lines, line => line.All(character => character == '-' || char.IsWhiteSpace(character)));
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

    private static IEnumerable<int> GetColumnStarts(string header)
    {
        if (!string.IsNullOrWhiteSpace(header))
        {
            yield return 0;
        }

        for (var index = 2; index < header.Length; index++)
        {
            if (!char.IsWhiteSpace(header[index]) &&
                char.IsWhiteSpace(header[index - 1]) &&
                char.IsWhiteSpace(header[index - 2]))
            {
                yield return index;
            }
        }
    }

    private static Dictionary<string, string> ParseRow(string line, int[] starts, string[] headers)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < starts.Length && index < headers.Length; index++)
        {
            var value = SliceColumn(line, starts, index).Trim();
            if (!string.IsNullOrWhiteSpace(headers[index]))
            {
                row[headers[index]] = value;
            }
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
