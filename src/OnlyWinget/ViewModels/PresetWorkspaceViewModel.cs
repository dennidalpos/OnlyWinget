// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using OnlyWinget.Commands;
using OnlyWinget.Models;
using OnlyWinget.Services;

namespace OnlyWinget.ViewModels;

public sealed class PresetWorkspaceViewModel : ObservableObject
{
    private readonly bool _isWingetAvailable;
    private readonly LocalizationService _localizationService;
    private readonly PresetWorkspaceService _presetWorkspaceService;
    private readonly IDialogService _dialogService;
    private readonly IAppEntryService _appEntryService;
    private readonly ITabService _tabService;
    private readonly Action<string> _appendOutput;
    private readonly Dictionary<string, ObservableCollection<AppEntry>> _tabs = new(StringComparer.OrdinalIgnoreCase);
    private string _selectedTabName = string.Empty;
    private ObservableCollection<AppEntry> _currentApps = new();
    private AppEntry? _selectedApp;
    private bool _areActionsEnabled = true;

    public PresetWorkspaceViewModel(
        bool isWingetAvailable,
        LocalizationService localizationService,
        PresetWorkspaceService presetWorkspaceService,
        IDialogService dialogService,
        IAppEntryService appEntryService,
        ITabService tabService,
        Action<string> appendOutput)
    {
        _isWingetAvailable = isWingetAvailable;
        _localizationService = localizationService;
        _presetWorkspaceService = presetWorkspaceService;
        _dialogService = dialogService;
        _appEntryService = appEntryService;
        _tabService = tabService;
        _appendOutput = appendOutput;

        AvailableActions = new ObservableCollection<ActionOption>
        {
            new() { Value = AppActions.Install, Label = Strings.Install },
            new() { Value = AppActions.Uninstall, Label = Strings.Uninstall },
            new() { Value = AppActions.Pause, Label = Strings.Pause }
        };
        TabNames = new ObservableCollection<string>();

        AddCommand = new RelayCommand(AddApp, () => _isWingetAvailable && AreActionsEnabled);
        EditCommand = new RelayCommand(EditAppAsync, () => SelectedApp != null && AreActionsEnabled);
        RemoveCommand = new RelayCommand(RemoveApp, () => SelectedApp != null && AreActionsEnabled);
        SaveCommand = new RelayCommand(SaveData, () => _isWingetAvailable && AreActionsEnabled);
        NewTabCommand = new RelayCommand(CreateTab, () => _isWingetAvailable && AreActionsEnabled);
        RenameTabCommand = new RelayCommand(RenameTab, () => _isWingetAvailable && AreActionsEnabled);
        DeleteTabCommand = new RelayCommand(DeleteTab, () => _isWingetAvailable && AreActionsEnabled);
        ImportPresetCommand = new RelayCommand(ImportPreset, () => AreActionsEnabled);
        ExportPresetCommand = new RelayCommand(ExportPreset, () => AreActionsEnabled && !string.IsNullOrWhiteSpace(SelectedTabName) && _tabs.ContainsKey(SelectedTabName));

        AttachCurrentAppsCollection(_currentApps);
        _localizationService.PropertyChanged += OnLocalizationServicePropertyChanged;
    }

    public LocalizedStrings Strings => _localizationService.Strings;

    public ObservableCollection<ActionOption> AvailableActions { get; }

    public ObservableCollection<string> TabNames { get; }

    public string CurrentPresetName => string.IsNullOrWhiteSpace(SelectedTabName)
        ? Strings.NewTabDefaultName
        : SelectedTabName;

    public string CurrentPresetAppCountText => string.Format(Strings.PresetAppCountText, CurrentApps.Count);

    public string SelectedTabName
    {
        get => _selectedTabName;
        set
        {
            if (SetProperty(ref _selectedTabName, value))
            {
                UpdateCurrentTab(value);
                RaiseShellStateChanged();
                RaiseCommandCanExecute();
            }
        }
    }

    public ObservableCollection<AppEntry> CurrentApps
    {
        get => _currentApps;
        private set
        {
            if (ReferenceEquals(_currentApps, value))
            {
                return;
            }

            _currentApps.CollectionChanged -= OnCurrentAppsCollectionChanged;
            if (SetProperty(ref _currentApps, value))
            {
                _currentApps.CollectionChanged += OnCurrentAppsCollectionChanged;
                OnPropertyChanged(nameof(CurrentPresetAppCountText));
            }
        }
    }

