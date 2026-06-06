// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class PackageOperationService
{
    private readonly WingetService _wingetService;
    private readonly IInstallCommandBuilder _installCommandBuilder;
    private readonly IElevatedWingetLauncher _elevatedLauncher;
    private readonly bool _isCurrentProcessElevated;

    public PackageOperationService(
        WingetService wingetService,
        IInstallCommandBuilder installCommandBuilder,
        IElevatedWingetLauncher? elevatedLauncher = null,
        bool? isCurrentProcessElevated = null)
    {
        _wingetService = wingetService;
        _installCommandBuilder = installCommandBuilder;
        _elevatedLauncher = elevatedLauncher ?? new ElevatedWingetLauncher();
        _isCurrentProcessElevated = isCurrentProcessElevated ?? ProcessElevationService.IsRunningAsAdministrator;
    }

    public async Task<PackageOperationResult> ExecuteAsync(
        PackageOperationRequest request,
        LocalizedStrings strings,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default,
        Action<PackageOperationExecutionMode>? onExecutionStarting = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.Kind switch
        {
            PackageOperationKind.Install => await ExecuteInstallAsync(request, strings, onOutputLine, onExecutionStarting, cancellationToken).ConfigureAwait(false),
            PackageOperationKind.Uninstall => await ExecuteUninstallAsync(request, strings, onOutputLine, onExecutionStarting, cancellationToken).ConfigureAwait(false),
            PackageOperationKind.Upgrade => await ExecuteUpgradeAsync(request, strings, onOutputLine, cancellationToken).ConfigureAwait(false),
            PackageOperationKind.UpdateWinget => await ExecuteUpdateWingetAsync(request, onOutputLine, cancellationToken).ConfigureAwait(false),
            PackageOperationKind.SourceUpdate => await ExecuteSourceUpdateAsync(request, onOutputLine, cancellationToken).ConfigureAwait(false),
            _ => CreateFailure(request, 9999, _wingetService.GetErrorMessage(9999, strings.LocaleCode), _wingetService.GetResolutionHint(9999, strings.LocaleCode))
        };
    }

    private async Task<PackageOperationResult> ExecuteInstallAsync(
        PackageOperationRequest request,
        LocalizedStrings strings,
        Action<string>? onOutputLine,
        Action<PackageOperationExecutionMode>? onExecutionStarting,
        CancellationToken cancellationToken)
    {
        if (request.RequiresAdvancedArgumentsReview)
        {
            return new PackageOperationResult
            {
                Kind = request.Kind,
                Outcome = PackageOperationOutcome.AdvancedArgumentsReviewRequired,
                OperationKey = request.OperationKey,
                Name = request.Name,
                Id = request.Id,
                Source = request.Source,
                Message = strings.AdvancedArgumentsReviewRequiredText,
                Resolution = strings.AdvancedArgumentsReviewRequiredResolution,
                AppendOutput = false,
                DiagnosticEvents = new[] { $"event=advanced_arguments_review_required id=\"{FormatLogValue(request.Id)}\"" }
            };
        }

        var resolution = _wingetService.ResolveSavedPackage(request.Id, request.Name, request.Source, cancellationToken);
        if (!resolution.IsResolved)
        {
            return CreateResolutionFailure(request, resolution, strings);
        }

        var resolvedRequest = request.WithResolvedPackage(resolution);
        var app = resolvedRequest.ToAppEntry();
        var installArgs = _installCommandBuilder.BuildInstallArguments(app);
        var elevationMode = ElevationDecisionService.Decide(_isCurrentProcessElevated, app.Scope, app.ElevationRequirement);
        var executionMode = elevationMode == ElevationMode.ElevatedRequired
            ? PackageOperationExecutionMode.Elevated
            : PackageOperationExecutionMode.Direct;
        var diagnostics = new List<string>
        {
            $"event=install_command_built id=\"{FormatLogValue(app.Id)}\" args=\"{FormatArgumentsForLog(installArgs)}\" elevation_mode={elevationMode} process_elevated={_isCurrentProcessElevated} scope=\"{FormatLogValue(app.Scope)}\""
        };

        if (executionMode == PackageOperationExecutionMode.Elevated)
        {
            diagnostics.Add($"event=elevated_launch_starting id=\"{FormatLogValue(app.Id)}\"");
        }

        var commandResult = await ExecuteWingetCommandAsync(
            installArgs,
            app.SupportsLog ? GetInstallLogPath(app) : null,
            executionMode,
            onOutputLine,
            onExecutionStarting,
            cancellationToken).ConfigureAwait(false);

        var result = ClassifyInstallResult(resolvedRequest, commandResult.Result, strings);
        return CopyExecutionDetails(result, commandResult, diagnostics);
    }

    private async Task<PackageOperationResult> ExecuteUninstallAsync(
        PackageOperationRequest request,
        LocalizedStrings strings,
        Action<string>? onOutputLine,
        Action<PackageOperationExecutionMode>? onExecutionStarting,
        CancellationToken cancellationToken)
    {
        var args = BuildUninstallArguments(request);
        var elevationMode = ElevationDecisionService.Decide(_isCurrentProcessElevated, request.Scope, request.ElevationRequirement);
        var executionMode = elevationMode == ElevationMode.ElevatedRequired
            ? PackageOperationExecutionMode.Elevated
            : PackageOperationExecutionMode.Direct;
        var logPath = GetOperationLogPath("uninstall", request);
        var diagnostics = new List<string>
        {
            $"event=uninstall_command_built id=\"{FormatLogValue(request.Id)}\" args=\"{FormatArgumentsForLog(args)}\" elevation_mode={elevationMode} process_elevated={_isCurrentProcessElevated} source=\"{FormatLogValue(request.Source)}\""
        };

        var commandResult = await ExecuteWingetCommandAsync(args, logPath, executionMode, onOutputLine, onExecutionStarting, cancellationToken).ConfigureAwait(false);
        var result = commandResult.Result.ExitCode == 0
            ? CreateSuccess(request, commandResult.Result)
            : CreateFailure(
                request,
                commandResult.Result.ExitCode,
                _wingetService.GetErrorMessage(commandResult.Result.ExitCode, strings.LocaleCode),
                _wingetService.GetResolutionHint(commandResult.Result.ExitCode, strings.LocaleCode),
                commandResult.Result.Output);

        return CopyExecutionDetails(result, commandResult, diagnostics);
    }

    private async Task<PackageOperationResult> ExecuteUpgradeAsync(
        PackageOperationRequest request,
        LocalizedStrings strings,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        var liveOutputLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commandResult = await Task.Run(() => _wingetService.UpgradeApp(
            request.Id,
            request.Source,
            request.Name,
            request.AvailableVersion,
            request.Scope,
            request.Architecture,
            request.Locale,
            request.InstallerType,
            line =>
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    liveOutputLines.Add(trimmed);
                }

                onOutputLine?.Invoke(line);
            },
            cancellationToken), cancellationToken).ConfigureAwait(false);

        var execution = new CommandExecutionResult(commandResult, PackageOperationExecutionMode.Direct, string.Empty, liveOutputLines.Count > 0);
        if (commandResult.ExitCode == 0)
        {
            var stillAvailableUpdate = await Task.Run(
                () => _wingetService.FindAvailableUpdate(request.Id, request.Source, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (stillAvailableUpdate != null)
            {
                var message = UpdateVerificationFormatter.FormatStillAvailableStatus(strings.LocaleCode);
                var resolution = UpdateVerificationFormatter.FormatStillAvailableResolution(strings.LocaleCode, ToUpdateEntry(request), stillAvailableUpdate);
                return CopyExecutionDetails(
                    new PackageOperationResult
                    {
                        Kind = request.Kind,
                        Outcome = PackageOperationOutcome.StillAvailable,
                        OperationKey = request.OperationKey,
                        Name = request.Name,
                        Id = request.Id,
                        Source = request.Source,
                        ExitCode = commandResult.ExitCode,
                        Output = UpdateVerificationFormatter.FormatStillAvailableLog(ToUpdateEntry(request), stillAvailableUpdate),
                        Message = message,
                        Resolution = resolution
                    },
                    execution,
                    Array.Empty<string>());
            }

            return CopyExecutionDetails(CreateSuccess(request, commandResult), execution, Array.Empty<string>());
        }

        if (_wingetService.IsNoUpgradeNeeded(commandResult.ExitCode))
        {
            var isNoApplicableUpgrade = _wingetService.IsNoApplicableUpgrade(commandResult);
            var hasAdvertisedUpdate = HasAdvertisedUpdate(request);
            var message = isNoApplicableUpgrade
                ? GetNoApplicableUpgradeMessage(strings.LocaleCode)
                : hasAdvertisedUpdate
                    ? GetAdvertisedUpdateNoopMessage(strings.LocaleCode)
                    : _wingetService.GetErrorMessage(commandResult.ExitCode, strings.LocaleCode);
            var resolution = isNoApplicableUpgrade
                ? GetNoApplicableUpgradeResolution(strings.LocaleCode, request)
                : hasAdvertisedUpdate
                    ? GetAdvertisedUpdateNoopResolution(strings.LocaleCode, request)
                    : _wingetService.GetResolutionHint(commandResult.ExitCode, strings.LocaleCode);

            var outputLines = new List<string>();
            if (liveOutputLines.Count > 0)
            {
                outputLines.AddRange(_wingetService.GetRelevantOutputLines(commandResult.Output)
                    .Where(line => !liveOutputLines.Contains(line.Trim())));
            }
            else if (!string.IsNullOrWhiteSpace(commandResult.Output))
            {
                outputLines.Add(commandResult.Output);
            }

            outputLines.Add($"event=winget_upgrade_noop id=\"{FormatLogValue(request.Id)}\" exit_code={commandResult.ExitCode} message=\"{FormatLogValue(message)}\" resolution=\"{FormatLogValue(resolution)}\"");

            return CopyExecutionDetails(
                new PackageOperationResult
                {
                    Kind = request.Kind,
                    Outcome = isNoApplicableUpgrade
                        ? PackageOperationOutcome.NoApplicableUpgrade
                        : hasAdvertisedUpdate
                            ? PackageOperationOutcome.AdvertisedUpdateNotApplied
                            : PackageOperationOutcome.AlreadyUpdated,
                    OperationKey = request.OperationKey,
                    Name = request.Name,
                    Id = request.Id,
                    Source = request.Source,
                    ExitCode = commandResult.ExitCode,
                    Output = string.Join(Environment.NewLine, outputLines.Where(line => !string.IsNullOrWhiteSpace(line))),
                    Message = message,
                    Resolution = resolution
                },
                execution,
                Array.Empty<string>());
        }

        var error = _wingetService.GetErrorMessage(commandResult.ExitCode, strings.LocaleCode);
        var errorResolution = _wingetService.GetResolutionHint(commandResult.ExitCode, strings.LocaleCode);
        return CopyExecutionDetails(CreateFailure(request, commandResult.ExitCode, error, errorResolution, commandResult.Output), execution, Array.Empty<string>());
    }

    private async Task<PackageOperationResult> ExecuteUpdateWingetAsync(
        PackageOperationRequest request,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        var commandResult = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = _wingetService.UpgradeWinget();
            onOutputLine?.Invoke(result.Output);
            return result;
        }, cancellationToken).ConfigureAwait(false);

        return new PackageOperationResult
        {
            Kind = request.Kind,
            Outcome = commandResult.ExitCode == 0 || _wingetService.IsNoUpgradeNeeded(commandResult.ExitCode)
                ? PackageOperationOutcome.Succeeded
                : PackageOperationOutcome.Failed,
            OperationKey = request.OperationKey,
            Name = request.Name,
            Id = request.Id,
            Source = request.Source,
            ExitCode = commandResult.ExitCode,
            Output = commandResult.Output,
            AppendOutput = false
        };
    }

    private async Task<PackageOperationResult> ExecuteSourceUpdateAsync(
        PackageOperationRequest request,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        var commandResult = await Task.Run(() => _wingetService.Invoke(new[] { "source", "update" }, onOutputLine, cancellationToken), cancellationToken).ConfigureAwait(false);
        return new PackageOperationResult
        {
            Kind = request.Kind,
            Outcome = commandResult.ExitCode == 0 ? PackageOperationOutcome.Succeeded : PackageOperationOutcome.Failed,
            OperationKey = request.OperationKey,
            Name = request.Name,
            Id = request.Id,
            Source = request.Source,
            ExitCode = commandResult.ExitCode,
            Output = commandResult.Output
        };
    }

    private async Task<CommandExecutionResult> ExecuteWingetCommandAsync(
        IReadOnlyList<string> args,
        string? logPath,
        PackageOperationExecutionMode executionMode,
        Action<string>? onOutputLine,
        Action<PackageOperationExecutionMode>? onExecutionStarting,
        CancellationToken cancellationToken)
    {
        var receivedLiveOutput = false;
        Action<string>? wrappedOutput = line =>
        {
            receivedLiveOutput = true;
            onOutputLine?.Invoke(line);
        };

        onExecutionStarting?.Invoke(executionMode);
        var result = executionMode == PackageOperationExecutionMode.Elevated
            ? await Task.Run(() => _elevatedLauncher.Launch(args, logPath, wrappedOutput, cancellationToken: cancellationToken), cancellationToken).ConfigureAwait(false)
            : await Task.Run(() => _wingetService.Invoke(args, wrappedOutput, cancellationToken), cancellationToken).ConfigureAwait(false);

        return new CommandExecutionResult(result, executionMode, logPath ?? string.Empty, receivedLiveOutput);
    }

    private PackageOperationResult ClassifyInstallResult(
        PackageOperationRequest request,
        WingetCommandResult commandResult,
        LocalizedStrings strings)
    {
        if (commandResult.ExitCode == 0)
        {
            return CreateSuccess(request, commandResult);
        }

        if (_wingetService.IsNoApplicableInstaller(commandResult))
        {
            return new PackageOperationResult
            {
                Kind = request.Kind,
                Outcome = PackageOperationOutcome.NoApplicableInstaller,
                OperationKey = request.OperationKey,
                Name = request.Name,
                Id = request.Id,
                Source = request.Source,
                ExitCode = commandResult.ExitCode,
                Output = commandResult.Output,
                Message = _wingetService.GetErrorMessage(commandResult.ExitCode, strings.LocaleCode),
                Resolution = GetNoApplicableInstallResolution(strings.LocaleCode, request),
                DiagnosticEvents = new[] { $"event=install_no_applicable_installer_preserved_selectors id=\"{FormatLogValue(request.Id)}\"" }
            };
        }

        if (_wingetService.IsAlreadyInstalled(commandResult))
        {
            return new PackageOperationResult
            {
                Kind = request.Kind,
                Outcome = PackageOperationOutcome.AlreadyInstalled,
                OperationKey = request.OperationKey,
                Name = request.Name,
                Id = request.Id,
                Source = request.Source,
                ExitCode = commandResult.ExitCode,
                Output = commandResult.Output
            };
        }

        if (_wingetService.IsNoUpgradeNeeded(commandResult.ExitCode))
        {
            return new PackageOperationResult
            {
                Kind = request.Kind,
                Outcome = PackageOperationOutcome.AlreadyUpdated,
                OperationKey = request.OperationKey,
                Name = request.Name,
                Id = request.Id,
                Source = request.Source,
                ExitCode = commandResult.ExitCode,
                Output = commandResult.Output
            };
        }

        return CreateFailure(
            request,
            commandResult.ExitCode,
            _wingetService.GetErrorMessage(commandResult.ExitCode, strings.LocaleCode),
            _wingetService.GetResolutionHint(commandResult.ExitCode, strings.LocaleCode),
            commandResult.Output);
    }

    private PackageOperationResult CreateResolutionFailure(
        PackageOperationRequest request,
        SavedPackageResolutionResult resolution,
        LocalizedStrings strings)
    {
        var isAmbiguous = resolution.Status == SavedPackageResolutionStatus.Ambiguous;
        return new PackageOperationResult
        {
            Kind = request.Kind,
            Outcome = isAmbiguous ? PackageOperationOutcome.PackageAmbiguous : PackageOperationOutcome.PackageUnresolved,
            OperationKey = request.OperationKey,
            Name = request.Name,
            Id = request.Id,
            Source = request.Source,
            Message = isAmbiguous ? strings.SavedPackageAmbiguousText : strings.SavedPackageUnresolvedText,
            Resolution = isAmbiguous ? strings.SavedPackageAmbiguousResolution : strings.SavedPackageUnresolvedResolution,
            AppendOutput = false,
            DiagnosticEvents = new[] { $"event=install_blocked_package_resolution id=\"{FormatLogValue(request.Id)}\" source=\"{FormatLogValue(request.Source)}\" status=\"{resolution.Status}\"" }
        };
    }

    private static PackageOperationResult CopyExecutionDetails(
        PackageOperationResult result,
        CommandExecutionResult execution,
        IReadOnlyList<string> diagnostics)
    {
        var combinedDiagnostics = diagnostics.Concat(result.DiagnosticEvents).ToArray();
        return new PackageOperationResult
        {
            Kind = result.Kind,
            Outcome = result.Outcome,
            OperationKey = result.OperationKey,
            Name = result.Name,
            Id = result.Id,
            Source = result.Source,
            ExitCode = result.ExitCode,
            Output = result.Output,
            AppendOutput = execution.ExecutionMode == PackageOperationExecutionMode.Elevated || !execution.ReceivedLiveOutput,
            Message = result.Message,
            Resolution = result.Resolution,
            RedactedCommand = result.RedactedCommand,
            LogPath = execution.LogPath,
            ExecutionMode = execution.ExecutionMode,
            DiagnosticEvents = combinedDiagnostics
        };
    }

    private static PackageOperationResult CreateSuccess(PackageOperationRequest request, WingetCommandResult result)
    {
        return new PackageOperationResult
        {
            Kind = request.Kind,
            Outcome = PackageOperationOutcome.Succeeded,
            OperationKey = request.OperationKey,
            Name = request.Name,
            Id = request.Id,
            Source = request.Source,
            ExitCode = result.ExitCode,
            Output = result.Output
        };
    }

    private static PackageOperationResult CreateFailure(
        PackageOperationRequest request,
        int exitCode,
        string message,
        string resolution,
        string output = "")
    {
        return new PackageOperationResult
        {
            Kind = request.Kind,
            Outcome = PackageOperationOutcome.Failed,
            OperationKey = request.OperationKey,
            Name = request.Name,
            Id = request.Id,
            Source = request.Source,
            ExitCode = exitCode,
            Output = output,
            Message = message,
            Resolution = resolution
        };
    }

    private string GetInstallLogPath(AppEntry app)
    {
        return string.IsNullOrWhiteSpace(app.LogPath)
            ? _wingetService.CreateOperationLogPath("install", app.OperationKey)
            : Environment.ExpandEnvironmentVariables(app.LogPath.Trim());
    }

    private string GetOperationLogPath(string operation, PackageOperationRequest request)
    {
        return string.IsNullOrWhiteSpace(request.LogPath)
            ? _wingetService.CreateOperationLogPath(operation, request.OperationKey)
            : Environment.ExpandEnvironmentVariables(request.LogPath.Trim());
    }

    private IReadOnlyList<string> BuildUninstallArguments(PackageOperationRequest request)
    {
        var args = new List<string>
        {
            "uninstall",
            "--id",
            request.Id,
            "--exact"
        };

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            args.Add("--source");
            args.Add(AppEntry.NormalizeSource(request.Source));
        }

        var logPath = GetOperationLogPath("uninstall", request);
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            args.Add("--log");
            args.Add(logPath);
        }

        args.Add("--accept-source-agreements");
        args.Add("--disable-interactivity");
        return args;
    }

    private static UpdateEntry ToUpdateEntry(PackageOperationRequest request)
    {
        return new UpdateEntry
        {
            Name = request.Name,
            Id = request.Id,
            Version = request.Version,
            Available = request.AvailableVersion,
            Source = request.Source,
            Scope = request.Scope,
            Architecture = request.Architecture,
            Locale = request.Locale,
            InstallerType = request.InstallerType
        };
    }

    private static string FormatArgumentsForLog(IReadOnlyList<string> args)
    {
        var formattedArgs = new List<string>(args.Count);
        var redactNextValue = false;

        foreach (var arg in args)
        {
            if (redactNextValue)
            {
                formattedArgs.Add("[redacted]");
                redactNextValue = false;
                continue;
            }

            formattedArgs.Add(FormatArgumentForLog(arg));
            if (IsSensitiveArgumentOption(arg))
            {
                redactNextValue = true;
            }
        }

        return string.Join(" ", formattedArgs);
    }

    private static bool IsSensitiveArgumentOption(string arg)
    {
        return string.Equals(arg, "--custom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--override", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatArgumentForLog(string arg)
    {
        return arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg;
    }

    private static string FormatLogValue(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string GetNoApplicableUpgradeMessage(string localeCode)
    {
        return UseEnglish(localeCode)
            ? "Upgrade not applicable"
            : "Aggiornamento non applicabile";
    }

    private static string GetNoApplicableUpgradeResolution(string localeCode, PackageOperationRequest request)
    {
        var configuredOptions = FormatConfiguredOptions(request);
        if (!string.IsNullOrWhiteSpace(configuredOptions))
        {
            return UseEnglish(localeCode)
                ? $"winget found a newer version in the source, but no installer applies to the configured package options ({configuredOptions}). Edit the package options to a supported installer, or wait for the package maintainer to publish a matching installer."
                : $"winget ha trovato una versione piu recente nella sorgente, ma nessun installer e compatibile con le opzioni configurate nel pacchetto ({configuredOptions}). Modifica le opzioni del pacchetto scegliendo un installer supportato oppure attendi che il manutentore pubblichi un installer compatibile.";
        }

        return UseEnglish(localeCode)
            ? "winget found a newer version in the source, but its manifest does not apply to this system or its requirements."
            : "winget ha trovato una versione piu recente nella sorgente, ma il manifest non si applica a questo sistema o ai suoi requisiti.";
    }

    private static string GetAdvertisedUpdateNoopMessage(string localeCode)
    {
        return UseEnglish(localeCode)
            ? "Advertised update not applied"
            : "Aggiornamento segnalato non applicato";
    }

    private static string GetAdvertisedUpdateNoopResolution(string localeCode, PackageOperationRequest request)
    {
        var currentVersion = string.IsNullOrWhiteSpace(request.Version) ? "unknown" : request.Version.Trim();
        var availableVersion = string.IsNullOrWhiteSpace(request.AvailableVersion) ? "unknown" : request.AvailableVersion.Trim();
        return UseEnglish(localeCode)
            ? $"winget listed {currentVersion} -> {availableVersion}, but upgrade returned already at the latest version. This usually means the installed major version or installer channel cannot be upgraded in place; review the package options or install the newer channel manually."
            : $"winget ha elencato {currentVersion} -> {availableVersion}, ma upgrade ha risposto gia alla versione piu recente. Di solito significa che la major version o il canale installer installato non puo essere aggiornato in-place; verifica le opzioni del pacchetto o installa manualmente il canale piu recente.";
    }

    private static string GetNoApplicableInstallResolution(string localeCode, PackageOperationRequest request)
    {
        var configuredOptions = FormatConfiguredOptions(request);
        if (!string.IsNullOrWhiteSpace(configuredOptions))
        {
            return UseEnglish(localeCode)
                ? $"winget did not find an installer matching the configured package options ({configuredOptions}). OnlyWinget did not retry without these constraints. Edit the package options to a supported installer, or install the package manually if those constraints are required."
                : $"winget non ha trovato un installer compatibile con le opzioni configurate nel pacchetto ({configuredOptions}). OnlyWinget non ha ritentato senza questi vincoli. Modifica le opzioni del pacchetto scegliendo un installer supportato oppure installa il pacchetto manualmente se quei vincoli sono necessari.";
        }

        return UseEnglish(localeCode)
            ? "winget did not find an installer that applies to this system or its requirements. Edit the package options or install the package manually."
            : "winget non ha trovato un installer applicabile a questo sistema o ai suoi requisiti. Modifica le opzioni del pacchetto oppure installa il pacchetto manualmente.";
    }

    private static bool HasAdvertisedUpdate(PackageOperationRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.AvailableVersion)
            && !IsNoUpdateMarker(request.AvailableVersion)
            && !string.Equals(request.Version?.Trim(), request.AvailableVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoUpdateMarker(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("No update", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("No update available", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Nessun aggiornamento", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Gia alla versione piu recente", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatConfiguredOptions(PackageOperationRequest request)
    {
        var options = new List<string>();
        AddConfiguredOption(options, "scope", request.Scope);
        AddConfiguredOption(options, "architecture", request.Architecture);
        AddConfiguredOption(options, "locale", request.Locale);
        AddConfiguredOption(options, "installer-type", request.InstallerType);
        return string.Join(", ", options);
    }

    private static void AddConfiguredOption(List<string> options, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            options.Add($"{name}={value.Trim()}");
        }
    }

    private static bool UseEnglish(string localeCode)
    {
        return !string.IsNullOrWhiteSpace(localeCode)
            && localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CommandExecutionResult(
        WingetCommandResult Result,
        PackageOperationExecutionMode ExecutionMode,
        string LogPath,
        bool ReceivedLiveOutput);
}
