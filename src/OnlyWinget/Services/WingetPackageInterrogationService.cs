// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class WingetPackageInterrogationService : IWingetPackageInterrogationService
{
    private const int DefaultManifestMaxBytes = 1024 * 1024;
    private const int DefaultManifestMaxAttempts = 3;
    private const int StructuredLogValueMaxLength = 500;

    private static readonly TimeSpan DefaultManifestFetchTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultManifestRetryBaseDelay = TimeSpan.FromMilliseconds(250);

    private static readonly Regex FoundPattern = new(
        @"^(Found|Trovato)\s+(?<name>.+?)\s+\[(?<id>[^\]]+)\]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PackageHeaderPattern = new(
        @"^(?<prefix>\S+)\s+(?<name>.+?)\s+\[(?<id>[^\]]+)\]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex KeyValuePattern = new(
        @"^(?<key>[^:]+):\s*(?<value>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly WingetService _wingetService;
    private readonly HttpClient _httpClient;
    private readonly Func<string> _architectureProvider;
    private readonly Func<CultureInfo> _cultureProvider;
    private readonly OperatingSystemInfo _operatingSystemInfo;
    private readonly TimeSpan _manifestFetchTimeout;
    private readonly int _manifestMaxBytes;
    private readonly int _manifestMaxAttempts;
    private readonly TimeSpan _manifestRetryBaseDelay;
    private readonly object _manifestCacheLock = new();
    private readonly Dictionary<string, string> _manifestCache = new(StringComparer.Ordinal);

    public WingetPackageInterrogationService(
        WingetService wingetService,
        HttpClient? httpClient = null,
        Func<string>? architectureProvider = null,
        Func<CultureInfo>? cultureProvider = null,
        OperatingSystemInfo? operatingSystemInfo = null,
        TimeSpan? manifestFetchTimeout = null,
        int? manifestMaxBytes = null,
        int? manifestMaxAttempts = null,
        TimeSpan? manifestRetryBaseDelay = null)
    {
        _wingetService = wingetService;
        _httpClient = httpClient ?? new HttpClient();
        _manifestFetchTimeout = ValidatePositive(manifestFetchTimeout ?? DefaultManifestFetchTimeout, nameof(manifestFetchTimeout));
        _manifestMaxBytes = ValidatePositive(manifestMaxBytes ?? DefaultManifestMaxBytes, nameof(manifestMaxBytes));
        _manifestMaxAttempts = ValidatePositive(manifestMaxAttempts ?? DefaultManifestMaxAttempts, nameof(manifestMaxAttempts));
        _manifestRetryBaseDelay = ValidateNonNegative(manifestRetryBaseDelay ?? DefaultManifestRetryBaseDelay, nameof(manifestRetryBaseDelay));
        _operatingSystemInfo = operatingSystemInfo ?? new OperatingSystemInfoService(
            osArchitectureProvider: () => RuntimeInformation.OSArchitecture,
            processArchitectureProvider: () => RuntimeInformation.ProcessArchitecture,
            cultureProvider: cultureProvider).Detect();
        _architectureProvider = architectureProvider ?? (() => _operatingSystemInfo.NormalizedArchitecture);
        _cultureProvider = cultureProvider ?? (() => GetCultureFromOperatingSystemInfo(_operatingSystemInfo));
    }

    public async Task<PackageInterrogationResult> InterrogateAsync(PackageInterrogationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log(request, $"event=package_interrogation_started id={Quote(request.PackageId)} source={Quote(request.Source)} version={Quote(request.Version)}");

        var commandResult = await InvokeShowWithVersionFallbackAsync(request, cancellationToken).ConfigureAwait(false);
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
        cancellationToken.ThrowIfCancellationRequested();
        var installedDetails = _wingetService.TryLoadInstalledPackageDetails(showMetadata.Id, showMetadata.Source, cancellationToken);
        var defaultSelection = new SelectedInstallOptions
        {
            LogPath = _wingetService.CreateOperationLogPath("install", showMetadata.Id),
            InstallMode = InstallModes.SilentWithProgress,
            Scope = installedDetails.Scope,
            Architecture = string.IsNullOrWhiteSpace(installedDetails.Architecture) ? _architectureProvider() : installedDetails.Architecture,
            Locale = string.IsNullOrWhiteSpace(installedDetails.Locale) ? _cultureProvider().Name : installedDetails.Locale,
            InstallerType = installedDetails.InstallerType
        };

        if (string.Equals(showMetadata.Source, AppEntry.DefaultSource, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(showMetadata.Version))
        {
            if (!TryBuildManifestUrl(showMetadata.Id, showMetadata.Version, out var manifestUrl))
            {
                warnings.Add("Installer manifest lookup skipped because package metadata contains unsupported characters.");
                Log(request, $"event=manifest_url_rejected id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} version={Quote(showMetadata.Version)}");
            }
            else
            {
                try
                {
                    var manifestContent = await TryFetchManifestAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(manifestContent))
                    {
                        warnings.Add("Installer manifest not available.");
                        Log(request, $"event=manifest_fetch_failed id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} url={Quote(manifestUrl)}");
                    }
                    else
                    {
                        InstallerManifest manifest;
                        try
                        {
                            manifest = ParseInstallerManifest(manifestContent);
                        }
                        catch (ManifestParseException ex)
                        {
                            warnings.Add("Installer manifest contains unsupported YAML. Reduced mode enabled.");
                            Log(request, $"event=manifest_parse_failed id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} reason={Quote(ex.Message)}");
                            manifest = new InstallerManifest();
                        }

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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    warnings.Add("Installer manifest could not be retrieved. Reduced mode enabled.");
                    Log(request, $"event=manifest_fetch_failed id={Quote(showMetadata.Id)} source={Quote(showMetadata.Source)} url={Quote(manifestUrl)} message={Quote(ex.Message)}");
                }
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
            AvailableScopes = GetAvailableScopes(installerOptions),
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
            AppEntry.NormalizeSource(request.Source),
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

    private async Task<WingetCommandResult> InvokeShowWithVersionFallbackAsync(PackageInterrogationRequest request, CancellationToken cancellationToken)
    {
        var showArgs = BuildShowArguments(request);
        var commandResult = await Task.Run(() => _wingetService.Invoke(showArgs, null, cancellationToken), cancellationToken).ConfigureAwait(false);
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
        return await Task.Run(() => _wingetService.Invoke(fallbackArgs, null, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static bool TryBuildManifestUrl(string packageId, string version, out string url)
    {
        url = string.Empty;
        var normalizedPackageId = packageId.Trim();
        var normalizedVersion = version.Trim();
        var segments = normalizedPackageId.Split('.', StringSplitOptions.TrimEntries);
        if (segments.Length == 0
            || segments.Any(segment => !IsSafeManifestPathSegment(segment))
            || !IsSafeManifestPathSegment(normalizedVersion))
        {
            return false;
        }

        var first = char.ToLowerInvariant(segments[0][0]).ToString(CultureInfo.InvariantCulture);
        var path = string.Join("/", segments.Select(Uri.EscapeDataString));
        url = "https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/"
            + $"{first}/{path}/{Uri.EscapeDataString(normalizedVersion)}/{Uri.EscapeDataString(normalizedPackageId)}.installer.yaml";
        return true;
    }

    private static bool IsSafeManifestPathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value != "."
            && value != ".."
            && value.All(static character =>
                char.IsLetterOrDigit(character)
                || character is '.' or '-' or '_' or '+');
    }

    private async Task<string?> TryFetchManifestAsync(string url, CancellationToken cancellationToken)
    {
        lock (_manifestCacheLock)
        {
            if (_manifestCache.TryGetValue(url, out var cachedManifest))
            {
                return cachedManifest;
            }
        }

        for (var attempt = 1; attempt <= _manifestMaxAttempts; attempt++)
        {
            using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCancellation.CancelAfter(_manifestFetchTimeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCancellation.Token)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < _manifestMaxAttempts && IsTransientManifestStatus(response.StatusCode))
                    {
                        await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return null;
                }

                var manifestContent = await ReadManifestContentAsync(response.Content, attemptCancellation.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(manifestContent))
                {
                    lock (_manifestCacheLock)
                    {
                        _manifestCache[url] = manifestContent;
                    }
                }

                return manifestContent;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (attempt < _manifestMaxAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Manifest fetch timed out.");
            }
            catch (Exception ex) when (IsTransientManifestException(ex) && attempt < _manifestMaxAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private async Task<string> ReadManifestContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength.Value > _manifestMaxBytes)
        {
            throw new InvalidOperationException("Manifest response exceeded the configured maximum size.");
        }

        var expectedLength = content.Headers.ContentLength is > 0
            ? (int)Math.Min(content.Headers.ContentLength.Value, _manifestMaxBytes)
            : 0;
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = expectedLength > 0 ? new MemoryStream(expectedLength) : new MemoryStream();
        var chunk = new byte[8192];
        var totalBytes = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > _manifestMaxBytes)
            {
                throw new InvalidOperationException("Manifest response exceeded the configured maximum size.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static bool IsTransientManifestStatus(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || numericStatusCode >= 500;
    }

    private static bool IsTransientManifestException(Exception exception)
    {
        return exception is HttpRequestException or IOException;
    }

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(_manifestRetryBaseDelay.TotalMilliseconds * attempt);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static int ValidatePositive(int value, string parameterName)
    {
        return value > 0 ? value : throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
    }

    private static TimeSpan ValidatePositive(TimeSpan value, string parameterName)
    {
        return value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
    }

    private static TimeSpan ValidateNonNegative(TimeSpan value, string parameterName)
    {
        return value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
    }

    private static ShowMetadata ParseShowMetadata(string output, PackageInterrogationRequest request)
    {
        var lines = NormalizeOutput(output)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .Where(line => !IsProgressLine(line))
            .ToList();

        var foundMatch = lines
            .Select(TryMatchPackageHeader)
            .FirstOrDefault(match => match.Success) ?? Match.Empty;
        if (!foundMatch.Success)
        {
            if (lines.Any(IsAmbiguousOutput))
            {
                return ShowMetadata.Failure("Package could not be resolved uniquely.");
            }

            return ShowMetadata.Failure("Unable to resolve package metadata from winget show.");
        }

        var name = foundMatch.Groups["name"].Value.Trim();
        var id = foundMatch.Groups["id"].Value.Trim();
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
            Source = AppEntry.NormalizeSource(request.Source),
            InstallerType = installerType
        };
    }

    private static Match TryMatchPackageHeader(string line)
    {
        var foundMatch = FoundPattern.Match(line);
        if (foundMatch.Success)
        {
            return foundMatch;
        }

        var packageHeaderMatch = PackageHeaderPattern.Match(line);
        if (!packageHeaderMatch.Success)
        {
            return Match.Empty;
        }

        var id = packageHeaderMatch.Groups["id"].Value.Trim();
        return id.Contains('.', StringComparison.Ordinal) && !line.Contains(':', StringComparison.Ordinal)
            ? packageHeaderMatch
            : Match.Empty;
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
            EnsureSupportedYamlSyntax(trimmed);

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
            Scope = string.IsNullOrWhiteSpace(selected.Scope) && IsPortableArchiveOption(selected) ? "user" : selected.Scope,
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

    private static IReadOnlyList<string> GetAvailableScopes(IReadOnlyList<ResolvedInstallerOption> options)
    {
        var scopes = Unique(options.Select(option => option.Scope)).ToList();
        if (options.Any(option => string.IsNullOrWhiteSpace(option.Scope) && IsPortableArchiveOption(option)))
        {
            AddScopeIfMissing(scopes, "user");
            AddScopeIfMissing(scopes, "machine");
        }

        return scopes;
    }

    private static void AddScopeIfMissing(List<string> scopes, string scope)
    {
        if (!scopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
        {
            scopes.Add(scope);
        }
    }

    private static bool IsPortableArchiveOption(ResolvedInstallerOption option)
    {
        return string.Equals(option.InstallerType, "zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.InstallerType, "portable", StringComparison.OrdinalIgnoreCase);
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
        if (trimmedLine.StartsWith("- &", StringComparison.Ordinal) || trimmedLine.StartsWith("- *", StringComparison.Ordinal))
        {
            throw new ManifestParseException("YAML anchors and aliases are not supported.");
        }

        return trimmedLine.StartsWith("- ", StringComparison.Ordinal)
            && TrySplitKeyValue(trimmedLine[2..], out _, out _);
    }

    private static void AddYamlSequenceValues(ICollection<string> target, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (IsUnsupportedYamlScalar(value))
        {
            throw new ManifestParseException("Complex YAML scalar values are not supported.");
        }

        if (value[0] != '[')
        {
            throw new ManifestParseException("YAML sequence must use block list items or a flow sequence.");
        }

        if (value[^1] != ']')
        {
            throw new ManifestParseException("YAML flow sequence is not closed.");
        }

        foreach (var item in SplitYamlFlowSequence(value[1..^1]))
        {
            target.Add(NormalizeYamlScalar(item));
        }
    }

    private static IEnumerable<string> SplitYamlFlowSequence(string value)
    {
        var item = new StringBuilder();
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
            else if (current == ',' && !inSingleQuote && !inDoubleQuote)
            {
                if (item.ToString().Trim().Length > 0)
                {
                    yield return item.ToString().Trim();
                }

                item.Clear();
                continue;
            }

            item.Append(current);
        }

        if (item.ToString().Trim().Length > 0)
        {
            yield return item.ToString().Trim();
        }
    }

    private static string NormalizeYamlScalar(string value)
    {
        var trimmed = StripInlineComment(value).Trim();
        if (IsUnsupportedYamlScalar(trimmed))
        {
            throw new ManifestParseException("Complex YAML scalar values are not supported.");
        }

        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("''", "'", StringComparison.Ordinal);
    }

    private static void EnsureSupportedYamlSyntax(string trimmedLine)
    {
        if (trimmedLine == "---" || trimmedLine == "...")
        {
            return;
        }

        var keyValueLine = trimmedLine.StartsWith("- ", StringComparison.Ordinal) ? trimmedLine[2..] : trimmedLine;
        if (TrySplitKeyValue(keyValueLine, out var key, out var value)
            && (key == "<<" || value.StartsWith('&') || value.StartsWith('*')))
        {
            throw new ManifestParseException("YAML anchors, aliases, and merge keys are not supported.");
        }
    }

    private static bool IsUnsupportedYamlScalar(string value)
    {
        var trimmed = value.Trim();
        return trimmed is "|" or ">" or "|-" or ">-" or "|+" or ">+"
            || trimmed.StartsWith('&')
            || trimmed.StartsWith('*');
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
        var normalized = SanitizeStructuredLogValue(value);
        return $"\"{normalized}\"";
    }

    private static string SanitizeStructuredLogValue(string? value)
    {
        var source = value ?? string.Empty;
        var builder = new StringBuilder(Math.Min(source.Length, StructuredLogValueMaxLength));
        foreach (var character in source)
        {
            if (builder.Length >= StructuredLogValueMaxLength)
            {
                break;
            }

            builder.Append(character switch
            {
                '"' => '\'',
                _ when char.IsControl(character) => ' ',
                _ => character
            });
        }

        return builder.ToString().Trim();
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

    private sealed class ManifestParseException : Exception
    {
        public ManifestParseException(string message)
            : base(message)
        {
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
