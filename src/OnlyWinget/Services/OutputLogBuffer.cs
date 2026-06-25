// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;

namespace OnlyWinget.Services;

public sealed class OutputLogBuffer
{
    public const int DefaultMaxLineCount = 1000;

    private readonly Queue<string> _lines = new();

    public OutputLogBuffer(int maxLineCount = DefaultMaxLineCount)
    {
        if (maxLineCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLineCount), "Maximum line count must be greater than zero.");
        }

        MaxLineCount = maxLineCount;
    }

    public int MaxLineCount { get; }

    public int Count => _lines.Count;

    public void AppendLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var line in SplitOutputLines(text))
        {
            _lines.Enqueue(line);
            while (_lines.Count > MaxLineCount)
            {
                _lines.Dequeue();
            }
        }
    }

    public void Clear()
    {
        _lines.Clear();
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine, _lines);
    }

    private static IEnumerable<string> SplitOutputLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }
}
