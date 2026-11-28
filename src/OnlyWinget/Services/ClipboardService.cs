using Windows.ApplicationModel.DataTransfer;

namespace OnlyWinget.Services;

internal sealed class ClipboardService : IClipboardService
{
    public void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
