using Microsoft.UI;

namespace OnlyWinget.Services;

internal interface IFilePickerService
{
    Task<string?> PickAndReadTextAsync(WindowId windowId, string extension, CancellationToken cancellationToken);

    Task<bool> PickAndWriteTextAsync(
        WindowId windowId,
        string suggestedFileName,
        string extension,
        string fileTypeResourceKey,
        string content,
        CancellationToken cancellationToken);
}
