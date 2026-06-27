using OnlyWinget.Application.App;

namespace OnlyWinget;

public partial class App : Microsoft.UI.Xaml.Application
{
    private static Microsoft.UI.Xaml.Window? window;

    internal static nint WindowHandle => window is null ? 0 : WinRT.Interop.WindowNative.GetWindowHandle(window);

    public static OnlyWingetApplication Workflow { get; } = AppComposition.CreateWorkflow();

    public App()
    {
        AppDiagnostics.Initialize();
        AppDiagnostics.Register(this);
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            window = new MainWindow();
            window.Activate();
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("OnLaunched", exception);
            throw;
        }
    }

}
