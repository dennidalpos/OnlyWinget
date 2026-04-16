// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Windows;

namespace OnlyWinget.Dialogs;

public partial class PackageInterrogationDialog : Window
{
    public PackageInterrogationDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
