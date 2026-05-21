// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class SourceAlignmentTests
{
    [Fact]
    public void MsiSource_AllowsSameVersionUpgrade_ToAvoidDuplicateArpEntriesOnReinstall()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Setup.wxs"));

        Assert.Contains("<MajorUpgrade", source, StringComparison.Ordinal);
        Assert.Contains("AllowSameVersionUpgrades=\"yes\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MsiSource_HidesInternalMsiFromAddRemovePrograms_WhenInstalledByBundle()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Setup.wxs"));

        Assert.Contains("<Property Id=\"ARPSYSTEMCOMPONENT\" Value=\"1\" />", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingScript_PublishesSelfContainedPayloads_ForCleanWindowsInstalls()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "scripts", "package.ps1"));

        Assert.Contains("'--self-contained'", source, StringComparison.Ordinal);
        Assert.Contains("'true'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("'--self-contained'\r\n        'false'", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BundleSource_BlocksUnsupportedWindowsBeforeInstallingPayload()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Bundle.wxs"));

        Assert.Contains("<bal:Condition", source, StringComparison.Ordinal);
        Assert.Contains("OnlyWinget requires Windows 10 or Windows 11", source, StringComparison.Ordinal);
        Assert.Contains("VersionNT >= v10.0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MsiSource_BlocksUnsupportedWindowsForDirectMsiExecution()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Setup.wxs"));

        Assert.Contains("WindowsBuildNumberSearch", source, StringComparison.Ordinal);
        Assert.Contains("CurrentBuildNumber", source, StringComparison.Ordinal);
        Assert.Contains("WINDOWSBUILDNUMBER &gt;= 10240", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MsiSource_DoesNotCarryServiceCleanupCustomActions_ForDesktopAppPackaging()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Setup.wxs"));

        Assert.DoesNotContain("ServiceCleanupScriptComponent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupProjectServicesForUninstall", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MsiSource_DoesNotRecursivelyDeleteMutableRegistryInstallFolder()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Setup.wxs"));

        Assert.DoesNotContain("RemoveFolderEx", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ONLYWINGET_INSTALLFOLDER", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WixUtilExtension", source, StringComparison.Ordinal);
        Assert.Contains("<RemoveFolder Id=\"RemoveInstallFolder\" On=\"uninstall\" />", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TableCellTextStyle_UsesSingleLineEllipsisForCompactRows()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Styles", "Templates.xaml"));
        var tableCellStyleStart = source.IndexOf("x:Key=\"TableCellTextStyle\"", StringComparison.Ordinal);
        var tableCellStyleEnd = source.IndexOf("</Style>", tableCellStyleStart, StringComparison.Ordinal);

        Assert.True(tableCellStyleStart >= 0);
        Assert.True(tableCellStyleEnd > tableCellStyleStart);

        var styleSource = source[tableCellStyleStart..tableCellStyleEnd];
        Assert.Contains("Property=\"TextWrapping\" Value=\"NoWrap\"", styleSource, StringComparison.Ordinal);
        Assert.Contains("Property=\"TextTrimming\" Value=\"CharacterEllipsis\"", styleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GridViewAutoSizeBehavior_CompensatesForListViewBorderWidth()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Helpers", "GridViewAutoSizeBehavior.cs"));

        Assert.Contains("listView.BorderThickness.Left + listView.BorderThickness.Right", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GridViewColumnHeaderStyle_UsesExplicitTemplateWithHeaderGripper()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Styles", "Controls.xaml"));

        Assert.Contains("<Style TargetType=\"GridViewColumnHeader\">", source, StringComparison.Ordinal);
        Assert.Contains("<Thumb x:Name=\"PART_HeaderGripper\"", source, StringComparison.Ordinal);
        Assert.Contains("<ContentPresenter Margin=\"{TemplateBinding Padding}\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageInterrogationDialog_InputControlsExposeAutomationNames()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Dialogs", "PackageInterrogationDialog.xaml"));
        var controlMatches = Regex.Matches(source, @"<(ComboBox|TextBox)\b(?:(?!/>|</\1>).)*(?:/>|</\1>)", RegexOptions.Singleline);

        Assert.NotEmpty(controlMatches);

        foreach (Match match in controlMatches)
        {
            Assert.Contains("AutomationProperties.Name", match.Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DialogWindows_UseOnlyWingetIcon()
    {
        var dialogsRoot = Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Dialogs");

        Assert.Contains(
            "Icon=\"/OnlyWinget;component/Assets/OnlyWinget.ico\"",
            File.ReadAllText(Path.Combine(dialogsRoot, "PackageInterrogationDialog.xaml")),
            StringComparison.Ordinal);
        Assert.Contains(
            "Icon=\"/OnlyWinget;component/Assets/OnlyWinget.ico\"",
            File.ReadAllText(Path.Combine(dialogsRoot, "TextPromptWindow.xaml")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PackageInterrogationDialog_ConfirmButtonIsDefaultAction()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Dialogs", "PackageInterrogationDialog.xaml"));
        var confirmButton = Regex.Match(source, "<Button Grid.Column=\"3\"(?:(?!/>).)*/>", RegexOptions.Singleline);

        Assert.True(confirmButton.Success);
        Assert.Contains("IsDefault=\"True\"", confirmButton.Value, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnConfirmClick\"", confirmButton.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_StatusBadgesExposeFullStatusInTooltips()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "MainWindow.xaml"));
        var statusTextBlocks = Regex.Matches(
            source,
            "<TextBlock Style=\"\\{StaticResource StatusBadgeTextStyle\\}\"(?:(?!/>).)*/>",
            RegexOptions.Singleline);

        Assert.Equal(2, statusTextBlocks.Count);
        foreach (Match match in statusTextBlocks)
        {
            Assert.Contains("Text=\"{Binding StatusBadgeSymbol}\"", match.Value, StringComparison.Ordinal);
            Assert.Contains("ToolTip=\"{Binding Status}\"", match.Value, StringComparison.Ordinal);
        }

        Assert.Equal(2, Regex.Matches(source, "AutomationProperties.Name=\"\\{Binding Status\\}\"").Count);
    }

    [Fact]
    public void TableHeadersAndCellsUseSharedAlignedPaddingTokens()
    {
        var root = GetRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(root, "src", "OnlyWinget", "Styles", "Tokens.xaml"));
        var controls = File.ReadAllText(Path.Combine(root, "src", "OnlyWinget", "Styles", "Controls.xaml"));
        var templates = File.ReadAllText(Path.Combine(root, "src", "OnlyWinget", "Styles", "Templates.xaml"));

        Assert.Contains("x:Key=\"TableCellPadding\">10,6</Thickness>", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"TableHeaderPadding\">10,6</Thickness>", tokens, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"{DynamicResource TableHeaderPadding}\"", controls, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"{DynamicResource TableCellPadding}\"", templates, StringComparison.Ordinal);
        Assert.Contains("Property=\"Padding\" Value=\"{DynamicResource TableCellCompactPadding}\"", templates, StringComparison.Ordinal);
    }

    [Fact]
    public void GridViewAutoSizeBehavior_IgnoresResizeFeedbackAndPreservesStarMinimums()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Helpers", "GridViewAutoSizeBehavior.cs"));

        Assert.Contains("IsResizing", source, StringComparison.Ordinal);
        Assert.Contains("SetColumnWidth(state, column", source, StringComparison.Ordinal);
        Assert.Contains("Math.Max(0, available - starMinWidth)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressBarIndeterminateState_DoesNotUseFixedTravelDistance()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Styles", "Controls.xaml"));
        var progressBarStyleStart = source.IndexOf("<Style TargetType=\"ProgressBar\">", StringComparison.Ordinal);
        var progressBarStyleEnd = source.IndexOf("</Style>", progressBarStyleStart, StringComparison.Ordinal);

        Assert.True(progressBarStyleStart >= 0);
        Assert.True(progressBarStyleEnd > progressBarStyleStart);

        var styleSource = source[progressBarStyleStart..progressBarStyleEnd];
        Assert.Contains("HorizontalAlignment=\"Stretch\"", styleSource, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetProperty=\"Opacity\"", styleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("To=\"1200\"", styleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"80\"", styleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IndeterminateTranslate", styleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageInterrogationDialog_UsesSharedLabelColumnToken()
    {
        var root = GetRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(root, "src", "OnlyWinget", "Styles", "Tokens.xaml"));
        var dialog = File.ReadAllText(Path.Combine(root, "src", "OnlyWinget", "Dialogs", "PackageInterrogationDialog.xaml"));

        Assert.Contains("x:Key=\"PackageDialogLabelColumnWidth\">180</GridLength>", tokens, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinition Width=\"180\"", dialog, StringComparison.Ordinal);
        Assert.Equal(
            12,
            Regex.Matches(dialog, "ColumnDefinition Width=\"\\{StaticResource PackageDialogLabelColumnWidth\\}\"").Count);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OnlyWinget.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
