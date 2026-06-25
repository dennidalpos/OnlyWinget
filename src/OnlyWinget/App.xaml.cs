using OnlyWinget.Application.App;
using OnlyWinget.Infrastructure.Storage;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Microsoft.UI.Xaml.Window? window;

    public static OnlyWingetApplication Workflow { get; } = CreateWorkflow();

    public static event EventHandler? WorkflowChanged;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();
    }

    public static void NotifyWorkflowChanged() => WorkflowChanged?.Invoke(null, EventArgs.Empty);

    private static OnlyWingetApplication CreateWorkflow()
    {
        var runner = new ProcessWingetCommandRunner();
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        return new OnlyWingetApplication(
            new JsonWorkspaceStore(JsonWorkspaceStore.DefaultFilePath),
            new WingetPackageSearchService(runner, parser),
            new WingetPackageResolver(runner, classifier),
            new WingetUpdateLoader(runner, parser),
            new WingetOperationExecutor(runner, new WingetCommandBuilder(), classifier));
    }
}
