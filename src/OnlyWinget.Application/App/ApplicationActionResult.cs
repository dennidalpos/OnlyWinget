namespace OnlyWinget.Application.App;

public sealed record ApplicationActionResult(bool Succeeded, string? Error)
{
    public static ApplicationActionResult Success { get; } = new(true, null);

    public static ApplicationActionResult Failure(string error) => new(false, error);
}
