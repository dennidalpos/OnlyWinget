using System.Text.Json;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.Presets;

public sealed class PresetDocumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "OnlyWingetPresetDocument is a known DTO type.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "OnlyWingetPresetDocument is a known DTO type.")]
    public string Export(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return JsonSerializer.Serialize(OnlyWingetPresetDocument.Create(preset), JsonOptions);
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "OnlyWingetPresetDocument is a known DTO type.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "OnlyWingetPresetDocument is a known DTO type.")]
    public Preset Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Preset document is required.", nameof(json));
        }

        OnlyWingetPresetDocument document;
        try
        {
            document = JsonSerializer.Deserialize<OnlyWingetPresetDocument>(json, JsonOptions)
                ?? throw new InvalidOperationException("Preset document is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Preset document is invalid.", exception);
        }

        if (!string.Equals(document.Format, OnlyWingetPresetDocument.CurrentFormat, StringComparison.Ordinal))
        {
            throw new NotSupportedException("Only onlywinget.preset.v1 documents are supported.");
        }

        if (document.Preset is null)
        {
            throw new InvalidOperationException("Preset document does not contain a preset.");
        }

        var duplicate = document.Preset.Packages
            .GroupBy(package => $"{package.Source?.ToUpperInvariant()}|{package.Id.ToUpperInvariant()}", StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Preset contains duplicate package '{duplicate.First().Id}'.");
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
