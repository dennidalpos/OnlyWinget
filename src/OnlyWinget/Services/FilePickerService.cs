using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace OnlyWinget.Services;

internal sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickAndReadTextAsync(
        WindowId windowId,
        string extension,
        CancellationToken cancellationToken)
    {
        var picker = new FileOpenPicker(windowId)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(extension);

        var result = await picker.PickSingleFileAsync();
        return result is null
            ? null
            : await File.ReadAllTextAsync(result.Path, cancellationToken);
    }

    public async Task<bool> PickAndWriteTextAsync(
        WindowId windowId,
        string suggestedFileName,
        string extension,
        string fileTypeResourceKey,
        string content,
        CancellationToken cancellationToken)
    {
        var picker = new FileSavePicker(windowId)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add(TextResources.Get(fileTypeResourceKey), [extension]);

        var result = await picker.PickSaveFileAsync();
        if (result is null)
        {
            return false;
        }

        await File.WriteAllTextAsync(result.Path, content, cancellationToken);
        return true;
    }
}
