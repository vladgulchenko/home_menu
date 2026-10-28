using System.ComponentModel;
using System.Windows;

namespace HomeMenu.Wpf;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.Busy) { MessageBox.Show(this, "Дождитесь завершения операции или отмените подбор меню."); e.Cancel = true; return; }
        if (vm.PreferencesDirty() && MessageBox.Show(this, "Закрыть приложение и потерять несохраненные исключения?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes) e.Cancel = true;
    }
    private void OnClosed(object? sender, EventArgs e) { if (Application.Current.MainWindow == this) Application.Current.Shutdown(); }
}
