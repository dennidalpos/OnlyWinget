using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetCommandBuilder
{
    public IReadOnlyList<string> Build(PackageSelection selection, bool bypassHashValidation = false)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var verb = selection.Action switch
        {
            PackageAction.Install => "install",
            PackageAction.Uninstall => "uninstall",
            PackageAction.Upgrade => "upgrade",
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };

        ValidateInput(selection.Package.Id, nameof(selection.Package.Id));

        var arguments = new List<string>
        {
            verb,
            "--id",
            selection.Package.Id,
            "--exact",
            "--accept-source-agreements",
            "--disable-interactivity"
        };

        if (selection.Action is PackageAction.Install or PackageAction.Upgrade)
        {
            arguments.Add("--accept-package-agreements");
            if (bypassHashValidation)
            {
                arguments.Add("--ignore-security-hash");
            }
        }

        if (!string.IsNullOrWhiteSpace(selection.Package.Source))
        {
            ValidateInput(selection.Package.Source, nameof(selection.Package.Source));
            arguments.Add("--source");
            arguments.Add(selection.Package.Source);
        }

        return arguments;
    }

    public static void ValidateInput(string input, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input, paramName);
        if (input.Any(c => char.IsControl(c) || c is '"' or '\'' or '`' or ';' or '|' or '&' or '<' or '>'))
        {
            throw new ArgumentException($"Invalid characters in argument: {paramName}", paramName);
        }
    }
}

