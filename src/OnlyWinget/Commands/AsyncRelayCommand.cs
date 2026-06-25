// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OnlyWinget.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;
    private Task? _executionTask;
    private Exception? _lastException;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public Task? ExecutionTask
    {
        get => _executionTask;
        private set
        {
            _executionTask = value;
            ExecutionTaskChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Exception? LastException
    {
        get => _lastException;
        private set
        {
            _lastException = value;
            LastExceptionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ExecutionTaskChanged;

    public event EventHandler? LastExceptionChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync(parameter).ConfigureAwait(false);
        }
        catch
        {
            // Exception details are exposed through LastException for callers and tests.
        }
    }

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        LastException = null;
        RaiseCanExecuteChanged();
        try
        {
            ExecutionTask = _execute();
            await ExecutionTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastException = ex;
            throw;
        }
        finally
        {
            _isExecuting = false;
            ExecutionTask = null;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
