using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using HomeMenu.Infrastructure;

namespace HomeMenu.Wpf;

public partial class App : Application
{
    private InstanceLease? lease;
    private DatabaseRuntime? runtime;
    private readonly UserSession session = new();
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("ru-RU")));
        try
        {
            runtime = new DatabaseRuntime(DatabaseRuntime.DefaultPath); lease = new InstanceLease(runtime.Path);
            await runtime.InitializeAsync(); ShowLogin();
        }
        catch (Exception ex) { MessageBox.Show("Не удалось запустить HomeMenu.\n" + ex.Message, "HomeMenu", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    private void ShowLogin()
    {
        var service = new HomeMenuService(runtime!, session); var window = new AuthWindow();
        var vm = new AuthViewModel(service, () =>
        {
            var main = new MainWindow(); var dialogs = new DialogService(main);
            var model = new MainViewModel(service, session, runtime!, dialogs, () => { service.Logout(); ShowLogin(); main.Close(); });
            main.DataContext = model; MainWindow = main; main.Show(); window.Close();
            _ = model.InitializeAsync();
        });
        window.DataContext = vm; MainWindow = window; window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { lease?.Dispose(); base.OnExit(e); }
    public static void Log(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeMenuCourse");
            Directory.CreateDirectory(dir);
            // Exception types/stack only: no inputs, SQL parameters, credentials or profile values.
            File.AppendAllText(Path.Combine(dir, "errors.log"), $"{DateTime.UtcNow:O} {ex.GetType().FullName}\n{ex.StackTrace}\n");
        }
        catch { /* Logging failure must not hide the original error. */ }
    }
}
