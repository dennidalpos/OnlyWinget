using CommunityToolkit.Mvvm.Messaging;
using OnlyWinget.Application.App;

namespace OnlyWinget.Presentation;

public abstract class FeatureViewModel(OnlyWingetApplication workflow, Action<Action> dispatch) : ObservableObject, IDisposable
{
    private readonly Action<Action> dispatch = dispatch;
    private bool isDisposed;

    internal OnlyWingetApplication Workflow { get; } = workflow;

    public void Activate()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        WeakReferenceMessenger.Default.Unregister<StateChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<FeatureViewModel, StateChangedMessage>(this, (r, _) => r.dispatch(r.Refresh));
        Workflow.StateChanged -= OnStateChanged;
        Workflow.StateChanged += OnStateChanged;
        Refresh();
    }

    public void Deactivate()
    {
        WeakReferenceMessenger.Default.Unregister<StateChangedMessage>(this);
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
