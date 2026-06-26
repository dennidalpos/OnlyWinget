using OnlyWinget.Application.App;
using OnlyWinget.Infrastructure.Storage;
using OnlyWinget.Infrastructure.Winget;
using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Microsoft.UI.Xaml.Window? window;

    public static OnlyWingetApplication Workflow { get; } = CreateWorkflow();

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

    private static OnlyWingetApplication CreateWorkflow()
    {
        var runner = new ProcessWingetCommandRunner();
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        return new OnlyWingetApplication(
            new JsonWorkspaceStore(JsonWorkspaceStore.DefaultFilePath),
            new CommandAvailability(runner),
            new WingetPackageSearchService(runner, parser, classifier),
            new WingetPackageResolver(runner, classifier),
            new WingetUpdateLoader(runner, parser, classifier),
            new PowerShellWindowsUpdateService(runner),
            new WingetSourceService(runner, parser, classifier),
            new WingetOperationExecutor(runner, new WingetCommandBuilder(), classifier));
    }
}
