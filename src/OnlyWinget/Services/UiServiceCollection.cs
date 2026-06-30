namespace OnlyWinget.Services;

using OnlyWinget.Shell;

internal sealed record UiServiceCollection(
    IAppSettingsService Settings,
    IConfirmationService Confirmation,
    IFilePickerService FilePicker,
    IClipboardService Clipboard,
    INavigationRegistry Navigation);
