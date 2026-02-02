using System.Windows;
using OnlyWinget.ViewModels;

namespace OnlyWinget;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        if (!viewModel.IsWingetAvailable)
        {
            MessageBox.Show(viewModel.Strings.WingetNotFoundText, viewModel.Strings.WingetNotFoundTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        viewModel.Initialize();
    }
}
