using System.Runtime.Versioning;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget.Tests;

public sealed class WindowsUpdateManagementTests
{
    [Theory]
    [InlineData("ebfc1fc5-71a4-4f7b-9aca-3b9a503104a0", WindowsUpdateClassification.Driver)] // confirmed on a live scan
    [InlineData("EBFC1FC5-71A4-4F7B-9ACA-3B9A503104A0", WindowsUpdateClassification.Driver)]
    [InlineData("e0789628-ce08-4437-be74-2495b842f43b", WindowsUpdateClassification.Definition)]
    [InlineData("0fa1201d-4330-4fa8-8ae9-b877473b6441", WindowsUpdateClassification.Quality)]
    [InlineData("e6cf1350-c01b-414d-a61f-263d14d133b4", WindowsUpdateClassification.Quality)]
    [InlineData("3689bdc8-b205-4af4-8d4a-a63924c5e9d5", WindowsUpdateClassification.Feature)]
    [InlineData("00000000-0000-0000-0000-000000000000", WindowsUpdateClassification.Other)]
    public void CategoryIdsMapToTheWindowsSettingsGroups(string id, WindowsUpdateClassification expected) =>
        Assert.Equal(expected, WindowsUpdateClassifier.FromCategoryIds([id]));

    [Theory]
    [InlineData("Aggiornamento dell'intelligence sulla sicurezza per Microsoft Defender Antivirus -2267602 KB (versione 1.463.338.0) - Canale corrente (Generico)", WindowsUpdateClassification.Definition)]
    [InlineData("Security Intelligence Update for Microsoft Defender Antivirus - KB2267602 (Version 1.463.1.0)", WindowsUpdateClassification.Definition)]
    [InlineData("2026-10 Cumulative Update for Windows 11 Version 25H2 for x64-based Systems (KB5070001)", WindowsUpdateClassification.Quality)]
    [InlineData("Aggiornamento cumulativo 2026-10 per Windows 11 Version 25H2 per sistemi basati su x64 (KB5070001)", WindowsUpdateClassification.Quality)]
    [InlineData("Feature update to Windows 11, version 25H2", WindowsUpdateClassification.Feature)]
    [InlineData("Aggiornamento delle funzionalità a Windows 11, versione 25H2", WindowsUpdateClassification.Feature)]
    [InlineData("Intel Corporation - System - 10.1.54.3", WindowsUpdateClassification.Driver)]
    [InlineData("Realtek driver update", WindowsUpdateClassification.Driver)]
    [InlineData("Strumento di rimozione malware", WindowsUpdateClassification.Other)]
    [InlineData("", WindowsUpdateClassification.Other)]
    public void HistoryTitlesAreClassifiedWithoutCategoryIds(string title, WindowsUpdateClassification expected) =>
        Assert.Equal(expected, WindowsUpdateClassifier.FromTitle(title));

    [Fact]
    public void CategoryIdWinsOverTitle() =>
        Assert.Equal(
            WindowsUpdateClassification.Driver,
            WindowsUpdateClassifier.FromCategoryIds(["ebfc1fc5-71a4-4f7b-9aca-3b9a503104a0"], "Cumulative Update"));

    [Fact]
    public void ShowHiddenSearchesOnlyHiddenUpdates()
    {
        Assert.Equal(
            "IsInstalled=0 and IsHidden=1 and Type='Software' and BrowseOnly=0",
            WindowsUpdateSearchCriteria.Build(new WindowsUpdateOptions(ShowHidden: true)));
        Assert.Equal(
            "IsInstalled=0 and IsHidden=0 and Type='Software' and BrowseOnly=0",
            WindowsUpdateSearchCriteria.Build(new WindowsUpdateOptions()));
    }

