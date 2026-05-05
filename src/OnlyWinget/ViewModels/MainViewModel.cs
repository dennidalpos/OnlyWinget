// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OnlyWinget.Commands;
using OnlyWinget.Models;
using OnlyWinget.Services;

namespace OnlyWinget.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private enum ShellStatusState
    {
        None,
        Running,
        WingetUpdating
    }

    private enum ProgressTextState
    {
        None,
        OperationStart,
        UpdatesStart,
        Custom
    }

    private readonly WingetService _wingetService;
    private readonly LocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly IOperationRunner _operationRunner;
    private readonly OperatingSystemInfo _operatingSystemInfo;
    private ObservableCollection<SearchResult> _searchResults = new();
    private SearchResult? _selectedSearchResult;
    private ObservableCollection<SearchResult> _selectedSearchResults = new();
    private string _searchQuery = string.Empty;
    private string _searchPickId = string.Empty;
    private bool _isSearchVisible;
    private bool _isSearchEnabled = true;
    private bool _isSearchInProgress;
    private ObservableCollection<UpdateEntry> _updates = new();
    private bool _isUpdatesVisible;
    private bool _areUpdatesActionsEnabled = true;
    private bool _isUpdatesLoading;
    private string _outputText = string.Empty;
    private string _statusText = string.Empty;
    private string _operationProgressText = string.Empty;
    private int _operationProgressValue;
    private bool _isOperationProgressVisible;
    private bool _areMainActionsEnabled = true;
    private bool _isApplyEnabled = true;
    private bool _isWingetUpdateInProgress;
    private UiLanguageOption? _selectedLanguage;
    private ShellStatusState _shellStatusState;
    private ProgressTextState _progressTextState;
    private ObservableCollection<UpdateEntry>? _observedUpdates;
    private ObservableCollection<SearchResult>? _observedSelectedSearchResults;
    private ObservableCollection<AppEntry>? _observedPresetApps;
    private readonly HashSet<AppEntry> _observedPresetAppItems = new();

    public MainViewModel(
        WingetService wingetService,
        AppDataService appDataService,
        LocalizationService localizationService,
        IDialogService dialogService,
        IAppEntryService appEntryService,
        ITabService tabService,
        IOperationRunner operationRunner,
        OperatingSystemInfo? operatingSystemInfo = null)
    {
        _wingetService = wingetService;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _operationRunner = operationRunner;
        _operatingSystemInfo = operatingSystemInfo ?? new OperatingSystemInfoService().Detect();

        _selectedLanguage = _localizationService.SelectedLanguage;
        IsWingetAvailable = _wingetService.TestAvailable();
        PresetWorkspace = new PresetWorkspaceViewModel(
            IsWingetAvailable,
            localizationService,
            appDataService,
            dialogService,
            appEntryService,
            tabService,
            AppendOutput);

        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        OpenSearchCommand = new RelayCommand(OpenSearch, () => IsWingetAvailable && AreMainActionsEnabled && !IsSearchVisible);
        CloseSearchCommand = new RelayCommand(CloseSearch, () => IsSearchVisible);
        RunSearchCommand = new AsyncRelayCommand(RunSearchAsync, () => IsWingetAvailable && IsSearchEnabled && !string.IsNullOrWhiteSpace(SearchQuery.Trim()));
        UseSearchIdCommand = new AsyncRelayCommand(UseSearchIdAsync, () => IsWingetAvailable && AreMainActionsEnabled && IsSearchVisible && CanUseSearchId());
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => IsWingetAvailable && IsApplyEnabled && HasEnabledPresetApps());
        OpenUpdatesCommand = new AsyncRelayCommand(OpenUpdatesAsync, () => IsWingetAvailable && AreMainActionsEnabled && !IsUpdatesVisible);
        RefreshUpdatesCommand = new AsyncRelayCommand(RefreshUpdatesAsync, () => IsWingetAvailable && AreUpdatesActionsEnabled);
        ApplyUpdatesCommand = new AsyncRelayCommand(ApplyUpdatesAsync, () => IsWingetAvailable && AreUpdatesActionsEnabled && IsUpdatesVisible && HasSelectedUpdates());
        CloseUpdatesCommand = new RelayCommand(CloseUpdates, () => AreUpdatesActionsEnabled && IsUpdatesVisible);

        PresetWorkspace.PropertyChanged += OnPresetWorkspacePropertyChanged;
        _localizationService.PropertyChanged += OnLocalizationServicePropertyChanged;
        AttachPresetAppsCollection(PresetWorkspace.CurrentApps);
        AttachSelectedSearchResultsCollection(_selectedSearchResults);
        AttachUpdatesCollection(_updates);
    }

    public LocalizedStrings Strings => _localizationService.Strings;

    public ObservableCollection<UiLanguageOption> AvailableLanguages => _localizationService.SupportedLanguages;

    public bool IsWingetAvailable { get; }

    /// <summary>
    /// True when OnlyWinget is running with administrator rights.
    /// Used by the title bar admin badge and by elevation decision logic.
    /// </summary>
    public bool IsRunningAsAdministrator { get; } = ProcessElevationService.IsRunningAsAdministrator;

    public string PermissionStatusBadgeText => IsRunningAsAdministrator
        ? Strings.AdministratorBadge
        : Strings.StandardUserBadge;

    public string OperatingSystemStatusBadgeText => _operatingSystemInfo.DisplayText;

    public string OperatingSystemStatusBadgeTooltip => string.Format(Strings.OperatingSystemBadgeTooltip, _operatingSystemInfo.DisplayText);

    public PresetWorkspaceViewModel PresetWorkspace { get; }

    public ObservableCollection<string> TabNames => PresetWorkspace.TabNames;

    public ObservableCollection<ActionOption> AvailableActions => PresetWorkspace.AvailableActions;

    public UiLanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            if (value != null && !string.Equals(_localizationService.CurrentLocale, value.Code, StringComparison.OrdinalIgnoreCase))
            {
                _localizationService.SetCurrentLocale(value.Code);
            }
        }
    }

    public string SelectedTabName => PresetWorkspace.SelectedTabName;

    public ObservableCollection<AppEntry> CurrentApps => PresetWorkspace.CurrentApps;

    public AppEntry? SelectedApp => PresetWorkspace.SelectedApp;

    public string HeaderStatusText => string.IsNullOrWhiteSpace(StatusText) ? Strings.ReadyStatusText : StatusText;

    public bool IsPresetWorkspaceVisible => !IsSearchVisible && !IsUpdatesVisible;

    public string CurrentWorkspaceTitle => IsSearchVisible
        ? Strings.SearchTitleBarText
        : IsUpdatesVisible
            ? Strings.UpdatesTitleBarText
            : Strings.WorkspacePresetTitle;

    public string CurrentWorkspaceDescription => IsSearchVisible
        ? Strings.SearchWorkspaceDescription
        : IsUpdatesVisible
            ? Strings.UpdatesWorkspaceDescription
            : Strings.WorkspacePresetDescription;

    public bool IsSearchWorkspaceButtonVisible => !IsSearchVisible;

    public bool IsUpdatesWorkspaceButtonVisible => !IsUpdatesVisible;

    public ObservableCollection<SearchResult> SearchResults
    {
        get => _searchResults;
        private set
        {
            if (SetProperty(ref _searchResults, value))
            {
                OnPropertyChanged(nameof(IsSearchEmptyStateVisible));
            }
        }
    }

    public SearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (SetProperty(ref _selectedSearchResult, value))
            {
                SearchPickId = value?.Id ?? string.Empty;
            }
        }
    }

    public ObservableCollection<SearchResult> SelectedSearchResults
    {
        get => _selectedSearchResults;
        private set => SetProperty(ref _selectedSearchResults, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RunSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchPickId
    {
        get => _searchPickId;
        set
        {
            if (SetProperty(ref _searchPickId, value))
            {
                UseSearchIdCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set
        {
            if (SetProperty(ref _isSearchVisible, value))
            {
                RaiseWorkspaceStateChanged();
                OpenSearchCommand.RaiseCanExecuteChanged();
                CloseSearchCommand.RaiseCanExecuteChanged();
                RunSearchCommand.RaiseCanExecuteChanged();
                UseSearchIdCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsSearchWorkspaceButtonVisible));
            }
        }
    }

    public bool IsSearchEnabled
    {
        get => _isSearchEnabled;
        set
        {
            if (SetProperty(ref _isSearchEnabled, value))
            {
                RunSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSearchInProgress
    {
        get => _isSearchInProgress;
        private set
        {
            if (SetProperty(ref _isSearchInProgress, value))
            {
                OnPropertyChanged(nameof(IsSearchEmptyStateVisible));
            }
        }
    }

    public bool IsSearchEmptyStateVisible => SearchResults.Count == 0 && !IsSearchInProgress;

    public string SearchAddButtonText => SelectedSearchResults.Count > 1
        ? Strings.UseSelectedPackagesButton
        : Strings.UseIdButton;

    public ObservableCollection<UpdateEntry> Updates
    {
        get => _updates;
        private set
        {
            if (SetProperty(ref _updates, value))
            {
                AttachUpdatesCollection(value);
                OnPropertyChanged(nameof(IsUpdatesEmptyStateVisible));
            }
        }
    }

    public bool IsUpdatesVisible
    {
        get => _isUpdatesVisible;
        set
        {
            if (SetProperty(ref _isUpdatesVisible, value))
            {
                RaiseWorkspaceStateChanged();
                OpenUpdatesCommand.RaiseCanExecuteChanged();
                ApplyUpdatesCommand.RaiseCanExecuteChanged();
                CloseUpdatesCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsUpdatesWorkspaceButtonVisible));
            }
        }
    }

    public bool AreUpdatesActionsEnabled
    {
        get => _areUpdatesActionsEnabled;
        set
        {
            if (SetProperty(ref _areUpdatesActionsEnabled, value))
            {
                RefreshUpdatesCommand.RaiseCanExecuteChanged();
                ApplyUpdatesCommand.RaiseCanExecuteChanged();
                CloseUpdatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUpdatesLoading
    {
        get => _isUpdatesLoading;
        private set
        {
            if (SetProperty(ref _isUpdatesLoading, value))
            {
                OnPropertyChanged(nameof(IsUpdatesEmptyStateVisible));
            }
        }
    }

    public bool IsUpdatesEmptyStateVisible => Updates.Count == 0 && !IsUpdatesLoading;

    public string OutputText
    {
        get => _outputText;
        private set => SetProperty(ref _outputText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
            {
                _shellStatusState = ShellStatusState.None;
                OnPropertyChanged(nameof(HeaderStatusText));
            }
        }
    }

    public string OperationProgressText
    {
        get => _operationProgressText;
        set
        {
            if (SetProperty(ref _operationProgressText, value))
            {
                _progressTextState = ProgressTextState.Custom;
            }
        }
    }

    public int OperationProgressValue
    {
        get => _operationProgressValue;
        set => SetProperty(ref _operationProgressValue, value);
    }

    public bool IsOperationProgressVisible
    {
        get => _isOperationProgressVisible;
        set => SetProperty(ref _isOperationProgressVisible, value);
    }

    public bool AreMainActionsEnabled
    {
        get => _areMainActionsEnabled;
        private set
        {
            if (SetProperty(ref _areMainActionsEnabled, value))
            {
                RaiseMainCommandCanExecute();
            }
        }
    }

    public bool IsApplyEnabled
    {
        get => _isApplyEnabled;
        private set
        {
            if (SetProperty(ref _isApplyEnabled, value))
            {
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsWingetUpdateInProgress
    {
        get => _isWingetUpdateInProgress;
        set => SetProperty(ref _isWingetUpdateInProgress, value);
    }

    public RelayCommand AddCommand => PresetWorkspace.AddCommand;
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand OpenSearchCommand { get; }
    public RelayCommand CloseSearchCommand { get; }
    public AsyncRelayCommand RunSearchCommand { get; }
    public AsyncRelayCommand UseSearchIdCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public RelayCommand SaveCommand => PresetWorkspace.SaveCommand;
    public RelayCommand NewTabCommand => PresetWorkspace.NewTabCommand;
    public RelayCommand RenameTabCommand => PresetWorkspace.RenameTabCommand;
    public RelayCommand DeleteTabCommand => PresetWorkspace.DeleteTabCommand;
    public RelayCommand ImportPresetCommand => PresetWorkspace.ImportPresetCommand;
    public RelayCommand ExportPresetCommand => PresetWorkspace.ExportPresetCommand;
    public AsyncRelayCommand OpenUpdatesCommand { get; }
    public AsyncRelayCommand RefreshUpdatesCommand { get; }
    public AsyncRelayCommand ApplyUpdatesCommand { get; }
    public RelayCommand CloseUpdatesCommand { get; }

    public void Initialize()
    {
        PresetWorkspace.Initialize();
        AppendOutput(Strings.WingetAvailableLogText);
        AppendOutput($"event=os_detected product=\"{EscapeLogValue(_operatingSystemInfo.ProductName)}\" version=\"{EscapeLogValue(_operatingSystemInfo.Version)}\" build=\"{EscapeLogValue(_operatingSystemInfo.Build)}\" os_arch=\"{EscapeLogValue(_operatingSystemInfo.NormalizedArchitecture)}\" process_arch=\"{EscapeLogValue(_operatingSystemInfo.ProcessArchitecture)}\" ui_culture=\"{EscapeLogValue(_operatingSystemInfo.UiCultureName)}\"");
        AppendOutput($"event=process_elevation elevated={IsRunningAsAdministrator}");
    }

    private void OpenLogFolder()
    {
        var logDir = _wingetService.LogDirectory;
        try
        {
            Directory.CreateDirectory(logDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, Strings.Title);
        }
    }

    public void AppendLog(string text)
    {
        AppendOutput(text);
    }

    private void OpenSearch()
    {
        SearchResults = new ObservableCollection<SearchResult>();
        SearchQuery = string.Empty;
        SearchPickId = string.Empty;
        SelectedSearchResult = null;
        SelectedSearchResults.Clear();
        IsUpdatesVisible = false;
        IsSearchVisible = true;
    }

    private void CloseSearch()
    {
        IsSearchVisible = false;
    }

    private async Task RunSearchAsync()
    {
        var query = SearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        await ExecuteSafelyAsync(
            async () =>
            {
                await RunBusyAsync(
                    SetSearchUiEnabled,
                    async () =>
                    {
                        SearchResults = new ObservableCollection<SearchResult>();
                        var results = await Task.Run(() => _wingetService.Search(query));
                        SearchResults = new ObservableCollection<SearchResult>(results);
                    });
            },
            Strings.SearchFailedText);
    }

    private async Task UseSearchIdAsync()
    {
        await ExecuteSafelyAsync(
            async () =>
            {
                var requests = BuildInterrogationRequests();
                if (requests.Count == 0)
                {
                    return;
                }

                var warnings = new List<string>();
                var addedAny = false;

                foreach (var request in requests)
                {
                    var dialogResult = await _dialogService.ShowPackageInterrogationAsync(request);
                    if (dialogResult == null)
                    {
                        break;
                    }

                    var queueSelections = dialogResult.QueueSelections.Count > 0
                        ? dialogResult.QueueSelections
                        : new[] { dialogResult.SelectedOptions };

                    if (!PresetWorkspace.TryAddEntries(dialogResult.Interrogation, queueSelections, out var warning, showDialog: false))
                    {
                        if (!string.IsNullOrWhiteSpace(warning))
                        {
                            warnings.Add(warning);
                        }

                        break;
                    }

                    foreach (var selection in queueSelections)
                    {
                        AppendOutput(
                            $"event=queue_item_created id=\"{dialogResult.Interrogation.Id}\" source=\"{dialogResult.Interrogation.Source}\" version=\"{dialogResult.Interrogation.Version}\" arch=\"{selection.Architecture}\"");
                    }
                    addedAny = true;
                }

                if (warnings.Count > 0)
                {
                    _dialogService.ShowWarning(string.Join(Environment.NewLine, warnings), Strings.InvalidIdTitle);
                }

                if (addedAny)
                {
                    IsSearchVisible = false;
                }
            },
            Strings.ApplyFailedText);
    }

    private async Task OpenUpdatesAsync()
    {
        IsSearchVisible = false;
        IsUpdatesVisible = true;
        await RefreshUpdatesAsync();
    }

    private void CloseUpdates()
    {
        IsUpdatesVisible = false;
    }

    private async Task RefreshUpdatesAsync()
    {
        await ExecuteSafelyAsync(
            async () =>
            {
                await RunBusyAsync(
                    SetUpdatesUiEnabled,
                    async () =>
                    {
                        Updates = new ObservableCollection<UpdateEntry>();
                        var results = await Task.Run(() => _wingetService.LoadUpdates());
                        ApplyPresetUpdateOptions(results);
                        Updates = new ObservableCollection<UpdateEntry>(results);
                    });
            },
            Strings.UpdatesFailedText);
    }

    private async Task ApplyUpdatesAsync()
    {
        var selected = Updates.Where(update => update.Selected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        SetShellStatus(ShellStatusState.Running);
        IsOperationProgressVisible = true;
        OperationProgressValue = 0;
        SetProgressTextState(ProgressTextState.UpdatesStart);
        var finalStatuses = new Dictionary<string, UiStatusState>(StringComparer.OrdinalIgnoreCase);
        var finalErrors = new Dictionary<string, (string ErrorMessage, string Resolution)>(StringComparer.OrdinalIgnoreCase);

        void TrackAndSetStatus(string id, UiStatusState status)
        {
            finalStatuses[id] = status;
            SetUpdateStatus(id, status);
        }

        void TrackAndSetError(string id, string errorMessage, string resolution)
        {
            if (string.IsNullOrEmpty(errorMessage))
                finalErrors.Remove(id);
            else
                finalErrors[id] = (errorMessage, resolution);
            SetUpdateError(id, errorMessage, resolution);
        }

        try
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    await RunBusyAsync(
                        enabled => AreUpdatesActionsEnabled = enabled,
                        async () =>
                        {
                            await _operationRunner.RunUpdatesAsync(selected, TrackAndSetStatus, AppendOutput, ReportOperationProgress, Strings, TrackAndSetError);
                            var refreshedUpdates = await Task.Run(() => _wingetService.LoadUpdates());
                            ApplyPresetUpdateOptions(refreshedUpdates);
                            var attemptedUpdates = selected
                                .GroupBy(update => update.Id, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                            foreach (var entry in refreshedUpdates)
                            {
                                if (attemptedUpdates.TryGetValue(entry.Id, out var attemptedUpdate))
                                {
                                    if (finalErrors.TryGetValue(entry.Id, out var attemptedError))
                                    {
                                        entry.Status = attemptedError.ErrorMessage;
                                        entry.ErrorMessage = attemptedError.ErrorMessage;
                                        entry.Resolution = attemptedError.Resolution;
                                        continue;
                                    }

                                    var stillAvailableStatus = FormatUpdateStillAvailableStatus(entry);
                                    entry.Status = stillAvailableStatus;
                                    entry.ErrorMessage = stillAvailableStatus;
                                    entry.Resolution = FormatUpdateStillAvailableResolution(attemptedUpdate, entry);
                                    AppendOutput(FormatUpdateStillAvailableLog(attemptedUpdate, entry));
                                    continue;
                                }

                                if (finalStatuses.TryGetValue(entry.Id, out var status))
                                    entry.ApplyStatus(status, Strings);
                                if (finalErrors.TryGetValue(entry.Id, out var err))
                                {
                                    entry.ErrorMessage = err.ErrorMessage;
                                    entry.Resolution = err.Resolution;
                                }
                            }
                            Updates = new ObservableCollection<UpdateEntry>(refreshedUpdates);
                        });
                },
                Strings.UpdatesFailedText);
        }
        finally
        {
            ClearShellStatus();
            ClearProgressText();
            OperationProgressValue = 0;
            IsOperationProgressVisible = false;
        }
    }

    private async Task ApplyAsync()
    {
        if (!HasEnabledPresetApps())
        {
            return;
        }

        OutputText = string.Empty;
        SetShellStatus(ShellStatusState.Running);
        IsOperationProgressVisible = true;
        OperationProgressValue = 0;
        SetProgressTextState(ProgressTextState.OperationStart);
        var snapshot = PresetWorkspace.CurrentApps
            .Where(app => app.Enabled)
            .Select(app => new AppEntry
            {
                Enabled = app.Enabled,
                Name = app.Name,
                Id = app.Id,
                Source = app.Source,
                Version = app.Version,
                Action = app.Action,
                Scope = app.Scope,
                InstallMode = app.InstallMode,
                Architecture = app.Architecture,
                Locale = app.Locale,
                InstallerType = app.InstallerType,
                InstallLocation = app.InstallLocation,
                LogPath = app.LogPath,
                AdditionalCustomArgs = app.AdditionalCustomArgs,
                OverrideArgs = app.OverrideArgs,
                ManifestFingerprint = app.ManifestFingerprint,
                InterrogatedAtUtc = app.InterrogatedAtUtc,
                ElevationRequirement = app.ElevationRequirement
            })
            .ToList();

        try
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    await RunBusyAsync(
                        SetApplyUiEnabled,
                        () => _operationRunner.RunApplyAsync(snapshot, SetAppStatus, AppendOutput, ReportOperationProgress, Strings, SetAppError));
                },
                Strings.ApplyFailedText);
        }
        finally
        {
            ClearShellStatus();
            ClearProgressText();
            OperationProgressValue = 0;
            IsOperationProgressVisible = false;
        }
    }

    private void SetAppStatus(string operationKey, UiStatusState status)
    {
        RunOnUiThread(() => PresetWorkspace.SetAppStatus(operationKey, status));
    }

    private void SetAppError(string operationKey, string errorMessage, string resolution)
    {
        RunOnUiThread(() => PresetWorkspace.SetAppError(operationKey, errorMessage, resolution));
    }

    private void SetUpdateError(string id, string errorMessage, string resolution)
    {
        RunOnUiThread(() =>
        {
            var target = Updates.FirstOrDefault(update => string.Equals(update.Id, id, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                target.ErrorMessage = errorMessage;
                target.Resolution = resolution;
            }
        });
    }

    private void SetUpdateStatus(string id, UiStatusState status)
    {
        RunOnUiThread(() =>
        {
            var target = Updates.FirstOrDefault(update => string.Equals(update.Id, id, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                target.ApplyStatus(status, Strings);
            }
        });
    }

    private void ReportOperationProgress(int percentage, string text)
    {
        RunOnUiThread(() =>
        {
            OperationProgressValue = Math.Max(0, Math.Min(100, percentage));
            _progressTextState = ProgressTextState.Custom;
            _operationProgressText = text;
            OnPropertyChanged(nameof(OperationProgressText));
        });
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (OutputText.Length == 0)
            {
                OutputText = text;
                return;
            }

            var lastChar = OutputText[^1];
            var firstChar = text[0];
            var needsSeparator = lastChar != '\n' && lastChar != '\r' && firstChar != '\n' && firstChar != '\r';
            OutputText += needsSeparator ? Environment.NewLine + text : text;
        });
    }

    private static async Task RunBusyAsync(Action<bool> setEnabled, Func<Task> operation)
    {
        setEnabled(false);
        try
        {
            await operation();
        }
        finally
        {
            setEnabled(true);
        }
    }

    private void SetApplyUiEnabled(bool enabled)
    {
        AreMainActionsEnabled = enabled;
        PresetWorkspace.AreActionsEnabled = enabled;
        IsApplyEnabled = enabled;
    }

    private void SetSearchUiEnabled(bool enabled)
    {
        IsSearchEnabled = enabled;
        IsSearchInProgress = !enabled;
    }

    private void SetUpdatesUiEnabled(bool enabled)
    {
        AreUpdatesActionsEnabled = enabled;
        IsUpdatesLoading = !enabled;
    }

    public void SetWingetUpdatingStatus()
    {
        SetShellStatus(ShellStatusState.WingetUpdating);
    }

    public void ClearShellStatus()
    {
        _shellStatusState = ShellStatusState.None;
        StatusText = string.Empty;
    }

    private void SetShellStatus(ShellStatusState state)
    {
        _shellStatusState = state;
        _statusText = state switch
        {
            ShellStatusState.Running => Strings.RunningText,
            ShellStatusState.WingetUpdating => Strings.WingetUpdatingText,
            _ => string.Empty
        };
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HeaderStatusText));
    }

    private void ClearProgressText()
    {
        _progressTextState = ProgressTextState.None;
        _operationProgressText = string.Empty;
        OnPropertyChanged(nameof(OperationProgressText));
    }

    private void SetProgressTextState(ProgressTextState state)
    {
        _progressTextState = state;
        _operationProgressText = state switch
        {
            ProgressTextState.OperationStart => Strings.OperationStartText,
            ProgressTextState.UpdatesStart => Strings.UpdatesStartText,
            _ => string.Empty
        };
        OnPropertyChanged(nameof(OperationProgressText));
    }

    private void RaiseWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(IsPresetWorkspaceVisible));
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(CurrentWorkspaceTitle));
        OnPropertyChanged(nameof(CurrentWorkspaceDescription));
        OnPropertyChanged(nameof(IsSearchWorkspaceButtonVisible));
        OnPropertyChanged(nameof(IsUpdatesWorkspaceButtonVisible));
    }

    private void RaiseMainCommandCanExecute()
    {
        OpenSearchCommand.RaiseCanExecuteChanged();
        UseSearchIdCommand.RaiseCanExecuteChanged();
        OpenUpdatesCommand.RaiseCanExecuteChanged();
        CloseSearchCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private bool CanUseSearchId()
    {
        return SelectedSearchResults.Count > 0 || !string.IsNullOrWhiteSpace(SearchPickId.Trim());
    }

    private List<PackageInterrogationRequest> BuildInterrogationRequests()
    {
        var selectedResults = SelectedSearchResults.ToList();
        if (selectedResults.Count > 0)
        {
            return selectedResults
                .Select(result => new PackageInterrogationRequest
                {
                    PackageId = result.Id,
                    PackageName = result.Name,
                    Version = result.Version,
                    Source = result.Source,
                    Log = AppendOutput
                })
                .ToList();
        }

        var normalizedId = SearchPickId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return new List<PackageInterrogationRequest>();
        }

        return new List<PackageInterrogationRequest>
        {
            new()
            {
                PackageId = normalizedId,
                PackageName = SelectedSearchResult?.Name ?? string.Empty,
                Version = SelectedSearchResult?.Version ?? string.Empty,
                Source = SelectedSearchResult?.Source ?? "winget",
                Log = AppendOutput
            }
        };
    }

    private bool HasSelectedUpdates()
    {
        return Updates.Any(update => update.Selected);
    }

    private bool HasEnabledPresetApps()
    {
        return PresetWorkspace.CurrentApps.Any(app => app.Enabled);
    }

    private void ApplyPresetUpdateOptions(IEnumerable<UpdateEntry> updates)
    {
        var configuredApps = PresetWorkspace.CurrentApps
            .Where(app => app.Enabled && !string.IsNullOrWhiteSpace(app.Id))
            .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var update in updates)
        {
            if (!configuredApps.TryGetValue(update.Id, out var candidates))
            {
                continue;
            }

            var configured = candidates.FirstOrDefault(app => string.Equals(app.Source, update.Source, StringComparison.OrdinalIgnoreCase))
                ?? candidates[0];
            update.Scope = configured.Scope;
            update.Architecture = configured.Architecture;
            update.Locale = configured.Locale;
            update.InstallerType = configured.InstallerType;
        }
    }

    private string FormatUpdateStillAvailableStatus(UpdateEntry update)
    {
        return Strings.LocaleCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? "Update still available"
            : "Aggiornamento ancora disponibile";
    }

    private string FormatUpdateStillAvailableResolution(UpdateEntry attemptedUpdate, UpdateEntry refreshedUpdate)
    {
        var currentVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Version)
            ? attemptedUpdate.Version
            : refreshedUpdate.Version;
        var availableVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Available)
            ? attemptedUpdate.Available
            : refreshedUpdate.Available;

        return Strings.LocaleCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? $"winget still reports {currentVersion} -> {availableVersion}. Open the operation log folder for installer details."
            : $"winget segnala ancora {currentVersion} -> {availableVersion}. Apri la cartella log per i dettagli dell'installer.";
    }

    private static string FormatUpdateStillAvailableLog(UpdateEntry attemptedUpdate, UpdateEntry refreshedUpdate)
    {
        var currentVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Version)
            ? attemptedUpdate.Version
            : refreshedUpdate.Version;
        var availableVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Available)
            ? attemptedUpdate.Available
            : refreshedUpdate.Available;
        var source = string.IsNullOrWhiteSpace(refreshedUpdate.Source)
            ? attemptedUpdate.Source
            : refreshedUpdate.Source;

        return $"event=update_still_available id=\"{EscapeLogValue(refreshedUpdate.Id)}\" name=\"{EscapeLogValue(refreshedUpdate.Name)}\" version=\"{EscapeLogValue(currentVersion)}\" available=\"{EscapeLogValue(availableVersion)}\" source=\"{EscapeLogValue(source)}\"";
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void OnPresetWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PresetWorkspaceViewModel.CurrentApps))
        {
            return;
        }

        AttachPresetAppsCollection(PresetWorkspace.CurrentApps);
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private void AttachPresetAppsCollection(ObservableCollection<AppEntry> apps)
    {
        if (ReferenceEquals(_observedPresetApps, apps))
        {
            return;
        }

        if (_observedPresetApps != null)
        {
            _observedPresetApps.CollectionChanged -= OnPresetAppsCollectionChanged;
            DetachPresetAppItems();
        }

        _observedPresetApps = apps;
        _observedPresetApps.CollectionChanged += OnPresetAppsCollectionChanged;
        SyncPresetAppItems();
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private void OnPresetAppsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (AppEntry app in e.OldItems)
            {
                DetachPresetAppItem(app);
            }
        }

        if (e.NewItems != null)
        {
            foreach (AppEntry app in e.NewItems)
            {
                AttachPresetAppItem(app);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SyncPresetAppItems();
        }

        ApplyCommand.RaiseCanExecuteChanged();
    }

    private void SyncPresetAppItems()
    {
        var currentItems = _observedPresetApps == null
            ? new HashSet<AppEntry>()
            : new HashSet<AppEntry>(_observedPresetApps);

        foreach (var app in _observedPresetAppItems.Where(app => !currentItems.Contains(app)).ToList())
        {
            DetachPresetAppItem(app);
        }

        foreach (var app in currentItems)
        {
            AttachPresetAppItem(app);
        }
    }

    private void AttachPresetAppItem(AppEntry app)
    {
        if (_observedPresetAppItems.Add(app))
        {
            app.PropertyChanged += OnPresetAppPropertyChanged;
        }
    }

    private void DetachPresetAppItem(AppEntry app)
    {
        if (_observedPresetAppItems.Remove(app))
        {
            app.PropertyChanged -= OnPresetAppPropertyChanged;
        }
    }

    private void DetachPresetAppItems()
    {
        foreach (var app in _observedPresetAppItems.ToList())
        {
            DetachPresetAppItem(app);
        }
    }

    private void OnPresetAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppEntry.Enabled))
        {
            ApplyCommand.RaiseCanExecuteChanged();
        }
    }

    private void AttachSelectedSearchResultsCollection(ObservableCollection<SearchResult> selectedResults)
    {
        if (ReferenceEquals(_observedSelectedSearchResults, selectedResults))
        {
            return;
        }

        if (_observedSelectedSearchResults != null)
        {
            _observedSelectedSearchResults.CollectionChanged -= OnSelectedSearchResultsCollectionChanged;
        }

        _observedSelectedSearchResults = selectedResults;
        _observedSelectedSearchResults.CollectionChanged += OnSelectedSearchResultsCollectionChanged;
        UseSearchIdCommand.RaiseCanExecuteChanged();
    }

    private void OnSelectedSearchResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UseSearchIdCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SearchAddButtonText));
    }

    private void AttachUpdatesCollection(ObservableCollection<UpdateEntry> updates)
    {
        if (ReferenceEquals(_observedUpdates, updates))
        {
            return;
        }

        if (_observedUpdates != null)
        {
            _observedUpdates.CollectionChanged -= OnUpdatesCollectionChanged;
            foreach (var entry in _observedUpdates)
            {
                entry.PropertyChanged -= OnUpdateEntryPropertyChanged;
            }
        }

        _observedUpdates = updates;
        _observedUpdates.CollectionChanged += OnUpdatesCollectionChanged;
        foreach (var entry in _observedUpdates)
        {
            entry.PropertyChanged += OnUpdateEntryPropertyChanged;
        }

        ApplyUpdatesCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsUpdatesEmptyStateVisible));
    }

    private void OnUpdatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<UpdateEntry>())
            {
                item.PropertyChanged -= OnUpdateEntryPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<UpdateEntry>())
            {
                item.PropertyChanged += OnUpdateEntryPropertyChanged;
            }
        }

        ApplyUpdatesCommand.RaiseCanExecuteChanged();
    }

    private void OnUpdateEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateEntry.Selected))
        {
            ApplyUpdatesCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnLocalizationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocalizationService.Strings))
        {
            return;
        }

        _selectedLanguage = _localizationService.SelectedLanguage;
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(Strings));
        OnPropertyChanged(nameof(AvailableLanguages));
        OnPropertyChanged(nameof(HeaderStatusText));
        OnPropertyChanged(nameof(PermissionStatusBadgeText));
        OnPropertyChanged(nameof(OperatingSystemStatusBadgeTooltip));
        OnPropertyChanged(nameof(CurrentWorkspaceTitle));
        OnPropertyChanged(nameof(CurrentWorkspaceDescription));
        OnPropertyChanged(nameof(SearchAddButtonText));

        foreach (var update in Updates)
        {
            update.RefreshLocalizedStatus(Strings);
        }

        switch (_shellStatusState)
        {
            case ShellStatusState.Running:
            case ShellStatusState.WingetUpdating:
                SetShellStatus(_shellStatusState);
                break;
        }

        switch (_progressTextState)
        {
            case ProgressTextState.OperationStart:
            case ProgressTextState.UpdatesStart:
                SetProgressTextState(_progressTextState);
                break;
        }
    }

    private async Task ExecuteSafelyAsync(Func<Task> operation, string errorTemplate)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            AppendOutput($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.Message}");
            _dialogService.ShowError(string.Format(errorTemplate, ex.Message), Strings.Title);
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
