using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Data;
using HomeMenu.Core;

namespace HomeMenu.Wpf;

public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is decimal amount ? Rules.Money(amount) : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true;
    }
}
public sealed class RelayCommand(Action<object?> execute, Func<bool>? can = null) : ICommand
{
    public bool CanExecute(object? parameter) => can?.Invoke() ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? can = null) : ICommand
{
    private bool running;
    public bool IsRunning => running;
    public bool CanExecute(object? parameter) => !running && (can?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return; running = true; CommandManager.InvalidateRequerySuggested();
        try { await execute(); } finally { running = false; CommandManager.InvalidateRequerySuggested(); }
    }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
