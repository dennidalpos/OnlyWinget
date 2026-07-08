using OnlyWinget.Application.App;
using System.Collections.ObjectModel;

namespace OnlyWinget.Presentation;

public abstract class FeatureViewModel(OnlyWingetApplication workflow, Action<Action> dispatch) : ObservableObject, IDisposable
{
    private bool isDisposed;

    internal OnlyWingetApplication Workflow { get; } = workflow;

    public void Activate()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        Workflow.StateChanged -= OnStateChanged;
        Workflow.StateChanged += OnStateChanged;
        Refresh();
    }

    public void Deactivate() => Workflow.StateChanged -= OnStateChanged;

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
