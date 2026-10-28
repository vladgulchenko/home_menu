using System.ComponentModel;
using System.Windows;

namespace HomeMenu.Wpf;

public partial class EditorWindow : Window
{
    public EditorWindow() { InitializeComponent(); Loaded += (_, _) => ((EditorViewModel)DataContext).MarkClean(); }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not EditorViewModel vm) return;
        if (vm.Saved) return;
        if (vm.IsSaving) { e.Cancel = true; return; }
        if (vm.IsDirty && MessageBox.Show(this, "Закрыть редактор и потерять несохраненные изменения?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }
}
