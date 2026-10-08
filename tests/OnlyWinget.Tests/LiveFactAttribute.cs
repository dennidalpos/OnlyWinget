namespace OnlyWinget.Tests;

public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("ONLYWINGET_RUN_WINGET_SMOKE") != "1")
        {
            Skip = "Live validation requires ONLYWINGET_RUN_WINGET_SMOKE=1.";
        }
    }
}
