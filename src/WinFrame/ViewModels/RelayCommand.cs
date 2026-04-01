using System;
using System.Windows.Input;

namespace WinFrame.ViewModels;

/// <summary>
/// A lightweight <see cref="ICommand"/> implementation that delegates execution and
/// can-execute logic to caller-supplied delegates.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> so that bound controls re-query
    /// <see cref="CanExecute"/>.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
