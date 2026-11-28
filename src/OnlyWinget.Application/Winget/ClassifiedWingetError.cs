namespace OnlyWinget.Application.Winget;

public sealed record ClassifiedWingetError(WingetErrorKind Kind, string Message, int? ExitCode = null)
{
    /// <summary>Exit code formatted as 0xXXXXXXXX, or null when unknown.</summary>
    public string? ExitCodeHex => ExitCode is { } code ? $"0x{unchecked((uint)code):X8}" : null;
}
