using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Winget;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class OperationProgressFormatterTests
{
    private static readonly Dictionary<string, string> EnglishResources = new()
    {
        ["Progress_Starting"] = "Preparing",
        ["Progress_Downloading"] = "Downloading",
        ["Progress_Installing"] = "Installing",
        ["Progress_Completed"] = "Completed",
        ["Progress_Failed"] = "Failed"
    };

    private static readonly Dictionary<string, string> ItalianResources = new()
    {
        ["Progress_Starting"] = "Preparazione",
        ["Progress_Downloading"] = "Download",
        ["Progress_Installing"] = "Installazione",
        ["Progress_Completed"] = "Completato",
        ["Progress_Failed"] = "Errore"
    };

    [Fact]
    public void FormatMessage_ReturnsStarting_WhenProgressIsNull()
    {
        var englishResult = OperationProgressFormatter.FormatMessage(null, key => EnglishResources.GetValueOrDefault(key, key));
        var italianResult = OperationProgressFormatter.FormatMessage(null, key => ItalianResources.GetValueOrDefault(key, key));

        Assert.Equal("Preparing", englishResult);
        Assert.Equal("Preparazione", italianResult);
    }

    [Fact]
    public void FormatMessage_ReturnsPhaseOnly_WhenPackageIdIsEmpty()
    {
        var progress = new OperationProgress(string.Empty, WingetProgressPhase.Installing, 10, 0, 3);
        var result = OperationProgressFormatter.FormatMessage(progress, key => EnglishResources.GetValueOrDefault(key, key));

        Assert.Equal("Installing", result);
    }

    [Fact]
    public void FormatMessage_FormatsSinglePackageProgressCorrectly()
    {
        var progress = new OperationProgress("Git.Git", WingetProgressPhase.Installing, 50, 0, 1);
        var englishResult = OperationProgressFormatter.FormatMessage(progress, key => EnglishResources.GetValueOrDefault(key, key));
        var italianResult = OperationProgressFormatter.FormatMessage(progress, key => ItalianResources.GetValueOrDefault(key, key));

        Assert.Equal("Installing (1/1): Git.Git", englishResult);
        Assert.Equal("Installazione (1/1): Git.Git", italianResult);
    }

    [Fact]
    public void FormatMessage_FormatsMultiPackageProgressCorrectly()
    {
        var progress1 = new OperationProgress("Git.Git", WingetProgressPhase.Downloading, 25, 0, 3);
        var progress2 = new OperationProgress("7zip.7zip", WingetProgressPhase.Installing, 60, 1, 3);
        var progress3 = new OperationProgress("Mozilla.Firefox", WingetProgressPhase.Installing, 90, 2, 3);

        var italian1 = OperationProgressFormatter.FormatMessage(progress1, key => ItalianResources.GetValueOrDefault(key, key));
        var italian2 = OperationProgressFormatter.FormatMessage(progress2, key => ItalianResources.GetValueOrDefault(key, key));
        var italian3 = OperationProgressFormatter.FormatMessage(progress3, key => ItalianResources.GetValueOrDefault(key, key));

        Assert.Equal("Download (1/3): Git.Git", italian1);
        Assert.Equal("Installazione (2/3): 7zip.7zip", italian2);
        Assert.Equal("Installazione (3/3): Mozilla.Firefox", italian3);
    }

    [Fact]
    public void FormatProgressText_IncludesPercentageAndPackageId()
    {
        var progress = new OperationProgress("Git.Git", WingetProgressPhase.Installing, 75, 1, 4);
        var result = OperationProgressFormatter.FormatProgressText(progress, key => EnglishResources.GetValueOrDefault(key, key));

        Assert.Equal("Installing (2/4) · 75% · Git.Git", result);
    }
}
