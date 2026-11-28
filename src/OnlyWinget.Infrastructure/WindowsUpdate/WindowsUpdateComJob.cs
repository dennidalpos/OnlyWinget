using System.Runtime.InteropServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OnlyWinget.Tests")]

namespace OnlyWinget.Infrastructure.WindowsUpdate;

// WUA accepts an automation callback whose completion method has DISPID 0.
[ComVisible(true)]
[Guid("F69F254E-BFF4-4B95-9F84-C585F65D589C")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IWindowsUpdateCompletionCallback
{
    [DispId(0)]
    void Invoke(object job, object callbackArgs);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IWindowsUpdateCompletionCallback))]
public sealed class WindowsUpdateCompletionCallback : IWindowsUpdateCompletionCallback
{
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Completion => completion.Task;

    public void Invoke(object job, object callbackArgs) => completion.TrySetResult();
}

internal static class WindowsUpdateComJob
{
    internal static async Task<object> RunAsync(
        Func<object, object> begin,
        Func<object, object> end,
        Action<object> abort,
        Action<object> cleanup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callback = new WindowsUpdateCompletionCallback();
        var job = begin(callback);
        try
        {
            try
            {
                await callback.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                FinishStep(abort, job, "abort");
                throw;
            }

            return end(job);
        }
        finally
        {
            // CleanUp waits until WUA has released the callbacks; never run it inside Invoke.
            try
            {
                FinishStep(cleanup, job, "clean up");
            }
            finally
            {
                GC.KeepAlive(callback);
            }
        }
    }

    private static void FinishStep(Action<object> step, object job, string action)
    {
        try
        {
            step(job);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new WindowsUpdateComCompletionException(
                $"Windows Update Agent could not {action} the native job. Its state is uncertain. Check Windows Update before retrying.", exception);
        }
    }
}

internal sealed class WindowsUpdateComCompletionException(string message, Exception cause)
    : InvalidOperationException(message, cause);
