# WinUI 3 MVVM Page Pattern Template

This template demonstrates a clean View and ViewModel separation using `CommunityToolkit.Mvvm` and standard `x:Bind` parameters.

## 1. ViewModel Layout (`SettingsViewModel.cs`)
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnlyWinget.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isDeveloperMode;

    [RelayCommand]
    private async Task ToggleDeveloperModeAsync()
    {
        StatusMessage = "Applying changes...";
        await Task.Delay(1000); // Simulate background work
        StatusMessage = IsDeveloperMode ? "Developer mode active" : "Developer mode disabled";
    }
}
```

## 2. View Interface (`SettingsPage.xaml`)
```xml
<Page
    x:Class="OnlyWinget.Presentation.Views.SettingsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="using:OnlyWinget.Presentation.ViewModels"
    mc:Ignorable="d"
    Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">

    <Grid Padding="24">
        <StackPanel Spacing="16" MaxWidth="400" HorizontalAlignment="Left">
            <TextBlock Text="App Settings" Style="{ThemeResource HeaderTextBlockStyle}" />
            
            <!-- Binding a Boolean Property (Two-Way) -->
            <ToggleSwitch Header="Enable Developer Mode" 
                          IsOn="{x:Bind ViewModel.IsDeveloperMode, Mode=TwoWay}"
                          Command="{x:Bind ViewModel.ToggleDeveloperModeCommand}" />

            <TextBlock Text="{x:Bind ViewModel.StatusMessage, Mode=OneWay}" 
                       Style="{ThemeResource BodyTextBlockStyle}" 
                       FontStyle="Italic" />
        </StackPanel>
    </Grid>
</Page>
```

## 3. View Code-Behind (`SettingsPage.xaml.cs`)
```csharp
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Presentation.Views;

public partial class SettingsPage : Page
{
    // strongly-typed property for binding in XAML
    public ViewModels.SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        this.InitializeComponent();
        this.ViewModel = new ViewModels.SettingsViewModel();
    }
}
```
