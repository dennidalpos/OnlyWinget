// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Windows;
using System.Windows.Automation;

namespace OnlyWinget.Dialogs;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string prompt, string defaultValue, string confirmLabel, string cancelLabel)
    {
        InitializeComponent();
        Title = title;
        PromptTitleTextBlock.Text = title;
        PromptMessageTextBlock.Text = prompt;
        InputTextBox.Text = defaultValue;
        ConfirmButton.Content = confirmLabel;
        CancelButton.Content = cancelLabel;
        AutomationProperties.SetName(PromptTitleTextBlock, title);
        AutomationProperties.SetName(InputTextBox, prompt);

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    public string ResponseText => InputTextBox.Text.Trim();

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
