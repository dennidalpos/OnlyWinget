// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OnlyWinget.Dialogs;
using OnlyWinget.Models;
using OnlyWinget.ViewModels;

namespace OnlyWinget.Services;

public sealed class DialogService : IDialogService
{
    private readonly IWingetPackageInterrogationService _interrogationService;
    private readonly LocalizationService _localizationService;

    public DialogService(IWingetPackageInterrogationService interrogationService, LocalizationService localizationService)
    {
        _interrogationService = interrogationService;
        _localizationService = localizationService;
    }

    public string Prompt(string prompt, string title, string defaultValue = "")
    {
        var strings = _localizationService.Strings;
        var window = new TextPromptWindow(
            title,
            prompt,
            defaultValue,
            confirmLabel: strings.PromptConfirmLabel,
            cancelLabel: strings.PromptCancelLabel)
        {
            Owner = Application.Current?.MainWindow
        };

        return window.ShowDialog() == true
            ? window.ResponseText
            : string.Empty;
    }

    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public string? OpenFile(string title, string filter, string defaultExtension = "json")
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            CheckFileExists = true
        };

        return dialog.ShowDialog(Application.Current?.MainWindow) == true
            ? dialog.FileName
            : null;
    }

    public string? SaveFile(string title, string filter, string defaultFileName, string defaultExtension = "json")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            FileName = defaultFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog(Application.Current?.MainWindow) == true
            ? dialog.FileName
            : null;
    }

    public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationAsync(PackageInterrogationRequest request)
        => RunInterrogationDialogAsync(request, existingEntry: null);

    public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationEditAsync(PackageInterrogationRequest request, AppEntry existingEntry)
        => RunInterrogationDialogAsync(request, existingEntry);

    private Task<PackageInterrogationDialogResult?> RunInterrogationDialogAsync(PackageInterrogationRequest request, AppEntry? existingEntry)
    {
        var strings = _localizationService.Strings;
        var viewModel = new PackageInterrogationDialogViewModel(strings);
        viewModel.ConfigureForEditMode(existingEntry != null);
        var window = new PackageInterrogationDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };

        PackageInterrogationResult? interrogation = null;
        window.Loaded += async (_, _) =>
        {
            try
            {
                interrogation = await _interrogationService.InterrogateAsync(request).ConfigureAwait(true);
                if (!interrogation.Success)
                {
                    ShowError(interrogation.ErrorMessage, strings.Title);
                    window.DialogResult = false;
                    window.Close();
                    return;
                }

                viewModel.ApplyInterrogationResult(interrogation);

                // Pre-populate fields from the existing entry when editing a queued item.
                if (existingEntry != null)
                {
                    viewModel.ApplyExistingEntry(existingEntry);
                }
            }
            catch (System.Exception ex)
            {
                ShowError(ex.Message, strings.Title);
                window.DialogResult = false;
                window.Close();
            }
        };

        var confirmed = window.ShowDialog() == true;
        if (!confirmed || interrogation == null || !interrogation.Success)
        {
            return Task.FromResult<PackageInterrogationDialogResult?>(null);
        }

        return Task.FromResult<PackageInterrogationDialogResult?>(new PackageInterrogationDialogResult
        {
            Interrogation = interrogation,
            SelectedOptions = viewModel.BuildSelection(),
            QueueSelections = viewModel.BuildSelections()
        });
    }
}
