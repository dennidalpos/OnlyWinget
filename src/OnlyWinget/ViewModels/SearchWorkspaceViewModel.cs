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
using OnlyWinget.Models;
using OnlyWinget.Services;

namespace OnlyWinget.ViewModels;

public sealed class SearchWorkspaceViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    private readonly Action<string> _appendOutput;
    private readonly HashSet<SearchResult> _observedResults = new();
    private ObservableCollection<SearchResult> _results = new();
    private SearchResult? _selectedResult;
    private string _query = string.Empty;
    private string _pickId = string.Empty;
    private bool _isVisible;
    private bool _isEnabled = true;
    private bool _isInProgress;

    public SearchWorkspaceViewModel(LocalizationService localizationService, Action<string> appendOutput)
    {
        _localizationService = localizationService;
        _appendOutput = appendOutput;
        AttachResultsCollection(_results);
    }

    public ObservableCollection<SearchResult> Results
    {
        get => _results;
        set
        {
            if (ReferenceEquals(_results, value))
            {
                return;
            }

            DetachResultsCollection();
            if (SetProperty(ref _results, value))
            {
                AttachResultsCollection(value);
                RaiseSelectionStateChanged(logSelection: false);
                OnPropertyChanged(nameof(IsEmptyStateVisible));
            }
        }
    }

    public SearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                PickId = value?.Id ?? string.Empty;
            }
        }
    }

    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value);
    }

    public string PickId
    {
        get => _pickId;
        set => SetProperty(ref _pickId, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsInProgress
    {
        get => _isInProgress;
        set
        {
            if (SetProperty(ref _isInProgress, value))
            {
                OnPropertyChanged(nameof(IsEmptyStateVisible));
            }
        }
    }

    public bool IsEmptyStateVisible => Results.Count == 0 && !IsInProgress;

    public int SelectedCount => Results.Count(result => result.IsSelected);

    public string SelectedCountText => string.Format(_localizationService.Strings.SelectedCountText, SelectedCount, Results.Count);

    public string AddButtonText => SelectedCount > 1
        ? _localizationService.Strings.UseSelectedPackagesButton
        : _localizationService.Strings.UseIdButton;

    public bool? AreAllSearchResultsSelected
    {
        get => GetTriStateSelection(Results);
        set
        {
            if (value.HasValue)
            {
                SetAllSelected(value.Value);
            }
        }
    }

    public IReadOnlyList<SearchResult> SelectedResults()
    {
        return Results.Where(result => result.IsSelected).ToList();
    }

    public void Reset()
    {
        Results = new ObservableCollection<SearchResult>();
        Query = string.Empty;
        PickId = string.Empty;
        SelectedResult = null;
    }

    public bool CanUseSelectedOrManualId()
    {
        return SelectedCount > 0 || !string.IsNullOrWhiteSpace(PickId.Trim());
    }

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(AddButtonText));
    }

    private void AttachResultsCollection(ObservableCollection<SearchResult> results)
    {
        _results = results;
        _results.CollectionChanged += OnResultsCollectionChanged;
        SyncResultItems();
    }

    private void DetachResultsCollection()
    {
        _results.CollectionChanged -= OnResultsCollectionChanged;
        foreach (var result in _observedResults.ToList())
        {
            DetachResultItem(result);
        }
    }

    private void OnResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var result in e.OldItems.OfType<SearchResult>())
            {
                DetachResultItem(result);
            }
        }

        if (e.NewItems != null)
        {
            foreach (var result in e.NewItems.OfType<SearchResult>())
            {
                AttachResultItem(result);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SyncResultItems();
        }

        OnPropertyChanged(nameof(IsEmptyStateVisible));
        RaiseSelectionStateChanged(logSelection: false);
    }

    private void SyncResultItems()
    {
        var currentItems = new HashSet<SearchResult>(Results);
        foreach (var result in _observedResults.Where(result => !currentItems.Contains(result)).ToList())
        {
            DetachResultItem(result);
        }

        foreach (var result in currentItems)
        {
            AttachResultItem(result);
        }
    }

    private void AttachResultItem(SearchResult result)
    {
        if (_observedResults.Add(result))
        {
            result.PropertyChanged += OnResultPropertyChanged;
        }
    }

    private void DetachResultItem(SearchResult result)
    {
        if (_observedResults.Remove(result))
        {
            result.PropertyChanged -= OnResultPropertyChanged;
        }
    }

    private void OnResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchResult.IsSelected))
        {
            RaiseSelectionStateChanged(logSelection: true);
        }
    }

    private void SetAllSelected(bool isSelected)
    {
        foreach (var result in Results)
        {
            result.IsSelected = isSelected;
        }

        RaiseSelectionStateChanged(logSelection: true);
    }

    private void RaiseSelectionStateChanged(bool logSelection)
    {
        if (logSelection)
        {
            _appendOutput($"event=batch_selection_changed scope=search selected_count={SelectedCount} total_count={Results.Count}");
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(AddButtonText));
        OnPropertyChanged(nameof(AreAllSearchResultsSelected));
    }

    private static bool? GetTriStateSelection(IReadOnlyCollection<SearchResult> results)
    {
        if (results.Count == 0)
        {
            return false;
        }

        var selectedCount = results.Count(result => result.IsSelected);
        if (selectedCount == 0)
        {
            return false;
        }

        return selectedCount == results.Count ? true : null;
    }
}