    public AppEntry? SelectedApp
    {
        get => _selectedApp;
        set
        {
            if (SetProperty(ref _selectedApp, value))
            {
                RaiseCommandCanExecute();
            }
        }
    }

    public bool AreActionsEnabled
    {
        get => _areActionsEnabled;
        set
        {
            if (SetProperty(ref _areActionsEnabled, value))
            {
                RaiseMainCommandCanExecute();
            }
        }
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand NewTabCommand { get; }
    public RelayCommand RenameTabCommand { get; }
    public RelayCommand DeleteTabCommand { get; }
    public RelayCommand ImportPresetCommand { get; }
    public RelayCommand ExportPresetCommand { get; }

    public void Initialize()
    {
        var loadResult = _presetWorkspaceService.Load();
        TabNames.Clear();
        _tabs.Clear();

        foreach (var tabName in loadResult.TabNames)
        {
            TabNames.Add(tabName);
            _tabs[tabName] = new ObservableCollection<AppEntry>(loadResult.Tabs[tabName]);
        }

        if (TabNames.Count > 0)
        {
            SelectedTabName = TabNames[0];
        }

        HandleLoadResult(loadResult);
        RefreshLocalizedState();
    }

    public void SetAppStatus(string id, UiStatusState status)
    {
        var target = CurrentApps.FirstOrDefault(app => string.Equals(app.OperationKey, id, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            target.ApplyStatus(status, Strings);
        }
    }

    public void SetAppError(string id, string errorMessage, string resolution)
    {
        var target = CurrentApps.FirstOrDefault(app => string.Equals(app.OperationKey, id, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            target.ErrorMessage = errorMessage;
            target.Resolution = resolution;
        }
    }

    public bool TryAddEntry(string? name, string? id)
    {
        return TryAddEntry(name, id, "winget", out _, showDialog: true);
    }

    public bool TryAddEntry(PackageInterrogationResult interrogation, SelectedInstallOptions selectedOptions, out string warning, bool showDialog)
    {
        return TryAddEntries(interrogation, new[] { selectedOptions }, out warning, showDialog);
    }

    public bool TryAddEntries(PackageInterrogationResult interrogation, IReadOnlyList<SelectedInstallOptions> selectedOptions, out string warning, bool showDialog)
    {
        warning = string.Empty;
        var options = selectedOptions.Count > 0 ? selectedOptions : new[] { new SelectedInstallOptions() };
        var pendingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in options)
        {
            var architecture = option.Architecture?.Trim() ?? string.Empty;
            var validation = _appEntryService.ValidateResolvedForInsert(interrogation.Id, CurrentApps, architecture);
            if (validation != AppEntryValidationError.None)
            {
                warning = GetValidationWarning(validation, interrogation.Id, architecture);
                if (showDialog && !string.IsNullOrWhiteSpace(warning))
                {
                    _dialogService.ShowWarning(warning, validation == AppEntryValidationError.DuplicateId ? Strings.DuplicateIdTitle : Strings.InvalidIdTitle);
                }

                return false;
            }

            var pendingKey = AppEntry.BuildOperationKey(interrogation.Id, architecture);
            if (!pendingKeys.Add(pendingKey))
            {
                warning = GetValidationWarning(AppEntryValidationError.DuplicateId, interrogation.Id, architecture);
                if (showDialog && !string.IsNullOrWhiteSpace(warning))
                {
                    _dialogService.ShowWarning(warning, Strings.DuplicateIdTitle);
                }

                return false;
            }
        }

        foreach (var option in options)
        {
            CurrentApps.Add(_appEntryService.Create(interrogation, option));
        }

        return true;
    }

    public bool TryAddEntry(string? name, string? id, out string warning, bool showDialog)
    {
        return TryAddEntry(name, id, "winget", out warning, showDialog);
    }

    public bool TryAddEntry(string? name, string? id, string? source, out string warning, bool showDialog)
    {
        warning = string.Empty;
        var normalizedId = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return false;
        }

        var validation = _appEntryService.ValidateForInsert(normalizedId, CurrentApps, source);
        if (validation != AppEntryValidationError.None)
        {
            warning = GetValidationWarning(validation, normalizedId);
            if (showDialog && !string.IsNullOrWhiteSpace(warning))
            {
                _dialogService.ShowWarning(warning, validation == AppEntryValidationError.DuplicateId ? Strings.DuplicateIdTitle : Strings.InvalidIdTitle);
            }

            return false;
        }

        CurrentApps.Add(_appEntryService.Create(name, normalizedId, source));
        return true;
    }

    private void AttachCurrentAppsCollection(ObservableCollection<AppEntry> collection)
    {
        _currentApps = collection;
        _currentApps.CollectionChanged += OnCurrentAppsCollectionChanged;
    }

    private void OnCurrentAppsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentPresetAppCountText));
    }

    private void UpdateCurrentTab(string tabName)
    {
        if (_tabs.TryGetValue(tabName, out var list))
        {
            CurrentApps = list;
            SelectedApp = null;
        }
    }

    private void AddApp()
    {
        var name = _dialogService.Prompt(Strings.InputNamePrompt, Strings.InputNameTitle, string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        while (true)
        {
            var id = _dialogService.Prompt(Strings.InputIdPrompt, Strings.InputIdTitle, string.Empty);
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var source = _dialogService.Prompt(Strings.InputSourcePrompt, Strings.InputSourceTitle, "winget");
            var effectiveSource = string.IsNullOrWhiteSpace(source) ? "winget" : source.Trim();

            if (TryAddEntry(name, id, effectiveSource, out _, showDialog: true))
            {
                return;
            }
        }
    }

    private async void EditAppAsync()
    {
        if (SelectedApp == null)
        {
            return;
        }

        var app = SelectedApp;
        var request = new PackageInterrogationRequest
        {
            PackageId = app.Id,
            PackageName = app.Name,
            Version = app.Version,
            Source = string.IsNullOrWhiteSpace(app.Source) ? "winget" : app.Source,
            Log = _appendOutput
        };

        PackageInterrogationDialogResult? dialogResult;
        try
        {
            dialogResult = await _dialogService.ShowPackageInterrogationEditAsync(request, app);
        }
        catch (Exception ex)
        {
            _appendOutput($"event=edit_failed id=\"{app.Id}\" message=\"{ex.Message}\"");
            return;
        }

        if (dialogResult == null)
        {
            return;
        }

        var editedName = string.IsNullOrWhiteSpace(dialogResult.Interrogation.Name) ? app.Name : dialogResult.Interrogation.Name.Trim();
        var editedId = string.IsNullOrWhiteSpace(dialogResult.Interrogation.Id) ? app.Id : dialogResult.Interrogation.Id.Trim();
        var editedSource = string.IsNullOrWhiteSpace(dialogResult.Interrogation.Source)
            ? (string.IsNullOrWhiteSpace(app.Source) ? "winget" : app.Source)
            : dialogResult.Interrogation.Source.Trim();
        var editedArchitecture = dialogResult.SelectedOptions.Architecture?.Trim() ?? string.Empty;
        var validation = _appEntryService.ValidateForEdit(editedId, app.Id, CurrentApps, editedSource, editedArchitecture, app.Architecture);
        if (validation != AppEntryValidationError.None)
        {
            var warning = GetValidationWarning(validation, editedId, editedArchitecture);
            if (!string.IsNullOrWhiteSpace(warning))
            {
                _dialogService.ShowWarning(warning, validation == AppEntryValidationError.DuplicateId ? Strings.DuplicateIdTitle : Strings.InvalidIdTitle);
            }

            return;
        }

        app.Name = editedName;
        app.Id = editedId;
        app.Source = editedSource;
        app.Version = string.IsNullOrWhiteSpace(dialogResult.Interrogation.Version)
            ? app.Version
            : dialogResult.Interrogation.Version.Trim();
        app.Scope = dialogResult.SelectedOptions.Scope ?? string.Empty;
        app.InstallMode = string.IsNullOrWhiteSpace(dialogResult.SelectedOptions.InstallMode)
            ? InstallModes.SilentWithProgress
            : dialogResult.SelectedOptions.InstallMode;
        app.Architecture = dialogResult.SelectedOptions.Architecture ?? string.Empty;
        app.Locale = dialogResult.SelectedOptions.Locale ?? string.Empty;
        app.InstallerType = dialogResult.SelectedOptions.InstallerType ?? string.Empty;
        app.InstallLocation = dialogResult.SelectedOptions.InstallLocation ?? string.Empty;
        app.LogPath = dialogResult.SelectedOptions.LogPath ?? string.Empty;
        app.AdditionalCustomArgs = dialogResult.SelectedOptions.AdditionalCustomArgs ?? string.Empty;
        app.OverrideArgs = dialogResult.SelectedOptions.OverrideArgs ?? string.Empty;
        app.ManifestFingerprint = dialogResult.Interrogation.ManifestFingerprint ?? string.Empty;
        app.InterrogatedAtUtc = dialogResult.Interrogation.InterrogatedAtUtc.ToString("O");
        app.ElevationRequirement = dialogResult.SelectedOptions.ElevationRequirement ?? string.Empty;

        _appendOutput($"event=queue_item_updated id=\"{app.Id}\" scope=\"{app.Scope}\" mode=\"{app.InstallMode}\" arch=\"{app.Architecture}\"");
    }

    private void RemoveApp()
    {
        if (SelectedApp == null)
        {
            return;
        }

        CurrentApps.Remove(SelectedApp);
        SelectedApp = null;
    }

    private void SaveData()
    {
        PersistPresetLibrary(showSuccessFeedback: true, appendSuccessOutput: true);
    }

    private bool PersistPresetLibrary(bool showSuccessFeedback, bool appendSuccessOutput)
    {
        var result = _presetWorkspaceService.Save(TabNames.ToList(), _tabs);
        if (result.Success)
        {
            if (appendSuccessOutput)
            {
                _appendOutput(Strings.SaveSuccessText);
            }

            if (showSuccessFeedback)
            {
                _dialogService.ShowInfo(Strings.SaveSuccessText, Strings.SaveSuccessTitle);
            }

            return true;
        }

        _dialogService.ShowError(
            string.Format(Strings.SaveErrorText, result.Path, result.ErrorMessage),
            Strings.SaveErrorTitle);

        return false;
    }

    private void CreateTab()
    {
        var name = _dialogService.Prompt(Strings.TabNamePrompt, Strings.TabNameTitle, Strings.NewTabDefaultName);
        if (!_tabService.TryCreate(name, _tabs, TabNames, out var createdName, out var error))
        {
            if (error == TabOperationError.AlreadyExists)
            {
                _dialogService.ShowWarning(Strings.TabExistsText, Strings.TabExistsTitle);
            }

            return;
        }

        SelectedTabName = createdName;
        PersistPresetLibrary(showSuccessFeedback: false, appendSuccessOutput: false);
    }

    private void RenameTab()
    {
        if (string.IsNullOrWhiteSpace(SelectedTabName))
        {
            return;
        }

        var newName = _dialogService.Prompt(Strings.TabRenamePrompt, Strings.TabRenameTitle, SelectedTabName);
        if (!_tabService.TryRename(SelectedTabName, newName, _tabs, TabNames, out var renamedName, out var error))
        {
            if (error == TabOperationError.AlreadyExists)
            {
                _dialogService.ShowWarning(Strings.TabExistsText, Strings.TabExistsTitle);
            }

            return;
        }

        SelectedTabName = renamedName;
        PersistPresetLibrary(showSuccessFeedback: false, appendSuccessOutput: false);
    }

    private void DeleteTab()
    {
        if (!_tabService.TryDelete(SelectedTabName, _tabs, TabNames, out var nextSelectedName, out var error))
        {
            if (error == TabOperationError.CannotDeleteLast)
            {
                _dialogService.ShowInfo(Strings.NoTabToDeleteText, Strings.NoTabToDeleteTitle);
            }

            return;
        }

        SelectedTabName = nextSelectedName;
        PersistPresetLibrary(showSuccessFeedback: false, appendSuccessOutput: false);
    }

    private void ImportPreset()
    {
        var selectedPath = _dialogService.OpenFile(Strings.ImportPresetTitle, Strings.PresetFileDialogFilter);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var result = _presetWorkspaceService.ImportPreset(selectedPath, TabNames);
        if (!result.Success)
        {
            _dialogService.ShowError(
                string.Format(Strings.ImportPresetErrorText, result.Path, result.ErrorMessage),
                Strings.ImportPresetErrorTitle);
            return;
        }

        TabNames.Add(result.ImportedPresetName);
        _tabs[result.ImportedPresetName] = new ObservableCollection<AppEntry>(result.Apps);
        SelectedTabName = result.ImportedPresetName;
        PersistPresetLibrary(showSuccessFeedback: false, appendSuccessOutput: false);

        var successText = string.Format(Strings.ImportPresetSuccessText, result.ImportedPresetName);
        _appendOutput(successText);
        _dialogService.ShowInfo(successText, Strings.ImportPresetSuccessTitle);
    }

    private void ExportPreset()
    {
        if (!_tabs.ContainsKey(SelectedTabName))
        {
            return;
        }

        var defaultFileName = _presetWorkspaceService.GetDefaultPresetExportFileName(SelectedTabName);
        var selectedPath = _dialogService.SaveFile(Strings.ExportPresetTitle, Strings.PresetFileDialogFilter, defaultFileName);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var result = _presetWorkspaceService.ExportPreset(selectedPath, SelectedTabName, CurrentApps);
        if (!result.Success)
        {
            _dialogService.ShowError(
                string.Format(Strings.ExportPresetErrorText, result.Path, result.ErrorMessage),
                Strings.ExportPresetErrorTitle);
            return;
        }

        var successText = string.Format(Strings.ExportPresetSuccessText, SelectedTabName, result.Path);
        _appendOutput(successText);
        _dialogService.ShowInfo(successText, Strings.ExportPresetSuccessTitle);
    }

    private void HandleLoadResult(AppDataLoadResult loadResult)
    {
        switch (loadResult.Status)
        {
            case AppDataLoadStatus.FileNotFound:
                _appendOutput(Strings.DataLoadMissingText);
                break;

            case AppDataLoadStatus.InvalidData:
                ShowDataLoadWarning(string.Format(Strings.DataLoadInvalidText, loadResult.Path));
                break;

            case AppDataLoadStatus.IoError:
                ShowDataLoadError(string.Format(Strings.DataLoadIoErrorText, loadResult.Path, loadResult.ErrorMessage));
                break;
        }
    }

    private void ShowDataLoadWarning(string message)
    {
        _appendOutput(message);
        _dialogService.ShowWarning(message, Strings.DataLoadMessageTitle);
    }

    private void ShowDataLoadError(string message)
    {
        _appendOutput(message);
        _dialogService.ShowError(message, Strings.DataLoadMessageTitle);
    }

    private string GetValidationWarning(AppEntryValidationError validation, string id, string? architecture = null)
    {
        var reference = BuildEntryReference(id, architecture);
        return validation switch
        {
            AppEntryValidationError.DuplicateId => string.Format(Strings.DuplicateIdText, reference),
            AppEntryValidationError.InvalidId => string.Format(Strings.InvalidIdText, reference),
            _ => string.Empty
        };
    }

    private static string BuildEntryReference(string id, string? architecture)
    {
        var normalizedArchitecture = (architecture ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalizedArchitecture)
            ? id
            : $"{id} [{normalizedArchitecture}]";
    }

    private void RefreshLocalizedState()
    {
        if (AvailableActions.Count >= 3)
        {
            AvailableActions[0].Label = Strings.Install;
            AvailableActions[1].Label = Strings.Uninstall;
            AvailableActions[2].Label = Strings.Pause;
        }

        foreach (var app in _tabs.Values.SelectMany(tab => tab))
        {
            app.RefreshLocalizedStatus(Strings);
        }

        RaiseShellStateChanged();
    }

    private void OnLocalizationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalizationService.Strings))
        {
            RefreshLocalizedState();
        }
    }

    private void RaiseShellStateChanged()
    {
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(CurrentPresetName));
        OnPropertyChanged(nameof(CurrentPresetAppCountText));
    }

    private void RaiseMainCommandCanExecute()
    {
        AddCommand.RaiseCanExecuteChanged();
        EditCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        NewTabCommand.RaiseCanExecuteChanged();
        RenameTabCommand.RaiseCanExecuteChanged();
        DeleteTabCommand.RaiseCanExecuteChanged();
        ImportPresetCommand.RaiseCanExecuteChanged();
        ExportPresetCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandCanExecute()
    {
        RaiseMainCommandCanExecute();
    }
}
