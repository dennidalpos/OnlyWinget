using Microsoft.UI.Xaml;

namespace OnlyWinget.Services;

internal interface IConfirmationService
{
    Task<bool> ConfirmAsync(XamlRoot xamlRoot, string titleResourceKey, string messageResourceKey);
}