    // WU_E_SERVICE_STOP (0x8024001E) caused the 17 November scan failure; a retry succeeded two minutes later.
    [Theory]
    [InlineData(0x8024001Eu, true)]
    [InlineData(0x80240016u, true)]
    [InlineData(0x80240021u, true)]
    [InlineData(0x80240025u, false)] // blocked by policy: retrying cannot help
    [InlineData(0x80070005u, false)] // access denied
    public void OnlyServiceBusyErrorsAreTransient(uint hResult, bool transient) =>
        Assert.Equal(transient, WindowsUpdateErrorCodes.IsTransient(unchecked((int)hResult)));

    [Theory]
    [InlineData(0x80070005u, "Access is denied")]
    [InlineData(0x80240020u, "interactive user")]
    [InlineData(0x8024002Fu, "DisableWindowsUpdateAccess")]
    public void NewErrorCodesAreDescribed(uint hResult, string expectedFragment) =>
        Assert.Contains(expectedFragment, WindowsUpdateErrorCodes.Describe(unchecked((int)hResult)));

    [Theory]
    [InlineData(3, 0, "Installed with errors.")]
    [InlineData(4, unchecked((int)0x80240022), "Installation failed: The operation failed for all the updates")]
    [InlineData(5, 0, "Installation was aborted.")]
    [SupportedOSPlatform("windows")]
    public void InstallFailuresCarryTheDecodedHResult(int resultCode, int hResult, string expectedPrefix)
    {
        var message = ComWindowsUpdateService.DescribeInstallFailure(resultCode, hResult);

        Assert.StartsWith(expectedPrefix.TrimEnd('.'), message.TrimEnd('.'));
    }

    [Theory]
    [InlineData(2, WindowsUpdateHistoryResult.Succeeded)]
    [InlineData(3, WindowsUpdateHistoryResult.SucceededWithErrors)]
    [InlineData(4, WindowsUpdateHistoryResult.Failed)]
    [InlineData(5, WindowsUpdateHistoryResult.Aborted)]
    [SupportedOSPlatform("windows")]
    public void HistoryResultCodesMapToStates(int code, WindowsUpdateHistoryResult expected) =>
        Assert.Equal(expected, ComWindowsUpdateManagementService.ToHistoryResult(code));

    [Theory]
    [InlineData("2026-10 Cumulative Update (KB5070001)", "KB5070001")]
    [InlineData("Aggiornamento KB 5070001 cumulativo", "KB5070001")]
    [InlineData("Intel - System - 10.1.54.3", null)]
    [SupportedOSPlatform("windows")]
    public void KnowledgeBaseNumberIsExtractedFromTheTitle(string title, string? expected) =>
        Assert.Equal(expected, ComWindowsUpdateManagementService.ExtractKnowledgeBase(title));

    [Fact]
    public void OnlySuccessfulNonDefinitionInstallsCanBeUninstalledFromHistory()
    {
        WindowsUpdateHistoryEntry Entry(WindowsUpdateHistoryResult result, WindowsUpdateClassification group, string? id = "id", WindowsUpdateHistoryOperation op = WindowsUpdateHistoryOperation.Install) =>
            new(DateTimeOffset.UtcNow, "Update", op, result, 0, group, null, id);

        Assert.True(Application.App.OnlyWingetApplication.CanUninstallFromHistory(Entry(WindowsUpdateHistoryResult.Succeeded, WindowsUpdateClassification.Quality)));
        Assert.False(Application.App.OnlyWingetApplication.CanUninstallFromHistory(Entry(WindowsUpdateHistoryResult.Failed, WindowsUpdateClassification.Quality)));
        Assert.False(Application.App.OnlyWingetApplication.CanUninstallFromHistory(Entry(WindowsUpdateHistoryResult.Succeeded, WindowsUpdateClassification.Definition)));
        Assert.False(Application.App.OnlyWingetApplication.CanUninstallFromHistory(Entry(WindowsUpdateHistoryResult.Succeeded, WindowsUpdateClassification.Quality, id: null)));
        Assert.False(Application.App.OnlyWingetApplication.CanUninstallFromHistory(Entry(WindowsUpdateHistoryResult.Succeeded, WindowsUpdateClassification.Quality, op: WindowsUpdateHistoryOperation.Uninstall)));
    }
}
