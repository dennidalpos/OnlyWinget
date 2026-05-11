// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class WingetPackageInterrogationService : IWingetPackageInterrogationService
{
    private static readonly Regex FoundPattern = new(
        @"^(Found|Trovato)\s+(?<name>.+?)\s+\[(?<id>[^\]]+)\]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex KeyValuePattern = new(
        @"^(?<key>[^:]+):\s*(?<value>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly WingetService _wingetService;
    private readonly HttpClient _httpClient;
    private readonly Func<string> _architectureProvider;
    private readonly Func<CultureInfo> _cultureProvider;
    private readonly OperatingSystemInfo _operatingSystemInfo;

    public WingetPackageInterrogationService(
        WingetService wingetService,
        HttpClient? httpClient = null,
        Func<string>? architectureProvider = null,
        Func<CultureInfo>? cultureProvider = null,
        OperatingSystemInfo? operatingSystemInfo = null)
    {
        _wingetService = wingetService;
        _httpClient = httpClient ?? new HttpClient();
        _operatingSystemInfo = operatingSystemInfo ?? new OperatingSystemInfoService(
            osArchitectureProvider: () => RuntimeInformation.OSArchitecture,
            processArchitectureProvider: () => RuntimeInformation.ProcessArchitecture,
            cultureProvider: cultureProvider).Detect();
        _architectureProvider = architectureProvider ?? (() => _operatingSystemInfo.NormalizedArchitecture);
        _cultureProvider = cultureProvider ?? (() => GetCultureFromOperatingSystemInfo(_operatingSystemInfo));
    }

    public async Task<PackageInterrogationResult> InterrogateAsync(PackageInterrogationRequest request)
    {
        Log(request, $"event=package_interrogation_started id={Quote(request.PackageId)} source={Quote(request.Source)} version={Quote(request.Version)}");

        var commandResult = await InvokeShowWithVersionFallbackAsync(request).ConfigureAwait(false);
        if (commandResult.ExitCode != 0)
        {
            var failure = ExtractFailureMessage(commandResult.Output, _wingetService.GetErrorMessage(commandResult.ExitCode));
            Log(request, $"event=package_resolution_failed id={Quote(request.PackageId)} source={Quote(request.Source)} exit_code={commandResult.ExitCode} message={Quote(failure)}");
            return new PackageInterrogationResult
            {
                Success = false,
                ErrorMessage = failure,
                Id = request.PackageId,
                Name = request.PackageName,
                Source = request.Source
            };
        }

        var showMetadata = ParseShowMetadata(commandResult.Output, request);
        if (!showMetadata.Success)
        {
            Log(request, $"event=package_resolution_failed id={Quote(request.PackageId)} source={Quote(request.Source)} message={Quote(showMetadata.ErrorMessage)}");
            return new PackageInterrogationResult
            {
                Success = false,
                ErrorMessage = showMetadata.ErrorMessage,
                Id = request.PackageId,
                Name = request.PackageName,
                Source = request.Source
            };
        }

        Log(request, $"event=package_resolution_succeeded id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} version={Quote(showMetadata.Version)} installer_type={Quote(showMetadata.InstallerType)}");

        var warnings = new List<string>();
        var installerOptions = Array.Empty<ResolvedInstallerOption>();
        var manifestFingerprint = string.Empty;
        var isReducedMode = true;
        var installedDetails = _wingetService.TryLoadInstalledPackageDetails(showMetadata.Id, showMetadata.Source);
        var defaultSelection = new SelectedInstallOptions
        {
            LogPath = _wingetService.CreateOperationLogPath("install", showMetadata.Id),
            InstallMode = InstallModes.SilentWithProgress,
            Scope = installedDetails.Scope,
            Architecture = string.IsNullOrWhiteSpace(installedDetails.Architecture) ? _architectureProvider() : installedDetails.Architecture,
            Locale = string.IsNullOrWhiteSpace(installedDetails.Locale) ? _cultureProvider().Name : installedDetails.Locale,
            InstallerType = installedDetails.InstallerType
        };

        if (string.Equals(showMetadata.Source, "winget", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(showMetadata.Version))
        {
            var manifestUrl = BuildManifestUrl(showMetadata.Id, showMetadata.Version);
            try
            {
                var manifestContent = await TryFetchManifestAsync(manifestUrl).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(manifestContent))
                {
                    warnings.Add("Installer manifest not available.");
                    Log(request, $"event=manifest_fetch_failed id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} url={Quote(manifestUrl)}");
                }
                else
                {
                    var manifest = ParseInstallerManifest(manifestContent);
                    manifestFingerprint = ComputeFingerprint(manifestContent);
                    installerOptions = NormalizeInstallerOptions(manifest, _architectureProvider(), _cultureProvider(), installedDetails).ToArray();

                    if (installerOptions.Length == 0)
                    {
                        warnings.Add("Installer manifest did not expose selectable installer candidates.");
                        Log(request, $"event=manifest_fallback_mode id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} reason=no_candidates");
                    }
                    else
                    {
                        isReducedMode = false;
                        if (HasMultipleSelectableInstallerCandidates(installerOptions))
                        {
                            warnings.Add("Multiple installer candidates available. Review the selections before confirming.");
                        }

                        defaultSelection = CreateDefaultSelection(showMetadata.Id, installerOptions);
                        Log(request, $"event=manifest_fetch_succeeded id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} url={Quote(manifestUrl)} fingerprint={Quote(manifestFingerprint)} candidates={installerOptions.Length}");
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Installer manifest could not be retrieved. Reduced mode enabled.");
                Log(request, $"event=manifest_fetch_failed id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} url={Quote(manifestUrl)} message={Quote(ex.Message)}");
            }
        }

        if (isReducedMode)
        {
            Log(request, $"event=manifest_fallback_mode id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)}");
        }

        return new PackageInterrogationResult
        {
            Success = true,
            IsReducedMode = isReducedMode,
            Name = string.IsNullOrWhiteSpace(showMetadata.Name) ? request.PackageName : showMetadata.Name,
            Id = showMetadata.Id,
            Version = showMetadata.Version,
            Source = showMetadata.Source,
            InstallerType = showMetadata.InstallerType,
            ManifestFingerprint = manifestFingerprint,
            InterrogatedAtUtc = DateTime.UtcNow,
            Warnings = warnings,
            InstallerOptions = installerOptions,
            AvailableScopes = Unique(installerOptions.Select(option => option.Scope)),
            AvailableArchitectures = Unique(installerOptions.Select(option => option.Architecture)),
            AvailableLocales = Unique(installerOptions.Select(option => option.Locale)),
            AvailableInstallerTypes = Unique(installerOptions.Select(option => option.InstallerType)),
            AvailableInstallModes = Unique(GetAvailableInstallModes(installerOptions)),
            DefaultSelection = defaultSelection
        };
    }

    private static IReadOnlyList<string> BuildShowArguments(PackageInterrogationRequest request)
    {
        var args = new List<string>
        {
            "show",
            "--id",
            request.PackageId,
            "-e",
            "--source",
            string.IsNullOrWhiteSpace(request.Source) ? "winget" : request.Source,
            "--accept-source-agreements",
            "--disable-interactivity"
        };

        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            args.Add("--version");
            args.Add(request.Version.Trim());
        }

        return args;
    }

    private async Task<WingetCommandResult> InvokeShowWithVersionFallbackAsync(PackageInterrogationRequest request)
    {
        var showArgs = BuildShowArguments(request);
        var commandResult = await Task.Run(() => _wingetService.Invoke(showArgs)).ConfigureAwait(false);
        if (commandResult.ExitCode == 0
            || string.IsNullOrWhiteSpace(request.Version)
            || !IsVersionResolutionFailure(commandResult.Output))
        {
            return commandResult;
        }

        var fallbackRequest = new PackageInterrogationRequest
        {
            PackageId = request.PackageId,
            PackageName = request.PackageName,
            Source = request.Source,
            Log = request.Log
        };
        var fallbackArgs = BuildShowArguments(fallbackRequest);
        Log(request, $"event=package_resolution_version_fallback id={Quote(request.PackageId)} source={Quote(request.Source)} version={Quote(request.Version)}");
        return await Task.Run(() => _wingetService.Invoke(fallbackArgs)).ConfigureAwait(false);
    }

    private static string BuildManifestUrl(string packageId, string version)
    {
        var segments = packageId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = segments.Length == 0 ? "x" : char.ToLowerInvariant(segments[0][0]).ToString(CultureInfo.InvariantCulture);
        var path = string.Join("/", segments);
        return $"https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/{first}/{path}/{version}/{packageId}.installer.yaml";
    }

    private async Task<string?> TryFetchManifestAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static ShowMetadata ParseShowMetadata(string output, PackageInterrogationRequest request)
    {
        var lines = NormalizeOutput(output)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .Where(line => !IsProgressLine(line))
            .ToList();

        var foundLine = lines.FirstOrDefault(line => FoundPattern.IsMatch(line));
        if (foundLine == null)
        {
            if (lines.Any(IsAmbiguousOutput))
            {
                return ShowMetadata.Failure("Package could not be resolved uniquely.");
            }

            return ShowMetadata.Failure("Unable to resolve package metadata from winget show.");
        }

        var match = FoundPattern.Match(foundLine);
        var name = match.Groups["name"].Value.Trim();
        var id = match.Groups["id"].Value.Trim();
        var version = request.Version;
        var installerType = string.Empty;

        foreach (var line in lines)
        {
            var keyValue = KeyValuePattern.Match(line.Trim());
            if (!keyValue.Success)
            {
                continue;
            }

            var key = keyValue.Groups["key"].Value.Trim();
            var value = keyValue.Groups["value"].Value.Trim();

            if (MatchesAny(key, "Version", "Versione"))
            {
                version = value;
            }
            else if (MatchesAny(key, "Installer Type", "Tipo di programma di installazione"))
            {
                installerType = value;
            }
        }

        return new ShowMetadata
        {
            Success = true,
            Name = string.IsNullOrWhiteSpace(name) ? request.PackageName : name,
            Id = string.IsNullOrWhiteSpace(id) ? request.PackageId : id,
            Version = version,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "winget" : request.Source,
            InstallerType = installerType
        };
    }

    private static InstallerManifest ParseInstallerManifest(string content)
    {
        var manifest = new InstallerManifest();
        InstallerManifestEntry? currentEntry = null;
        var section = ManifestSection.None;
        var entrySection = ManifestSection.None;

        foreach (var rawLine in content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = rawLine.Length - rawLine.TrimStart(' ').Length;
            if (section == ManifestSection.Installers && IsInstallerEntryStart(trimmed))
            {
                currentEntry = new InstallerManifestEntry();
                manifest.Installers.Add(currentEntry);
                entrySection = ManifestSection.None;
                ParseInstallerKeyValue(trimmed[2..], currentEntry);
                continue;
            }

            if (section == ManifestSection.RootInstallModes && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                manifest.InstallModes.Add(NormalizeYamlScalar(trimmed[2..]));
                continue;
            }

            if (section == ManifestSection.RootUnsupportedArguments && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                manifest.UnsupportedArguments.Add(NormalizeYamlScalar(trimmed[2..]));
                continue;
            }

            if (entrySection == ManifestSection.EntryInstallModes && currentEntry != null && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                currentEntry.InstallModes.Add(NormalizeYamlScalar(trimmed[2..]));
                continue;
            }

            if (entrySection == ManifestSection.EntryUnsupportedArguments && currentEntry != null && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                currentEntry.UnsupportedArguments.Add(NormalizeYamlScalar(trimmed[2..]));
                continue;
            }

            if (indent == 0)
            {
                entrySection = ManifestSection.None;
                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    currentEntry = new InstallerManifestEntry();
                    manifest.Installers.Add(currentEntry);
                    section = ManifestSection.Installers;
                    ParseInstallerKeyValue(trimmed[2..], currentEntry);
                    continue;
                }

                if (TrySplitKeyValue(trimmed, out var key, out var value))
                {
                    switch (key)
                    {
                        case "InstallerType":
                            manifest.InstallerType = value;
                            section = ManifestSection.None;
                            break;
                        case "Scope":
                            manifest.Scope = value;
                            section = ManifestSection.None;
                            break;
                        case "InstallerLocale":
                            manifest.InstallerLocale = value;
                            section = ManifestSection.None;
                            break;
                        case "NestedInstallerType":
                            manifest.NestedInstallerType = value;
                            section = ManifestSection.None;
                            break;
                        case "ElevationRequirement":
                            manifest.ElevationRequirement = value;
                            section = ManifestSection.None;
                            break;
                        case "InstallModes":
                            AddYamlSequenceValues(manifest.InstallModes, value);
                            section = ManifestSection.RootInstallModes;
                            break;
                        case "InstallerSwitches":
                            section = ManifestSection.RootInstallerSwitches;
                            break;
                        case "UnsupportedArguments":
                            AddYamlSequenceValues(manifest.UnsupportedArguments, value);
                            section = ManifestSection.RootUnsupportedArguments;
                            break;
                        case "Installers":
                            section = ManifestSection.Installers;
                            break;
                        default:
                            section = ManifestSection.None;
                            break;
                    }
                }

                continue;
            }

            if (section == ManifestSection.RootInstallerSwitches && indent >= 2 && TrySplitKeyValue(trimmed, out var switchKey, out var switchValue))
            {
                ApplySwitch(manifest.Switches, switchKey, switchValue);
                continue;
            }

            if (currentEntry == null)
            {
                continue;
            }

            if (indent >= 2 && TrySplitKeyValue(trimmed, out var entryKey, out var entryValue) && entryKey == "InstallModes")
            {
                AddYamlSequenceValues(currentEntry.InstallModes, entryValue);
                entrySection = ManifestSection.EntryInstallModes;
                continue;
            }

            if (indent >= 2 && trimmed.StartsWith("InstallerSwitches:", StringComparison.Ordinal))
            {
                entrySection = ManifestSection.EntryInstallerSwitches;
                continue;
            }

            if (indent >= 2 && TrySplitKeyValue(trimmed, out entryKey, out entryValue) && entryKey == "UnsupportedArguments")
            {
                AddYamlSequenceValues(currentEntry.UnsupportedArguments, entryValue);
                entrySection = ManifestSection.EntryUnsupportedArguments;
                continue;
            }

            if (entrySection == ManifestSection.EntryInstallerSwitches && indent >= 4 && TrySplitKeyValue(trimmed, out switchKey, out switchValue))
            {
                ApplySwitch(currentEntry.Switches, switchKey, switchValue);
                continue;
            }

            if (indent >= 2 && TrySplitKeyValue(trimmed, out _, out _))
            {
                ParseInstallerKeyValue(trimmed, currentEntry);
                entrySection = ManifestSection.None;
            }
        }

        return manifest;
    }

    private static IEnumerable<ResolvedInstallerOption> NormalizeInstallerOptions(
        InstallerManifest manifest,
        string currentArchitecture,
        CultureInfo currentCulture,
        WingetPackageDetails installedDetails)
    {
        var candidates = new List<(ResolvedInstallerOption Option, int Score, int Index)>();
        for (var index = 0; index < manifest.Installers.Count; index++)
        {
            var installer = manifest.Installers[index];
            var switches = InstallerSwitches.Merge(manifest.Switches, installer.Switches);
            var installModes = installer.InstallModes.Count > 0 ? installer.InstallModes : manifest.InstallModes;
            var architecture = FirstNonEmpty(installer.Architecture);
            var scope = FirstNonEmpty(installer.Scope, manifest.Scope);
            var locale = FirstNonEmpty(installer.InstallerLocale, manifest.InstallerLocale);
            var installerType = FirstNonEmpty(installer.InstallerType, installer.NestedInstallerType, manifest.NestedInstallerType, manifest.InstallerType);
            var installerTypeLower = installerType.ToLowerInvariant();
            var typeSupportsSilent = installerTypeLower is "inno" or "nullsoft" or "msi" or "burn" or "wix";
            var typeSupportsSilentWithProgress = installerTypeLower is "inno" or "msi" or "burn" or "wix";
            var supportsSilent = installModes.Contains(InstallModes.Silent, StringComparer.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(switches.Silent) || typeSupportsSilent;
            var supportsSilentWithProgress = installModes.Contains(InstallModes.SilentWithProgress, StringComparer.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(switches.SilentWithProgress) || typeSupportsSilentWithProgress;
            var elevationRequirement = FirstNonEmpty(installer.ElevationRequirement, manifest.ElevationRequirement);
            // Entry-level unsupported arguments override root-level ones
            var unsupportedArgs = installer.UnsupportedArguments.Count > 0
                ? (IReadOnlyList<string>)installer.UnsupportedArguments
                : manifest.UnsupportedArguments;
            var option = new ResolvedInstallerOption
            {
                Architecture = architecture,
                Scope = scope,
                Locale = locale,
                InstallerType = installerType,
                SupportsSilent = supportsSilent,
                SupportsSilentWithProgress = supportsSilentWithProgress,
                SupportsInteractive = true,
                ElevationRequirement = elevationRequirement,
                UnsupportedArguments = unsupportedArgs,
                DisplayLabel = BuildDisplayLabel(architecture, scope, locale, installerType)
            };

            var score = ScoreInstalledValue(scope, installedDetails.Scope) * 10000
                + ScoreArchitecture(architecture, FirstNonEmpty(installedDetails.Architecture, currentArchitecture)) * 1000
                + ScoreLocale(locale, FirstNonEmpty(installedDetails.Locale, currentCulture.Name), currentCulture) * 100
                + ScoreInstalledValue(installerType, installedDetails.InstallerType) * 10
                + index;
            candidates.Add((option, score, index));
        }

        return candidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Option);
    }

    private SelectedInstallOptions CreateDefaultSelection(string packageId, IReadOnlyList<ResolvedInstallerOption> installerOptions)
    {
        var selected = installerOptions[0];
        return new SelectedInstallOptions
        {
            Scope = selected.Scope,
            Architecture = selected.Architecture,
            Locale = selected.Locale,
            InstallerType = selected.InstallerType,
            InstallMode = selected.SupportsSilentWithProgress
                ? InstallModes.SilentWithProgress
                : selected.SupportsSilent
                    ? InstallModes.Silent
                    : InstallModes.Interactive,
            LogPath = _wingetService.CreateOperationLogPath("install", packageId),
            ElevationRequirement = selected.ElevationRequirement
        };
    }

    private static IReadOnlyList<string> GetAvailableInstallModes(IEnumerable<ResolvedInstallerOption> options)
    {
        var results = new List<string>();
        if (options.Any())
        {
            results.Add(InstallModes.Interactive);
        }

        if (options.Any(option => option.SupportsSilent))
        {
            results.Add(InstallModes.Silent);
        }

        if (options.Any(option => option.SupportsSilentWithProgress))
        {
            results.Add(InstallModes.SilentWithProgress);
        }

        return results;
    }

    private static bool HasMultipleSelectableInstallerCandidates(IReadOnlyList<ResolvedInstallerOption> installerOptions)
    {
        return installerOptions
            .Select(option => new
            {
                Architecture = NormalizeSelectionValue(option.Architecture),
                Scope = NormalizeSelectionValue(option.Scope),
                Locale = NormalizeSelectionValue(option.Locale),
                InstallerType = NormalizeSelectionValue(option.InstallerType)
            })
            .Distinct()
            .Skip(1)
            .Any();
    }

    private static string NormalizeSelectionValue(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static void ParseInstallerKeyValue(string line, InstallerManifestEntry entry)
    {
        if (!TrySplitKeyValue(line.Trim(), out var key, out var value))
        {
            return;
        }

        switch (key)
        {
            case "Architecture":
                entry.Architecture = value;
                break;
            case "Scope":
                entry.Scope = value;
                break;
            case "InstallerLocale":
                entry.InstallerLocale = value;
                break;
            case "InstallerType":
                entry.InstallerType = value;
                break;
            case "NestedInstallerType":
                entry.NestedInstallerType = value;
                break;
            case "ElevationRequirement":
                entry.ElevationRequirement = value;
                break;
        }
    }

    private static void ApplySwitch(InstallerSwitches target, string key, string value)
    {
        switch (key)
        {
            case "Silent":
                target.Silent = value;
                break;
            case "SilentWithProgress":
                target.SilentWithProgress = value;
                break;
        }
    }

    private static bool TrySplitKeyValue(string line, out string key, out string value)
    {
        var normalizedLine = StripInlineComment(line).Trim();
        var parts = normalizedLine.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            key = parts[0];
            value = NormalizeYamlScalar(parts[1]);
            return true;
        }

        key = string.Empty;
        value = string.Empty;
        return false;
    }

    private static bool IsInstallerEntryStart(string trimmedLine)
    {
        return trimmedLine.StartsWith("- ", StringComparison.Ordinal)
            && TrySplitKeyValue(trimmedLine[2..], out _, out _);
    }

    private static void AddYamlSequenceValues(ICollection<string> target, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '[' || value[^1] != ']')
        {
            return;
        }

        foreach (var item in value[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            target.Add(NormalizeYamlScalar(item));
        }
    }

    private static string NormalizeYamlScalar(string value)
    {
        var trimmed = StripInlineComment(value).Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("''", "'", StringComparison.Ordinal);
    }

    private static string StripInlineComment(string value)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (current == '"' && !inSingleQuote && (index == 0 || value[index - 1] != '\\'))
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (current == '#' && !inSingleQuote && !inDoubleQuote && (index == 0 || char.IsWhiteSpace(value[index - 1])))
            {
                return value[..index];
            }
        }

        return value;
    }

    private static string NormalizeOutput(string output)
    {
        var noAnsi = Regex.Replace(output ?? string.Empty, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty);
        return noAnsi.Replace('\b', ' ');
    }

    private static bool IsProgressLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0
            && (trimmed.All(character => character is '-' or '/' or '\\' or '|' or '.' or '█' or '▒' or '■')
                || Regex.IsMatch(trimmed, @"^\d{1,3}%$", RegexOptions.CultureInvariant));
    }

    private static bool IsAmbiguousOutput(string line)
    {
        return line.Contains("Multiple packages found", StringComparison.OrdinalIgnoreCase)
            || line.Contains("piu pacchetti", StringComparison.OrdinalIgnoreCase)
            || line.Contains("più pacchetti", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractFailureMessage(string output, string fallback)
    {
        var relevant = NormalizeOutput(output)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !IsProgressLine(line) && !string.IsNullOrWhiteSpace(line));
        return string.IsNullOrWhiteSpace(relevant) ? fallback : relevant;
    }

    private static bool IsVersionResolutionFailure(string output)
    {
        var normalized = NormalizeOutput(output);
        return normalized.Contains("No version found matching", StringComparison.OrdinalIgnoreCase)
            || (normalized.Contains("versione", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("trovata", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("corrispondente", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreArchitecture(string architecture, string currentArchitecture)
    {
        if (string.IsNullOrWhiteSpace(architecture))
        {
            return 1;
        }

        return string.Equals(architecture, currentArchitecture, StringComparison.OrdinalIgnoreCase) ? 0 : 2;
    }

    private static int ScoreLocale(string locale, string preferredLocale, CultureInfo currentCulture)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(preferredLocale))
        {
            if (string.Equals(locale, preferredLocale, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var preferredLanguage = GetLanguageName(preferredLocale);
            if (!string.IsNullOrWhiteSpace(preferredLanguage)
                && (string.Equals(locale, preferredLanguage, StringComparison.OrdinalIgnoreCase)
                    || locale.StartsWith(preferredLanguage + "-", StringComparison.OrdinalIgnoreCase)))
            {
                return 1;
            }
        }

        if (string.Equals(locale, currentCulture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return string.Equals(locale, currentCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)
            || locale.StartsWith(currentCulture.TwoLetterISOLanguageName + "-", StringComparison.OrdinalIgnoreCase)
            ? 1
            : 3;
    }

    private static int ScoreInstalledValue(string value, string preferredValue)
    {
        if (string.IsNullOrWhiteSpace(preferredValue))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return 1;
        }

        return string.Equals(value, preferredValue, StringComparison.OrdinalIgnoreCase) ? 0 : 2;
    }

    private static string GetLanguageName(string locale)
    {
        var separatorIndex = locale.IndexOf('-', StringComparison.Ordinal);
        return separatorIndex <= 0 ? locale : locale[..separatorIndex];
    }

    private static CultureInfo GetCultureFromOperatingSystemInfo(OperatingSystemInfo operatingSystemInfo)
    {
        if (!string.IsNullOrWhiteSpace(operatingSystemInfo.UiCultureName))
        {
            try
            {
                return CultureInfo.GetCultureInfo(operatingSystemInfo.UiCultureName);
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return CultureInfo.CurrentUICulture;
    }

    private static string BuildDisplayLabel(string architecture, string scope, string locale, string installerType)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(architecture) ? null : architecture,
            string.IsNullOrWhiteSpace(scope) ? null : scope,
            string.IsNullOrWhiteSpace(locale) ? null : locale,
            string.IsNullOrWhiteSpace(installerType) ? null : installerType
        };

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static IReadOnlyList<string> Unique(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return string.Empty;
    }

    private static string ComputeFingerprint(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    private static string Quote(string? value)
    {
        var normalized = (value ?? string.Empty).Replace("\"", "'");
        return $"\"{normalized}\"";
    }

    private static void Log(PackageInterrogationRequest request, string message)
    {
        request.Log?.Invoke(message);
    }

    private sealed class ShowMetadata
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string InstallerType { get; init; } = string.Empty;

        public static ShowMetadata Failure(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    private sealed class InstallerManifest
    {
        public string InstallerType { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string InstallerLocale { get; set; } = string.Empty;
        public string NestedInstallerType { get; set; } = string.Empty;
        public string ElevationRequirement { get; set; } = string.Empty;
        public InstallerSwitches Switches { get; } = new();
        public List<string> InstallModes { get; } = new();
        public List<string> UnsupportedArguments { get; } = new();
        public List<InstallerManifestEntry> Installers { get; } = new();
    }

    private sealed class InstallerManifestEntry
    {
        public string Architecture { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string InstallerLocale { get; set; } = string.Empty;
        public string InstallerType { get; set; } = string.Empty;
        public string NestedInstallerType { get; set; } = string.Empty;
        public string ElevationRequirement { get; set; } = string.Empty;
        public InstallerSwitches Switches { get; } = new();
        public List<string> InstallModes { get; } = new();
        public List<string> UnsupportedArguments { get; } = new();
    }

    private sealed class InstallerSwitches
    {
        public string Silent { get; set; } = string.Empty;
        public string SilentWithProgress { get; set; } = string.Empty;

        public static InstallerSwitches Merge(InstallerSwitches root, InstallerSwitches node)
        {
            return new InstallerSwitches
            {
                Silent = FirstNonEmpty(node.Silent, root.Silent),
                SilentWithProgress = FirstNonEmpty(node.SilentWithProgress, root.SilentWithProgress)
            };
        }
    }

    private enum ManifestSection
    {
        None,
        RootInstallModes,
        RootInstallerSwitches,
        RootUnsupportedArguments,
        Installers,
        EntryInstallModes,
        EntryInstallerSwitches,
        EntryUnsupportedArguments
    }
}
