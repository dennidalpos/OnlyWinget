$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/UiTestWindowHelpers.ps1')

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class OnlyWingetDialogFixture {
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(uint exStyle, string className, string title,
        uint style, int x, int y, int width, int height, IntPtr owner, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)]
    public struct Message {
        public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam;
        public uint time; public int x; public int y; public uint privateValue;
    }
    [DllImport("user32.dll")]
    public static extern bool PeekMessage(out Message message, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref Message message);
    public static void Pump() {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1)) DispatchMessage(ref message);
    }
}
'@

$handles = [System.Collections.Generic.List[IntPtr]]::new()
try {
    # Real native windows; the unrelated dialog deliberately uses the same process.
    foreach ($ownerIndex in @(-1, -1, 0, 2)) {
        $owner = if ($ownerIndex -lt 0) { [IntPtr]::Zero } else { $handles[$ownerIndex] }
        $handle = [OnlyWingetDialogFixture]::CreateWindowEx(0, 'STATIC', 'Open', 0, 0, 0, 100, 100, $owner, [IntPtr]::Zero, [IntPtr]::Zero, [IntPtr]::Zero)
        if ($handle -eq [IntPtr]::Zero) { throw 'Cannot create a native dialog fixture.' }
        $handles.Add($handle)
    }
    if (Close-UiTestOwnedDialog -WindowHandle $handles[1] -AppWindowHandle $handles[0] -AppProcessId $PID) {
        throw 'Unrelated same-process dialog was accepted.'
    }
    if (Close-UiTestOwnedDialog -WindowHandle $handles[2] -AppWindowHandle $handles[0] -AppProcessId ($PID + 1)) {
        throw 'Wrong app process was accepted.'
    }
    if (Close-UiTestOwnedDialog -WindowHandle $handles[0] -AppWindowHandle $handles[0] -AppProcessId $PID) {
        throw 'Main window was accepted as a dialog.'
    }
    if (-not (Close-UiTestOwnedDialog -WindowHandle $handles[3] -AppWindowHandle $handles[0] -AppProcessId $PID)) {
        throw 'Nested owned dialog was not accepted.'
    }
    [OnlyWingetDialogFixture]::Pump()
    if ([OnlyWingetDialogFixture]::IsWindow($handles[3]) -or
        -not [OnlyWingetDialogFixture]::IsWindow($handles[1]) -or
        -not [OnlyWingetDialogFixture]::IsWindow($handles[0])) {
        throw 'Owned-dialog cancellation did not preserve unrelated/main windows.'
    }
    Write-Host 'PASS: Owned dialog closed; unrelated dialog and main window preserved.' -ForegroundColor Green
}
finally {
    foreach ($handle in $handles) {
        if ([OnlyWingetDialogFixture]::IsWindow($handle)) {
            [OnlyWingetDialogFixture]::DestroyWindow($handle) | Out-Null
        }
    }
}
