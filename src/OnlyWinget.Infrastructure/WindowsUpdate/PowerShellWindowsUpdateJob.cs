namespace OnlyWinget.Infrastructure.WindowsUpdate;

internal static class PowerShellWindowsUpdateJob
{
    // The callback runs on COM threads without entering a PowerShell runspace.
    // Keep the source compatible with Windows PowerShell 5.1's C# compiler.
    internal const string Script = """
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Threading;
[ComVisible(true)]
[Guid("F179B75E-0F1B-4A45-BA39-C94E0B3017F6")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IOnlyWingetUpdateCallback {
    [DispId(0)] void Invoke(object job, object args);
}
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IOnlyWingetUpdateCallback))]
public sealed class OnlyWingetUpdateCallback : IOnlyWingetUpdateCallback {
    public readonly ManualResetEvent Completed = new ManualResetEvent(false);
    public void Invoke(object job, object args) { Completed.Set(); }
}
'@
$cancellationEvent = [Threading.EventWaitHandle]::OpenExisting('__CANCELLATION_EVENT__')
function Stop-OnlyWingetUpdateJob {
    param($Job)
    try { $Job.RequestAbort() }
    catch {
        [Console]::Error.WriteLine('##OWU-ABORT-FAILED##' + $_.Exception.Message)
        throw
    }
    throw [OperationCanceledException]::new()
}
function Invoke-OnlyWingetUpdateJob {
    param([scriptblock]$Begin, [scriptblock]$End)
    if ($cancellationEvent.WaitOne(0)) { throw [OperationCanceledException]::new() }
    $callback = New-Object OnlyWingetUpdateCallback
    $progressCallback = New-Object OnlyWingetUpdateCallback
    $job = $null
    try {
        $job = & $Begin $callback $progressCallback
        while (-not $callback.Completed.WaitOne(100)) {
            if ($cancellationEvent.WaitOne(0)) {
                Stop-OnlyWingetUpdateJob $job
            }
        }
        if ($cancellationEvent.WaitOne(0)) {
            Stop-OnlyWingetUpdateJob $job
        }
        & $End $job
    }
    finally {
        try {
            if ($null -ne $job) { $job.CleanUp() }
        }
        catch {
            [Console]::Error.WriteLine('##OWU-CLEANUP-FAILED##' + $_.Exception.Message)
            throw
        }
        finally {
            if ($null -ne $job -and [Runtime.InteropServices.Marshal]::IsComObject($job)) {
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($job)
            }
            $callback.Completed.Dispose()
            $progressCallback.Completed.Dispose()
        }
    }
}
""";
}
