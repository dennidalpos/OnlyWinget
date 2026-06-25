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

public sealed class UpdatesWorkspaceViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    private readonly Action<string> _appendOutput;
    private readonly HashSet<UpdateEntry> _observedUpdates = new();
    private ObservableCollection<UpdateEntry> _updates = new();
    private bool _isVisible;
    private bool _areActionsEnabled = true;
    private bool _isLoading;

    public UpdatesWorkspaceViewModel(LocalizationService localizationService, Action<string> appendOutput)
    {
        _localizationService = localizationService;
        _appendOutput = appendOutput;
        AttachUpdatesCollection(_updates);
    }

    public ObservableCollection<UpdateEntry> Updates
    {
        get => _updates;
        set
        {
            if (ReferenceEquals(_updates, value))
            {
                return;
            }

            DetachUpdatesCollection();
            if (SetProperty(ref _updates, value))
            {
                AttachUpdatesCollection(value);
                RaiseSelectionStateChanged(logSelection: false);
                OnPropertyChanged(nameof(IsEmptyStateVisible));
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool AreActionsEnabled
    {
        get => _areActionsEnabled;
        set => SetProperty(ref _areActionsEnabled, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmptyStateVisible));
            }
        }
    }

    public bool IsEmptyStateVisible => Updates.Count == 0 && !IsLoading;

    public int SelectedCount => Updates.Count(update => update.IsSelected);

    public string SelectedCountText => string.Format(_localizationService.Strings.SelectedCountText, SelectedCount, Updates.Count);

    public bool? AreAllUpdatesSelected
    {
        get => GetTriStateSelection(Updates);
        set
        {
            if (value.HasValue)
            {
                SetAllSelected(value.Value);
            }
        }
    }

    public IReadOnlyList<UpdateEntry> SelectedUpdates()
    {
        return Updates.Where(update => update.IsSelected).ToList();
    }

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(SelectedCountText));
    }

    private void AttachUpdatesCollection(ObservableCollection<UpdateEntry> updates)
    {
        _updates = updates;
        _updates.CollectionChanged += OnUpdatesCollectionChanged;
        SyncUpdateItems();
    }

    private void DetachUpdatesCollection()
    {
        _updates.CollectionChanged -= OnUpdatesCollectionChanged;
        foreach (var update in _observedUpdates.ToList())
        {
            DetachUpdateItem(update);
        }
    }

    private void OnUpdatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var update in e.OldItems.OfType<UpdateEntry>())
            {
                DetachUpdateItem(update);
            }
        }

        if (e.NewItems != null)
        {
            foreach (var update in e.NewItems.OfType<UpdateEntry>())
            {
                AttachUpdateItem(update);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SyncUpdateItems();
        }

        OnPropertyChanged(nameof(IsEmptyStateVisible));
        RaiseSelectionStateChanged(logSelection: false);
    }

    private void SyncUpdateItems()
    {
        var currentItems = new HashSet<UpdateEntry>(Updates);
        foreach (var update in _observedUpdates.Where(update => !currentItems.Contains(update)).ToList())
        {
            DetachUpdateItem(update);
        }

        foreach (var update in currentItems)
        {
            AttachUpdateItem(update);
        }
    }

    private void AttachUpdateItem(UpdateEntry update)
    {
        if (_observedUpdates.Add(update))
        {
            update.PropertyChanged += OnUpdatePropertyChanged;
        }
    }

    private void DetachUpdateItem(UpdateEntry update)
    {
        if (_observedUpdates.Remove(update))
        {
            update.PropertyChanged -= OnUpdatePropertyChanged;
        }
    }

    private void OnUpdatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateEntry.IsSelected))
        {
            RaiseSelectionStateChanged(logSelection: true);
        }
    }

    private void SetAllSelected(bool isSelected)
    {
        foreach (var update in Updates)
        {
            update.IsSelected = isSelected;
        }

        RaiseSelectionStateChanged(logSelection: true);
    }

    private void RaiseSelectionStateChanged(bool logSelection)
    {
        if (logSelection)
        {
            _appendOutput($"event=batch_selection_changed scope=updates selected_count={SelectedCount} total_count={Updates.Count}");
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(AreAllUpdatesSelected));
    }

    private static bool? GetTriStateSelection(IReadOnlyCollection<UpdateEntry> updates)
    {
        if (updates.Count == 0)
        {
            return false;
        }

        var selectedCount = updates.Count(update => update.IsSelected);
        if (selectedCount == 0)
        {
            return false;
        }

        return selectedCount == updates.Count ? true : null;
    }
}
