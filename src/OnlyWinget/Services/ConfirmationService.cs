using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Services;

internal sealed class ConfirmationService(IAppSettingsService settings) : IConfirmationService
{
    public async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string titleResourceKey, string messageResourceKey)
    {
        if (!settings.Current.ConfirmDestructiveActions)
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            Title = TextResources.Get(titleResourceKey),
            Content = TextResources.Get(messageResourceKey),
            PrimaryButtonText = TextResources.Get("Dialog_Confirm"),
            CloseButtonText = TextResources.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
