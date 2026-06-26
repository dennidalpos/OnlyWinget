using OnlyWinget.Application.App;

namespace OnlyWinget;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Microsoft.UI.Xaml.Window? window;

    public static OnlyWingetApplication Workflow { get; } = AppComposition.CreateWorkflow();

    public static event EventHandler? WorkflowChanged;

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

    public static void NotifyWorkflowChanged() => WorkflowChanged?.Invoke(null, EventArgs.Empty);

}
