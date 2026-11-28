using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is [WindowsUpdateWorkerService.Switch, ..])
        {
            return args is [_, var eventId, var parent] && int.TryParse(parent, out var parentId) && parentId > 0
                ? WindowsUpdateWorkerService.RunWorkerAsync(eventId, parentId).GetAwaiter().GetResult()
                : 1;
        }

        XamlGeneratedProgram.XamlGeneratedMain();
        return 0;
    }
}
