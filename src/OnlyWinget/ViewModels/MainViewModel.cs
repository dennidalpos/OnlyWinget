using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualBasic;
using OnlyWinget.Commands;
using OnlyWinget.Models;
using OnlyWinget.Services;

namespace OnlyWinget.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly WingetService _wingetService = new();
    private readonly AppDataService _dataService = new();
    private readonly LocalizationService _localizationService = new();
    private readonly Dictionary<string, ObservableCollection<AppEntry>> _tabs = new(StringComparer.OrdinalIgnoreCase);
    private string _selectedTabName = string.Empty;
    private ObservableCollection<AppEntry> _currentApps = new();
    private AppEntry? _selectedApp;
    private ObservableCollection<SearchResult> _searchResults = new();
    private SearchResult? _selectedSearchResult;
    private ObservableCollection<SearchResult> _selectedSearchResults = new();
    private string _searchQuery = string.Empty;
    private string _searchPickId = string.Empty;
    private bool _isSearchVisible;
    private bool _isSearchEnabled = true;
    private ObservableCollection<UpdateEntry> _updates = new();
    private bool _isUpdatesVisible;
    private bool _areUpdatesActionsEnabled = true;
    private string _outputText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isApplyEnabled = true;

    public MainViewModel()
    {
        Strings = _localizationService.GetStrings();
        AvailableActions = new ObservableCollection<string> { "Install", "Uninstall", "Pause" };
        TabNames = new ObservableCollection<string>();
        IsWingetAvailable = _wingetService.TestAvailable();

        AddCommand = new RelayCommand(AddApp, () => IsWingetAvailable);
        EditCommand = new RelayCommand(EditApp, () => SelectedApp != null);
        RemoveCommand = new RelayCommand(RemoveApp, () => SelectedApp != null);
        OpenSearchCommand = new RelayCommand(OpenSearch, () => IsWingetAvailable);
        CloseSearchCommand = new RelayCommand(CloseSearch);
        RunSearchCommand = new AsyncRelayCommand(RunSearchAsync, () => IsWingetAvailable && IsSearchEnabled);
        UseSearchIdCommand = new RelayCommand(UseSearchId, () => IsWingetAvailable);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => IsWingetAvailable && IsApplyEnabled);
        SaveCommand = new RelayCommand(SaveData, () => IsWingetAvailable);
        NewTabCommand = new RelayCommand(CreateTab, () => IsWingetAvailable);
        RenameTabCommand = new RelayCommand(RenameTab, () => IsWingetAvailable);
        DeleteTabCommand = new RelayCommand(DeleteTab, () => IsWingetAvailable);
        OpenUpdatesCommand = new AsyncRelayCommand(OpenUpdatesAsync, () => IsWingetAvailable);
        RefreshUpdatesCommand = new AsyncRelayCommand(RefreshUpdatesAsync, () => IsWingetAvailable && AreUpdatesActionsEnabled);
        ApplyUpdatesCommand = new AsyncRelayCommand(ApplyUpdatesAsync, () => IsWingetAvailable && AreUpdatesActionsEnabled);
        CloseUpdatesCommand = new RelayCommand(CloseUpdates, () => AreUpdatesActionsEnabled);
    }

    public LocalizedStrings Strings { get; }

    public bool IsWingetAvailable { get; }

    public ObservableCollection<string> TabNames { get; }

    public ObservableCollection<string> AvailableActions { get; }

    public string SelectedTabName
    {
        get => _selectedTabName;
        set
        {
            if (SetProperty(ref _selectedTabName, value))
            {
                UpdateCurrentTab(value);
            }
        }
    }

    public ObservableCollection<AppEntry> CurrentApps
    {
        get => _currentApps;
        private set => SetProperty(ref _currentApps, value);
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

    public ObservableCollection<SearchResult> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
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
        set => SetProperty(ref _searchQuery, value);
    }

    public string SearchPickId
    {
        get => _searchPickId;
        set => SetProperty(ref _searchPickId, value);
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
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

    public ObservableCollection<UpdateEntry> Updates
    {
        get => _updates;
        private set => SetProperty(ref _updates, value);
    }

    public bool IsUpdatesVisible
    {
        get => _isUpdatesVisible;
        set => SetProperty(ref _isUpdatesVisible, value);
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

    public string OutputText
    {
        get => _outputText;
        private set => SetProperty(ref _outputText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
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

    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand OpenSearchCommand { get; }
    public RelayCommand CloseSearchCommand { get; }
    public AsyncRelayCommand RunSearchCommand { get; }
    public RelayCommand UseSearchIdCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand NewTabCommand { get; }
    public RelayCommand RenameTabCommand { get; }
    public RelayCommand DeleteTabCommand { get; }
    public AsyncRelayCommand OpenUpdatesCommand { get; }
    public AsyncRelayCommand RefreshUpdatesCommand { get; }
    public AsyncRelayCommand ApplyUpdatesCommand { get; }
    public RelayCommand CloseUpdatesCommand { get; }

    public void Initialize()
    {
        var jsonPath = GetJsonPath();
        var (tabs, tabNames) = _dataService.Load(jsonPath);
        TabNames.Clear();
        _tabs.Clear();

        foreach (var tabName in tabNames)
        {
            TabNames.Add(tabName);
            _tabs[tabName] = new ObservableCollection<AppEntry>(tabs[tabName]);
        }

        if (TabNames.Count > 0)
        {
            SelectedTabName = TabNames[0];
        }

        AppendOutput("winget disponibile: OK");
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
        var name = Interaction.InputBox(Strings.InputNamePrompt, Strings.InputNameTitle, string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        while (true)
        {
            var id = Interaction.InputBox(Strings.InputIdPrompt, Strings.InputIdTitle, string.Empty);
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (CurrentApps.Any(app => string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(string.Format(Strings.DuplicateIdText, id), Strings.DuplicateIdTitle, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                continue;
            }

            if (!_wingetService.TestAppExists(id))
            {
                MessageBox.Show(string.Format(Strings.InvalidIdText, id), Strings.InvalidIdTitle, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                continue;
            }

            CurrentApps.Add(new AppEntry { Name = name, Id = id, Action = "Install", Status = string.Empty });
            break;
        }
    }

    private void EditApp()
    {
        if (SelectedApp == null)
        {
            return;
        }

        var newName = Interaction.InputBox(Strings.InputNamePrompt, Strings.Edit, SelectedApp.Name);
        if (!string.IsNullOrWhiteSpace(newName))
        {
            SelectedApp.Name = newName;
        }

        while (true)
        {
            var newId = Interaction.InputBox(Strings.InputIdPrompt, Strings.Edit, SelectedApp.Id);
            if (string.IsNullOrWhiteSpace(newId))
            {
                break;
            }

            if (!string.Equals(newId, SelectedApp.Id, StringComparison.OrdinalIgnoreCase)
                && CurrentApps.Any(app => string.Equals(app.Id, newId, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(string.Format(Strings.DuplicateIdText, newId), Strings.DuplicateIdTitle, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                continue;
            }

            if (!_wingetService.TestAppExists(newId))
            {
                MessageBox.Show(string.Format(Strings.InvalidIdText, newId), Strings.InvalidIdTitle, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                continue;
            }

            SelectedApp.Id = newId;
            break;
        }
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

    private void OpenSearch()
    {
        SearchResults = new ObservableCollection<SearchResult>();
        SearchQuery = string.Empty;
        SearchPickId = string.Empty;
        SelectedSearchResult = null;
        SelectedSearchResults.Clear();
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

        IsSearchEnabled = false;
        try
        {
            var results = await Task.Run(() => _wingetService.Search(query));
            SearchResults = new ObservableCollection<SearchResult>(results);
        }
        finally
        {
            IsSearchEnabled = true;
        }
    }

    private void UseSearchId()
    {
        var selectedResults = SelectedSearchResults.ToList();
        if (selectedResults.Count > 0)
        {
            var warnings = new List<string>();
            var addedAny = false;

            foreach (var result in selectedResults)
            {
                var resultId = result.Id.Trim();
                if (string.IsNullOrWhiteSpace(resultId))
                {
                    continue;
                }

                if (CurrentApps.Any(app => string.Equals(app.Id, resultId, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add(string.Format(Strings.DuplicateIdText, resultId));
                    continue;
                }

                if (!_wingetService.TestAppExists(resultId))
                {
                    warnings.Add(string.Format(Strings.InvalidIdText, resultId));
                    continue;
                }

                var resultName = string.IsNullOrWhiteSpace(result.Name) ? resultId : result.Name;
                CurrentApps.Add(new AppEntry { Name = resultName, Id = resultId, Action = "Install", Status = string.Empty });
                addedAny = true;
            }

            if (warnings.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, warnings), Strings.InvalidIdTitle, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (addedAny)
            {
                IsSearchVisible = false;
            }

            return;
        }

        var id = SearchPickId.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (CurrentApps.Any(app => string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(string.Format(Strings.DuplicateIdText, id), Strings.DuplicateIdTitle, MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!_wingetService.TestAppExists(id))
        {
            MessageBox.Show(string.Format(Strings.InvalidIdText, id), Strings.InvalidIdTitle, MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var name = SelectedSearchResult?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = id;
        }

        CurrentApps.Add(new AppEntry { Name = name, Id = id, Action = "Install", Status = string.Empty });
        IsSearchVisible = false;
    }

    private async Task OpenUpdatesAsync()
    {
        IsUpdatesVisible = true;
        await RefreshUpdatesAsync();
    }

    private void CloseUpdates()
    {
        IsUpdatesVisible = false;
    }

    private async Task RefreshUpdatesAsync()
    {
        AreUpdatesActionsEnabled = false;
        Updates = new ObservableCollection<UpdateEntry>();
        try
        {
            var results = await Task.Run(() => _wingetService.LoadUpdates());
            Updates = new ObservableCollection<UpdateEntry>(results);
        }
        finally
        {
            AreUpdatesActionsEnabled = true;
        }
    }

    private async Task ApplyUpdatesAsync()
    {
        var selected = Updates.Where(update => update.Selected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        AreUpdatesActionsEnabled = false;
        StatusText = Strings.RunningText;
        AppendOutput($"=== Avvio aggiornamenti ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

        foreach (var update in selected)
        {
            AppendOutput($"--- {update.Name} [{update.Id}] : upgrade ---");
            var result = await Task.Run(() => _wingetService.UpgradeApp(update.Id));
            AppendOutputNormalized(result.Output);
            if (result.ExitCode != 0)
            {
                AppendOutput(_wingetService.GetErrorMessage(result.ExitCode));
            }
        }

        AppendOutput($"=== Fine aggiornamenti ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
        StatusText = string.Empty;
        AreUpdatesActionsEnabled = true;
        await RefreshUpdatesAsync();
    }

    private async Task ApplyAsync()
    {
        if (CurrentApps.Count == 0)
        {
            return;
        }

        OutputText = string.Empty;
        StatusText = Strings.RunningText;
        IsApplyEnabled = false;
        var snapshot = CurrentApps.Select(app => new AppEntry
        {
            Name = app.Name,
            Id = app.Id,
            Action = app.Action
        }).ToList();

        AppendOutput($"=== Avvio operazioni ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

        foreach (var app in snapshot)
        {
            if (app.Action == "Pause")
            {
                SetAppStatus(app.Id, "Pausa");
                continue;
            }

            if (string.IsNullOrWhiteSpace(app.Id))
            {
                continue;
            }

            if (app.Action == "Install")
            {
                SetAppStatus(app.Id, "Upgrade...");
                AppendOutput($"--- {app.Name} [{app.Id}] : upgrade ---");
                var upgradeResult = await Task.Run(() => _wingetService.UpgradeApp(app.Id));
                AppendOutputNormalized(upgradeResult.Output);

                if (upgradeResult.ExitCode == 0)
                {
                    SetAppStatus(app.Id, "OK");
                    continue;
                }

                if (_wingetService.IsNoUpgradeNeeded(upgradeResult.ExitCode))
                {
                    SetAppStatus(app.Id, "Già aggiornata");
                    continue;
                }

                SetAppStatus(app.Id, "Install...");
                AppendOutput($"--- {app.Name} [{app.Id}] : install ---");
                var installResult = await Task.Run(() => _wingetService.InstallApp(app.Id));
                AppendOutputNormalized(installResult.Output);

                if (installResult.ExitCode == 0)
                {
                    SetAppStatus(app.Id, "OK");
                }
                else if (_wingetService.IsAlreadyInstalled(installResult.ExitCode))
                {
                    SetAppStatus(app.Id, "Già installata");
                }
                else
                {
                    SetAppStatus(app.Id, _wingetService.GetErrorMessage(installResult.ExitCode));
                }

                continue;
            }

            if (app.Action == "Uninstall")
            {
                SetAppStatus(app.Id, "Disinstalla...");
                AppendOutput($"--- {app.Name} [{app.Id}] : uninstall ---");
                var uninstallResult = await Task.Run(() => _wingetService.UninstallApp(app.Id));
                AppendOutputNormalized(uninstallResult.Output);

                if (uninstallResult.ExitCode == 0)
                {
                    SetAppStatus(app.Id, "OK");
                }
                else
                {
                    SetAppStatus(app.Id, _wingetService.GetErrorMessage(uninstallResult.ExitCode));
                }
            }
        }

        AppendOutput($"=== Fine operazioni ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
        StatusText = string.Empty;
        IsApplyEnabled = true;
    }

    private void SaveData()
    {
        var jsonPath = GetJsonPath();
        var tabs = new Dictionary<string, List<AppEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in _tabs)
        {
            tabs[tab.Key] = tab.Value.ToList();
        }

        var success = _dataService.Save(jsonPath, TabNames.ToList(), tabs);
        if (success)
        {
            MessageBox.Show(Strings.SaveSuccessText, Strings.SaveSuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CreateTab()
    {
        var name = Interaction.InputBox(Strings.TabNamePrompt, Strings.TabNameTitle, "Scheda");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_tabs.ContainsKey(name))
        {
            MessageBox.Show(Strings.TabExistsText, Strings.TabExistsTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var list = new ObservableCollection<AppEntry>();
        _tabs[name] = list;
        TabNames.Add(name);
        SelectedTabName = name;
    }

    private void RenameTab()
    {
        if (string.IsNullOrWhiteSpace(SelectedTabName))
        {
            return;
        }

        var newName = Interaction.InputBox(Strings.TabRenamePrompt, Strings.TabRenameTitle, SelectedTabName);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedTabName)
        {
            return;
        }

        if (_tabs.ContainsKey(newName))
        {
            MessageBox.Show(Strings.TabExistsText, Strings.TabExistsTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var list = _tabs[SelectedTabName];
        _tabs.Remove(SelectedTabName);
        _tabs[newName] = list;
        var index = TabNames.IndexOf(SelectedTabName);
        if (index >= 0)
        {
            TabNames[index] = newName;
        }

        SelectedTabName = newName;
    }

    private void DeleteTab()
    {
        if (_tabs.Count <= 1 || string.IsNullOrWhiteSpace(SelectedTabName))
        {
            MessageBox.Show(Strings.NoTabToDeleteText, Strings.NoTabToDeleteTitle, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var oldIndex = TabNames.IndexOf(SelectedTabName);
        _tabs.Remove(SelectedTabName);
        TabNames.Remove(SelectedTabName);

        if (TabNames.Count == 0)
        {
            return;
        }

        if (oldIndex >= TabNames.Count)
        {
            oldIndex = TabNames.Count - 1;
        }

        SelectedTabName = TabNames[Math.Max(oldIndex, 0)];
    }

    private void SetAppStatus(string id, string status)
    {
        var target = CurrentApps.FirstOrDefault(app => string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            target.Status = status;
        }
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!string.IsNullOrEmpty(OutputText))
        {
            OutputText += Environment.NewLine;
        }

        OutputText += text;
    }

    private void AppendOutputNormalized(string text)
    {
        var normalized = NormalizeLogOutput(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        AppendOutput(normalized);
    }

    private static string NormalizeLogOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var filtered = lines
            .Select(line => NormalizeEncoding(line).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !IsSpinnerLine(line))
            .Where(line => !IsProgressLine(line));

        return string.Join(Environment.NewLine, filtered);
    }

    private static bool IsSpinnerLine(string line)
    {
        return line.All(ch => ch == '/' || ch == '\\' || ch == '-' || ch == '|');
    }

    private static bool IsProgressLine(string line)
    {
        if (line.Contains("â–", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        var hasProgressChars = trimmed.Any(ch => ch == '█' || ch == '▒' || ch == '░' || ch == '▉' || ch == '▊' || ch == '▌');
        if (hasProgressChars)
        {
            return true;
        }

        return trimmed.Contains("MB /", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEncoding(string line)
    {
        return line
            .Replace("Ã¨", "è", StringComparison.Ordinal)
            .Replace("Ã©", "é", StringComparison.Ordinal)
            .Replace("Ã ", "à", StringComparison.Ordinal)
            .Replace("Ã¬", "ì", StringComparison.Ordinal)
            .Replace("Ã²", "ò", StringComparison.Ordinal)
            .Replace("Ã¹", "ù", StringComparison.Ordinal)
            .Replace("Ã‰", "É", StringComparison.Ordinal)
            .Replace("Ãˆ", "È", StringComparison.Ordinal)
            .Replace("Ã€", "À", StringComparison.Ordinal)
            .Replace("Ã’", "Ò", StringComparison.Ordinal)
            .Replace("Ã™", "Ù", StringComparison.Ordinal);
    }

    private static string GetJsonPath()
    {
        return System.IO.Path.Combine(AppContext.BaseDirectory, "AppsList.json");
    }

    private void RaiseCommandCanExecute()
    {
        EditCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
    }
}
