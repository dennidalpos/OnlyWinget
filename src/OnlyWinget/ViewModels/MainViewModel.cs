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
using System.Threading;
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

    private readonly WingetCommandService _wingetService;
    private readonly WingetQueryService _wingetQueryService;
    private readonly LocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly IOperationRunner _operationRunner;
    private readonly OperatingSystemInfo _operatingSystemInfo;
    private readonly OutputLogBuffer _outputLog = new();
    private string _outputText = string.Empty;
    private string _statusText = string.Empty;
    private string _operationProgressText = string.Empty;
    private int _operationProgressValue;
    private bool _isOperationProgressIndeterminate;
    private bool _isOperationProgressVisible;
    private bool _areMainActionsEnabled = true;
    private bool _isApplyEnabled = true;
    private bool _isWingetUpdateInProgress;
    private bool _isOperationCancellationAvailable;
    private CancellationTokenSource? _operationCancellation;
    private UiLanguageOption? _selectedLanguage;
    private ShellStatusState _shellStatusState;
    private ProgressTextState _progressTextState;
    private ObservableCollection<AppEntry>? _observedPresetApps;
    private readonly HashSet<AppEntry> _observedPresetAppItems = new();

    public MainViewModel(
        WingetCommandService wingetService,
        AppDataService appDataService,
        LocalizationService localizationService,
        IDialogService dialogService,
        IAppEntryService appEntryService,
        ITabService tabService,
        IOperationRunner operationRunner,
        WingetQueryService wingetQueryService,
        OperatingSystemInfo? operatingSystemInfo = null)
    {
        _wingetService = wingetService;
        _wingetQueryService = wingetQueryService ?? throw new ArgumentNullException(nameof(wingetQueryService));
        _localizationService = localizationService;
        _dialogService = dialogService;
        _operationRunner = operationRunner;
        _operatingSystemInfo = operatingSystemInfo ?? new OperatingSystemInfoService().Detect();

        _selectedLanguage = _localizationService.SelectedLanguage;
        IsWingetAvailable = _wingetService.TestAvailable();
        SearchWorkspace = new SearchWorkspaceViewModel(localizationService, AppendOutput);
        UpdatesWorkspace = new UpdatesWorkspaceViewModel(localizationService, AppendOutput);
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
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => IsWingetAvailable && IsApplyEnabled && HasRunnableSelectedPresetApps());
        OpenUpdatesCommand = new AsyncRelayCommand(OpenUpdatesAsync, () => IsWingetAvailable && AreMainActionsEnabled && !IsUpdatesVisible);
        RefreshUpdatesCommand = new AsyncRelayCommand(RefreshUpdatesAsync, () => IsWingetAvailable && AreUpdatesActionsEnabled);
        ApplyUpdatesCommand = new AsyncRelayCommand(ApplyUpdatesAsync, () => IsWingetAvailable && AreUpdatesActionsEnabled && IsUpdatesVisible && HasSelectedUpdates());
        CloseUpdatesCommand = new RelayCommand(CloseUpdates, () => AreUpdatesActionsEnabled && IsUpdatesVisible);
        CancelOperationCommand = new RelayCommand(CancelOperation, () => IsOperationCancellationAvailable);

        PresetWorkspace.PropertyChanged += OnPresetWorkspacePropertyChanged;
        SearchWorkspace.PropertyChanged += OnSearchWorkspacePropertyChanged;
        UpdatesWorkspace.PropertyChanged += OnUpdatesWorkspacePropertyChanged;
        _localizationService.PropertyChanged += OnLocalizationServicePropertyChanged;
        AttachPresetAppsCollection(PresetWorkspace.CurrentApps);
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

    public SearchWorkspaceViewModel SearchWorkspace { get; }

    public UpdatesWorkspaceViewModel UpdatesWorkspace { get; }

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

    public bool IsSearchWorkspaceButtonVisible => !IsSearchVisible && !IsUpdatesVisible;

    public bool IsUpdatesWorkspaceButtonVisible => !IsUpdatesVisible;

    public bool IsPresetToolbarActionVisible => !IsUpdatesVisible;

    public ObservableCollection<SearchResult> SearchResults
    {
        get => SearchWorkspace.Results;
        private set => SearchWorkspace.Results = value;
    }

    public SearchResult? SelectedSearchResult
    {
        get => SearchWorkspace.SelectedResult;
        set => SearchWorkspace.SelectedResult = value;
    }

    public string SearchQuery
    {
        get => SearchWorkspace.Query;
        set
        {
            var oldValue = SearchWorkspace.Query;
            SearchWorkspace.Query = value;
            if (!string.Equals(oldValue, SearchWorkspace.Query, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(SearchQuery));
                RunSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchPickId
    {
        get => SearchWorkspace.PickId;
        set
        {
            var oldValue = SearchWorkspace.PickId;
            SearchWorkspace.PickId = value;
            if (!string.Equals(oldValue, SearchWorkspace.PickId, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(SearchPickId));
                UseSearchIdCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSearchVisible
    {
        get => SearchWorkspace.IsVisible;
        set
        {
            var oldValue = SearchWorkspace.IsVisible;
            SearchWorkspace.IsVisible = value;
            if (oldValue != SearchWorkspace.IsVisible)
            {
                OnPropertyChanged(nameof(IsSearchVisible));
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
        get => SearchWorkspace.IsEnabled;
        set
        {
            var oldValue = SearchWorkspace.IsEnabled;
            SearchWorkspace.IsEnabled = value;
            if (oldValue != SearchWorkspace.IsEnabled)
            {
                OnPropertyChanged(nameof(IsSearchEnabled));
                RunSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSearchInProgress
    {
        get => SearchWorkspace.IsInProgress;
        private set => SearchWorkspace.IsInProgress = value;
    }

    public bool IsSearchEmptyStateVisible => SearchWorkspace.IsEmptyStateVisible;

    public string SearchAddButtonText => SearchWorkspace.AddButtonText;

    public int SelectedSearchResultCount => SearchWorkspace.SelectedCount;

    public bool? AreAllSearchResultsSelected
    {
        get => SearchWorkspace.AreAllSearchResultsSelected;
        set => SearchWorkspace.AreAllSearchResultsSelected = value;
    }

    public ObservableCollection<UpdateEntry> Updates
    {
        get => UpdatesWorkspace.Updates;
        private set => UpdatesWorkspace.Updates = value;
    }

    public bool IsUpdatesVisible
    {
        get => UpdatesWorkspace.IsVisible;
        set
        {
            var oldValue = UpdatesWorkspace.IsVisible;
            UpdatesWorkspace.IsVisible = value;
            if (oldValue != UpdatesWorkspace.IsVisible)
            {
                OnPropertyChanged(nameof(IsUpdatesVisible));
                RaiseWorkspaceStateChanged();
                OpenUpdatesCommand.RaiseCanExecuteChanged();
                ApplyUpdatesCommand.RaiseCanExecuteChanged();
                CloseUpdatesCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsUpdatesWorkspaceButtonVisible));
                OnPropertyChanged(nameof(IsPresetToolbarActionVisible));
            }
        }
    }

    public bool AreUpdatesActionsEnabled
    {
        get => UpdatesWorkspace.AreActionsEnabled;
        set
        {
            var oldValue = UpdatesWorkspace.AreActionsEnabled;
            UpdatesWorkspace.AreActionsEnabled = value;
            if (oldValue != UpdatesWorkspace.AreActionsEnabled)
            {
                OnPropertyChanged(nameof(AreUpdatesActionsEnabled));
                RefreshUpdatesCommand.RaiseCanExecuteChanged();
                ApplyUpdatesCommand.RaiseCanExecuteChanged();
                CloseUpdatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUpdatesLoading
    {
        get => UpdatesWorkspace.IsLoading;
        private set => UpdatesWorkspace.IsLoading = value;
    }

    public bool IsUpdatesEmptyStateVisible => UpdatesWorkspace.IsEmptyStateVisible;

    public int SelectedUpdateCount => UpdatesWorkspace.SelectedCount;

    public bool? AreAllUpdatesSelected
    {
        get => UpdatesWorkspace.AreAllUpdatesSelected;
        set => UpdatesWorkspace.AreAllUpdatesSelected = value;
    }

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

    public bool IsOperationProgressIndeterminate
    {
        get => _isOperationProgressIndeterminate;
        set => SetProperty(ref _isOperationProgressIndeterminate, value);
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

    public bool IsOperationCancellationAvailable
    {
        get => _isOperationCancellationAvailable;
        private set
        {
            if (SetProperty(ref _isOperationCancellationAvailable, value))
            {
                CancelOperationCommand.RaiseCanExecuteChanged();
            }
        }
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
    public RelayCommand CancelOperationCommand { get; }

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
        SearchWorkspace.Reset();
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

        using var cancellation = BeginCancellableOperation();
        try
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    await RunBusyAsync(
                        SetSearchUiEnabled,
                        async () =>
                        {
                            SearchResults = new ObservableCollection<SearchResult>();
                            var results = await _wingetQueryService.SearchAsync(query, cancellation.Token);
                            cancellation.Token.ThrowIfCancellationRequested();
                            SearchResults = new ObservableCollection<SearchResult>(results);
                        });
                },
                Strings.SearchFailedText);
        }
        finally
        {
            EndCancellableOperation(cancellation);
        }
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
        using var cancellation = BeginCancellableOperation();
        try
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    await RunBusyAsync(
                        SetUpdatesUiEnabled,
                        async () =>
                        {
                            Updates = new ObservableCollection<UpdateEntry>();
                            var results = await _wingetQueryService.LoadUpdatesAsync(cancellation.Token);
                            cancellation.Token.ThrowIfCancellationRequested();
                            UpdateWorkflow.ApplyPresetOptions(results, PresetWorkspace.CurrentApps);
                            Updates = new ObservableCollection<UpdateEntry>(results);
                        });
                },
                Strings.UpdatesFailedText);
        }
        finally
        {
            EndCancellableOperation(cancellation);
        }
    }

    private async Task ApplyUpdatesAsync()
    {
        var selected = UpdatesWorkspace.SelectedUpdates().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        SetShellStatus(ShellStatusState.Running);
        IsOperationProgressVisible = true;
        IsOperationProgressIndeterminate = false;
        OperationProgressValue = 0;
        SetProgressTextState(ProgressTextState.UpdatesStart);
        using var cancellation = BeginCancellableOperation();
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
                            await _operationRunner.RunUpdatesAsync(selected, TrackAndSetStatus, AppendOutput, ReportOperationProgress, Strings, TrackAndSetError, cancellation.Token);
                            if (cancellation.IsCancellationRequested)
                            {
                                return;
                            }

                            var refreshedUpdates = await _wingetQueryService.LoadUpdatesAsync(cancellation.Token);
                            UpdateWorkflow.ApplyPresetOptions(refreshedUpdates, PresetWorkspace.CurrentApps);
                            UpdateWorkflow.ApplyAttemptResults(
                                refreshedUpdates,
                                selected,
                                finalStatuses,
                                finalErrors,
                                Strings,
                                FormatUpdateStillAvailableStatus,
                                FormatUpdateStillAvailableResolution,
                                AppendOutput);
                            Updates = new ObservableCollection<UpdateEntry>(refreshedUpdates);
                        });
                },
                Strings.UpdatesFailedText);
        }
        finally
        {
            EndCancellableOperation(cancellation);
            ClearShellStatus();
            ClearProgressText();
            IsOperationProgressIndeterminate = false;
            OperationProgressValue = 0;
            IsOperationProgressVisible = false;
        }
    }

    private async Task ApplyAsync()
    {
        if (!HasRunnableSelectedPresetApps())
        {
            return;
        }

        ClearOutput();
        SetShellStatus(ShellStatusState.Running);
        IsOperationProgressVisible = true;
        IsOperationProgressIndeterminate = false;
        OperationProgressValue = 0;
        SetProgressTextState(ProgressTextState.OperationStart);
        using var cancellation = BeginCancellableOperation();
        var snapshot = CreateSelectedRunnableAppSnapshot(PresetWorkspace.CurrentApps);

        try
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    await RunBusyAsync(
                        SetApplyUiEnabled,
                        () => _operationRunner.RunApplyAsync(snapshot, SetAppStatus, AppendOutput, ReportOperationProgress, Strings, SetAppError, cancellation.Token));
                },
                Strings.ApplyFailedText);
        }
        finally
        {
            EndCancellableOperation(cancellation);
            ClearShellStatus();
            ClearProgressText();
            IsOperationProgressIndeterminate = false;
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
            var target = FindUpdateById(id);
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
            var target = FindUpdateById(id);
            if (target != null)
            {
                target.ApplyStatus(status, Strings);
            }
        });
    }

    private UpdateEntry? FindUpdateById(string id)
    {
        return Updates.FirstOrDefault(update => string.Equals(update.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<AppEntry> CreateSelectedRunnableAppSnapshot(IEnumerable<AppEntry> apps)
    {
        return apps
            .Where(app => app.IsSelected && !string.Equals(app.Action, AppActions.Pause, StringComparison.Ordinal))
            .Select(CloneAppEntryForOperation)
            .ToList();
    }

    private static AppEntry CloneAppEntryForOperation(AppEntry app)
    {
        return new AppEntry
        {
            IsSelected = true,
            Name = app.Name,
            Id = app.Id,
            Source = app.Source,
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
            AdvancedArgumentsReviewed = app.AdvancedArgumentsReviewed,
            SupportsInstallLocation = app.SupportsInstallLocation,
            SupportsLog = app.SupportsLog,
            ElevationRequirement = app.ElevationRequirement
        };
    }

    private CancellationTokenSource BeginCancellableOperation()
    {
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        IsOperationCancellationAvailable = true;
        return cancellation;
    }

    private void EndCancellableOperation(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_operationCancellation, cancellation))
        {
            _operationCancellation = null;
            IsOperationCancellationAvailable = false;
        }
    }

    private void CancelOperation()
    {
        var cancellation = _operationCancellation;
        if (cancellation == null || cancellation.IsCancellationRequested)
        {
            return;
        }

        AppendOutput("event=operation_cancel_requested");
        cancellation.Cancel();
        IsOperationCancellationAvailable = false;
    }

    private void ReportOperationProgress(int percentage, string text)
    {
        RunOnUiThread(() =>
        {
            IsOperationProgressIndeterminate = percentage < 0;
            if (percentage >= 0)
            {
                OperationProgressValue = Math.Max(0, Math.Min(100, percentage));
            }

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
            _outputLog.AppendLines(text);
            OutputText = _outputLog.ToString();
        });
    }

    private void ClearOutput()
    {
        _outputLog.Clear();
        OutputText = string.Empty;
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
        OnPropertyChanged(nameof(IsPresetToolbarActionVisible));
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
        return SearchWorkspace.CanUseSelectedOrManualId();
    }

    private List<PackageInterrogationRequest> BuildInterrogationRequests()
    {
        var selectedResults = SearchWorkspace.SelectedResults();
        if (selectedResults.Count > 0)
        {
            return selectedResults
                .Select(result => new PackageInterrogationRequest
                {
                    PackageId = result.Id,
                    PackageName = result.Name,
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
                Source = AppEntry.NormalizeSource(SelectedSearchResult?.Source),
                Log = AppendOutput
            }
        };
    }

    private bool HasSelectedUpdates()
    {
        return UpdatesWorkspace.SelectedCount > 0;
    }

    private bool HasRunnableSelectedPresetApps()
    {
        return PresetWorkspace.CurrentApps.Any(app =>
            app.IsSelected && !string.Equals(app.Action, AppActions.Pause, StringComparison.Ordinal));
    }

    private string FormatUpdateStillAvailableStatus(UpdateEntry update)
    {
        return UpdateVerificationFormatter.FormatStillAvailableStatus(Strings.LocaleCode);
    }

    private string FormatUpdateStillAvailableResolution(UpdateEntry attemptedUpdate, UpdateEntry refreshedUpdate)
    {
        return UpdateVerificationFormatter.FormatStillAvailableResolution(Strings.LocaleCode, attemptedUpdate, refreshedUpdate);
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void OnPresetWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PresetWorkspaceViewModel.CurrentApps))
        {
            AttachPresetAppsCollection(PresetWorkspace.CurrentApps);
        }

        if (e.PropertyName == nameof(PresetWorkspaceViewModel.CurrentApps)
            || e.PropertyName == nameof(PresetWorkspaceViewModel.SelectedAppCount)
            || e.PropertyName == nameof(PresetWorkspaceViewModel.AreAllPresetRowsSelected))
        {
            ApplyCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IsApplyEnabled));
        }
    }

    private void OnSearchWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SearchWorkspaceViewModel.Results):
                OnPropertyChanged(nameof(SearchResults));
                OnPropertyChanged(nameof(IsSearchEmptyStateVisible));
                break;
            case nameof(SearchWorkspaceViewModel.SelectedResult):
                OnPropertyChanged(nameof(SelectedSearchResult));
                break;
            case nameof(SearchWorkspaceViewModel.Query):
                OnPropertyChanged(nameof(SearchQuery));
                RunSearchCommand.RaiseCanExecuteChanged();
                break;
            case nameof(SearchWorkspaceViewModel.PickId):
                OnPropertyChanged(nameof(SearchPickId));
                UseSearchIdCommand.RaiseCanExecuteChanged();
                break;
            case nameof(SearchWorkspaceViewModel.IsVisible):
                OnPropertyChanged(nameof(IsSearchVisible));
                break;
            case nameof(SearchWorkspaceViewModel.IsEnabled):
                OnPropertyChanged(nameof(IsSearchEnabled));
                RunSearchCommand.RaiseCanExecuteChanged();
                break;
            case nameof(SearchWorkspaceViewModel.IsInProgress):
                OnPropertyChanged(nameof(IsSearchInProgress));
                OnPropertyChanged(nameof(IsSearchEmptyStateVisible));
                break;
            case nameof(SearchWorkspaceViewModel.IsEmptyStateVisible):
                OnPropertyChanged(nameof(IsSearchEmptyStateVisible));
                break;
            case nameof(SearchWorkspaceViewModel.SelectedCount):
            case nameof(SearchWorkspaceViewModel.AddButtonText):
            case nameof(SearchWorkspaceViewModel.AreAllSearchResultsSelected):
                OnPropertyChanged(nameof(SelectedSearchResultCount));
                OnPropertyChanged(nameof(SearchAddButtonText));
                OnPropertyChanged(nameof(AreAllSearchResultsSelected));
                UseSearchIdCommand.RaiseCanExecuteChanged();
                break;
        }
    }

    private void OnUpdatesWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UpdatesWorkspaceViewModel.Updates):
                OnPropertyChanged(nameof(Updates));
                OnPropertyChanged(nameof(IsUpdatesEmptyStateVisible));
                break;
            case nameof(UpdatesWorkspaceViewModel.IsVisible):
                OnPropertyChanged(nameof(IsUpdatesVisible));
                break;
            case nameof(UpdatesWorkspaceViewModel.AreActionsEnabled):
                OnPropertyChanged(nameof(AreUpdatesActionsEnabled));
                RefreshUpdatesCommand.RaiseCanExecuteChanged();
                ApplyUpdatesCommand.RaiseCanExecuteChanged();
                CloseUpdatesCommand.RaiseCanExecuteChanged();
                break;
            case nameof(UpdatesWorkspaceViewModel.IsLoading):
                OnPropertyChanged(nameof(IsUpdatesLoading));
                OnPropertyChanged(nameof(IsUpdatesEmptyStateVisible));
                break;
            case nameof(UpdatesWorkspaceViewModel.IsEmptyStateVisible):
                OnPropertyChanged(nameof(IsUpdatesEmptyStateVisible));
                break;
            case nameof(UpdatesWorkspaceViewModel.SelectedCount):
            case nameof(UpdatesWorkspaceViewModel.AreAllUpdatesSelected):
                OnPropertyChanged(nameof(SelectedUpdateCount));
                OnPropertyChanged(nameof(AreAllUpdatesSelected));
                ApplyUpdatesCommand.RaiseCanExecuteChanged();
                break;
        }
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
        if (e.PropertyName == nameof(AppEntry.IsSelected) || e.PropertyName == nameof(AppEntry.Action))
        {
            ApplyCommand.RaiseCanExecuteChanged();
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
        SearchWorkspace.RefreshLocalizedState();
        UpdatesWorkspace.RefreshLocalizedState();

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
        catch (OperationCanceledException)
        {
            AppendOutput("event=operation_cancelled");
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
