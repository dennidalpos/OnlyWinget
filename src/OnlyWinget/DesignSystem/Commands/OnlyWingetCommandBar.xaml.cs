using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;

namespace OnlyWinget.DesignSystem.Commands;

public sealed class UiCommandInvokedEventArgs(UiCommand command) : EventArgs
{
    public UiCommand Command { get; } = command;
}

public sealed partial class OnlyWingetCommandBar : UserControl
{
    public event EventHandler<UiCommandInvokedEventArgs>? CommandInvoked;

    public OnlyWingetCommandBar() => InitializeComponent();

    public void SetCommands(IEnumerable<UiCommand> commands)
    {
        Bar.PrimaryCommands.Clear();
        Bar.SecondaryCommands.Clear();
        foreach (var command in commands.Where(command => command.IsVisible))
        {
            var button = new AppBarButton
            {
                Label = TextResources.Get(command.LabelResourceKey),
                IsEnabled = command.IsEnabled,
                Tag = command,
                Icon = CreateIcon(command.Icon)
            };
            AutomationProperties.SetName(button, button.Label);
            ToolTipService.SetToolTip(button, TextResources.Get(command.TooltipResourceKey ?? command.LabelResourceKey));
            button.Click += OnClick;
            if (command.Placement == UiCommandPlacement.Overflow)
            {
                Bar.SecondaryCommands.Add(button);
            }
            else
            {
                Bar.PrimaryCommands.Add(button);
            }
        }
    }

    private static IconElement? CreateIcon(string? icon) => Enum.TryParse<Symbol>(icon, out var symbol) ? new SymbolIcon(symbol) : null;

    private void OnClick(object sender, RoutedEventArgs args)
    {
        if (sender is AppBarButton { Tag: UiCommand command })
        {
            CommandInvoked?.Invoke(this, new UiCommandInvokedEventArgs(command));
        }
    }
}
