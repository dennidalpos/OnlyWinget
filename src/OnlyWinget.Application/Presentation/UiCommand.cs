namespace OnlyWinget.Application.Presentation;

public enum UiCommandId
{
    AddPreset,
    RenamePreset,
    RemovePreset,
    AddPresetPackage,
    EditPresetPackage,
    RemovePresetPackages,
    ImportPreset,
    ExportPreset,
    SaveWorkspace,
    InstallPreset,
    UninstallPreset,
    SearchPackages,
    AddSearchResults,
    RefreshUpdates,
    ApplyUpdates,
    ScanWindowsUpdates,
    InstallWindowsUpdates,
    RefreshSources,
    UpdateSources,
    AddSource,
    RemoveSource,
    ResetSources,
    CancelOperation,
    ClearActivity,
    ExportActivity
}

public enum UiCommandKind
{
    Primary,
    Secondary,
    Destructive,
    Cancel
}

public enum UiCommandPlacement
{
    Primary,
    Overflow
}

public sealed record UiCommand(
    UiCommandId Id,
    string LabelResourceKey,
    bool IsEnabled,
    UiCommandKind Kind = UiCommandKind.Secondary,
    UiCommandPlacement Placement = UiCommandPlacement.Primary,
    bool IsVisible = true,
    string? Icon = null,
    string? TooltipResourceKey = null,
    string? ConfirmationResourceKey = null,
    string? Shortcut = null);
