$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not ('OnlyWingetUiTestWindowNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class OnlyWingetUiTestWindowNative {
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
'@
}

function Test-UiTestWindowOwnership {
    param(
        [IntPtr]$WindowHandle,
        [IntPtr]$AppWindowHandle,
        [int]$AppProcessId
    )

    [uint32]$windowProcessId = 0
    [OnlyWingetUiTestWindowNative]::GetWindowThreadProcessId($AppWindowHandle, [ref]$windowProcessId) | Out-Null
    if ($AppWindowHandle -eq [IntPtr]::Zero -or $windowProcessId -ne $AppProcessId -or $WindowHandle -eq $AppWindowHandle) {
        return $false
    }

    $visited = [System.Collections.Generic.HashSet[IntPtr]]::new()
    while ($WindowHandle -ne [IntPtr]::Zero -and $visited.Add($WindowHandle)) {
        $WindowHandle = [OnlyWingetUiTestWindowNative]::GetWindow($WindowHandle, 4) # GW_OWNER
        if ($WindowHandle -eq $AppWindowHandle) {
            return $true
        }
    }
    return $false
}

function Close-UiTestOwnedDialog {
    param(
        [IntPtr]$WindowHandle,
        [IntPtr]$AppWindowHandle,
        [int]$AppProcessId
    )

    if (-not (Test-UiTestWindowOwnership -WindowHandle $WindowHandle -AppWindowHandle $AppWindowHandle -AppProcessId $AppProcessId)) {
        return $false
    }
    if (-not [OnlyWingetUiTestWindowNative]::PostMessage($WindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)) {
        throw "Cannot close the owned test dialog: $WindowHandle"
    }
    return $true
}
