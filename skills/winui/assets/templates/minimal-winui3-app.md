# Minimal Unpackaged WinUI 3 App Template

This asset template provides the minimal layout for a modern, unpackaged WinUI 3 desktop application targeting .NET 10 LTS and Windows App SDK 2.2.0.

## 1. Project Configuration (`MinimalWinUIApp.csproj`)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <Nullable>enable</Nullable>
    <UseWinUI>true</UseWinUI>
    <!-- Configures unpackaged execution mode -->
    <WindowsPackageType>None</WindowsPackageType>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.2.0" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.28000.2270" />
  </ItemGroup>
</Project>
```

## 2. Application Entrypoint (`App.xaml` & `App.xaml.cs`)

### `App.xaml`
```xml
<Application
    x:Class="MinimalWinUIApp.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:MinimalWinUIApp">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
                <!-- Core system design styles are loaded here -->
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### `App.xaml.cs`
```csharp
using Microsoft.UI.Xaml;

namespace MinimalWinUIApp;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
```

## 3. Main Window Layout (`MainWindow.xaml` & `MainWindow.xaml.cs`)

### `MainWindow.xaml`
```xml
<Window
    x:Class="MinimalWinUIApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:MinimalWinUIApp"
    Title="Minimal WinUI 3 App">

    <Grid VerticalAlignment="Center" HorizontalAlignment="Center">
        <StackPanel Spacing="12">
            <TextBlock Text="Hello WinUI 3!" Style="{ThemeResource TitleTextBlockStyle}" HorizontalAlignment="Center"/>
            <Button Content="Click Me" Click="OnButtonClick" HorizontalAlignment="Center"/>
        </StackPanel>
    </Grid>
</Window>
```

### `MainWindow.xaml.cs`
```csharp
using Microsoft.UI.Xaml;

namespace MinimalWinUIApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        // Simple click action
    }
}
```
