using OnlyWinget.Application.App;

namespace OnlyWinget.Presentation;

public abstract class FeatureViewModel : ObservableObject, IDisposable
{
    private readonly Action<Action> dispatch;
    private bool isDisposed;

    protected FeatureViewModel(Action<Action> dispatch, OnlyWingetApplication? workflow = null)
    {
        this.dispatch = dispatch;
        Workflow = workflow ?? App.Workflow;
    }

    internal OnlyWingetApplication Workflow { get; }
    protected Action<Action> Dispatch => dispatch;

    public void Activate()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        Workflow.StateChanged -= OnStateChanged;
        Workflow.StateChanged += OnStateChanged;
        Refresh();
    }

    public void Deactivate()
    {
        Workflow.StateChanged -= OnStateChanged;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        Deactivate();
        GC.SuppressFinalize(this);
    }

    protected abstract void Refresh();

    private void OnStateChanged(object? sender, EventArgs args) => dispatch(Refresh);
}
