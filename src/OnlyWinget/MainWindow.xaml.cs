using System.Windows;
using System.Windows.Controls;
using OnlyWinget.ViewModels;

namespace OnlyWinget;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (sender is not ListView listView)
        {
            return;
        }

        viewModel.SelectedSearchResults.Clear();
        foreach (var item in listView.SelectedItems)
        {
            if (item is Models.SearchResult result)
            {
                viewModel.SelectedSearchResults.Add(result);
            }
        }
    }
}
