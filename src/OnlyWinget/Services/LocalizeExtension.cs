using Microsoft.UI.Xaml.Markup;

namespace OnlyWinget.Services;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue()
    {
        return TextResources.Get(Key);
    }
}
