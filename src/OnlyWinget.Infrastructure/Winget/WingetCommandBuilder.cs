using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetCommandBuilder(UpdateScanOptions? scanOptions = null)
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

        if (selection.Action == PackageAction.Upgrade && scanOptions is not null)
        {
            // An explicit upgrade of a package that was listed with these switches needs them again.
            if (scanOptions.IncludeUnknownVersions)
            {
                arguments.Add("--include-unknown");
            }

            if (scanOptions.IncludePinned)
            {
                arguments.Add("--include-pinned");
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

    /// <summary>
    /// Fallback for an upgrade or uninstall that winget lists but cannot match by id: installed entries correlated from Add/Remove
    /// Programs only answer to their display name. Returns null when the selection has no installed name.
    /// </summary>
    public IReadOnlyList<string>? BuildByInstalledName(PackageSelection selection, bool bypassHashValidation = false)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Action is not (PackageAction.Upgrade or PackageAction.Uninstall) || !WingetInputValidator.IsValid(selection.InstalledName))
        {
            return null;
        }

        var arguments = Build(selection, bypassHashValidation).ToList();
        var idIndex = arguments.IndexOf("--id");
        arguments[idIndex] = "--name";
        arguments[idIndex + 1] = selection.InstalledName!;
        return arguments;
    }

    public static void ValidateInput(string input, string paramName) =>
        WingetInputValidator.Validate(input, paramName);
}

