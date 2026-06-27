using System.Text.Json;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.Presets;

public sealed class PresetDocumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Export(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return JsonSerializer.Serialize(OnlyWingetPresetDocument.Create(preset), JsonOptions);
    }

    public Preset Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Preset document is required.", nameof(json));
        }

        var document = JsonSerializer.Deserialize<OnlyWingetPresetDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Preset document is invalid.");

        if (!string.Equals(document.Format, OnlyWingetPresetDocument.CurrentFormat, StringComparison.Ordinal))
        {
            throw new NotSupportedException("Only onlywinget.preset.v1 documents are supported.");
        }

        return document.Preset;
    }

    public static string GetExportFileName(string presetName)
    {
        var normalized = new string(presetName.Trim()
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)
            .ToArray())
            .Trim('.', ' ');
        return $"{(string.IsNullOrWhiteSpace(normalized) ? "preset" : normalized)}.onlywinget.json";
    }
}
