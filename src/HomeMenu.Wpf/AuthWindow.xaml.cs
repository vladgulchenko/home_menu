using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HomeMenu.Wpf;

public partial class AuthWindow : Window
{
    public AuthWindow() => InitializeComponent();
    private void PasswordChanged(object sender, RoutedEventArgs e) { if (DataContext is AuthViewModel vm) vm.Password = ((PasswordBox)sender).Password; }
    private void RepeatChanged(object sender, RoutedEventArgs e) { if (DataContext is AuthViewModel vm) vm.Repeat = ((PasswordBox)sender).Password; }
    private void OnClosing(object? sender, CancelEventArgs e) { if (Application.Current.MainWindow == this) Application.Current.Shutdown(); }
}
