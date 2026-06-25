using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetCommandBuilder
{
    public IReadOnlyList<string> Build(PackageSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var verb = selection.Action switch
        {
            PackageAction.Install => "install",
            PackageAction.Uninstall => "uninstall",
            PackageAction.Upgrade => "upgrade",
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };

        var arguments = new List<string>
        {
            verb,
            "--id",
            selection.Package.Id,
            "--exact",
            "--accept-source-agreements"
        };

        if (selection.Action is PackageAction.Install or PackageAction.Upgrade)
        {
            arguments.Add("--accept-package-agreements");
        }

        if (!string.IsNullOrWhiteSpace(selection.Package.Source))
        {
            arguments.Add("--source");
            arguments.Add(selection.Package.Source);
        }

        return arguments;
    }
}
