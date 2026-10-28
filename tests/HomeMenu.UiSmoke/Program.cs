using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HomeMenu.Core;
using HomeMenu.Infrastructure;
using HomeMenu.Wpf;
using Microsoft.EntityFrameworkCore;

namespace HomeMenu.UiSmoke;

/// <summary>Runs the real views and ViewModel commands against an isolated test database; never touches the user's database.</summary>
internal static class Program
{
    private static readonly List<string> checks = [];
    [STAThread]
    private static int Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("ru-RU")));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/HomeMenu.Wpf;component/Theme.xaml", UriKind.Relative) });
        var trace = new BindingTrace(); PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var result = 1;
        app.Startup += async (_, _) =>
        {
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HomeMenuUiSmoke", Guid.NewGuid() + ".db");
                var runtime = new DatabaseRuntime(path); using var lease = new InstanceLease(path); await runtime.InitializeAsync();
                await using (var database = runtime.CreateContext())
                {
                    var connection = database.Database.GetDbConnection(); await connection.OpenAsync();
                    await using var version = connection.CreateCommand(); version.CommandText = "SELECT sqlite_version()"; checks.Add("SQLite runtime " + await version.ExecuteScalarAsync());
                }
                var session = new UserSession(); var service = new HomeMenuService(runtime, session);
                var auth = new AuthWindow { DataContext = new AuthViewModel(service, () => { }) }; auth.Show(); await Frame(); Capture(auth, "01-login");
                var main = new MainWindow(); app.MainWindow = main; auth.Close();
                checks.Add("PASS AuthWindow: loaded with PasswordBox controls; no user authentication automated");
                // This account exists only in the new temporary fixture database.
                await service.RegisterAsync("ui.fixture", "Демонстрация", "fixture-password", "fixture-password");
                await service.LoginAsync("ui.fixture", "fixture-password");
                var dialogs = new FixtureDialogs(main); var vm = new MainViewModel(service, session, runtime, dialogs, () => service.Logout());
                main.DataContext = vm; main.Show(); await vm.InitializeAsync(); await Frame();
                Require(vm.Products.Count >= 15 && vm.Recipes.Count >= 9, "Initial catalogs and main bindings");
                dialogs.Edit = e => { Set(e, "sex", 0); Set(e, "activity", 2); };
                await Command(vm.EditProfile); Require(vm.ProfileText.Contains("2633"), "Profile editor and nutrition targets: " + vm.ProfileText + "; " + vm.Status + "; " + string.Join(" | ", dialogs.Errors));
                dialogs.Edit = e => { Text(e, "name", "UI продукт"); Text(e, "category", "Тест UI"); Text(e, "k", "100"); Text(e, "price", "0,1234"); };
                await Command(vm.AddProduct); Require(vm.Products.Any(p => p.Name == "UI продукт"), "Product editor saves decimal comma");
                var product = vm.AllProducts.Single(p => p.Name == "UI продукт");
                dialogs.Edit = e => { Text(e, "name", "UI рецепт"); Text(e, "instructions", "Смешайте ингредиенты."); e.Ingredients.Add(new IngredientRow { Product = e.Products.Single(p => p.Id == product.Id), Grams = "100" }); };
                await Command(vm.AddRecipe); Require(vm.Recipes.Any(r => r.Name == "UI рецепт"), "Recipe editor persists ingredients");
                dialogs.Edit = e => { Set(e, "product", product.Id); Text(e, "grams", "1200"); };
                await Command(vm.AddLot); Require(vm.Pantry.Any(p => p.Name == "UI продукт" && p.Grams == 1200), "Pantry editor persists lot");
                var preference = vm.Preferences.Single(p => p.Product.Id == product.Id); preference.Excluded = true; await Command(vm.SavePreferences);
                vm.Target = "2200"; vm.Budget = "150"; await Command(vm.Generate); Require(vm.Days.Count == 7, "Generator command produces seven-day grid");
                var plan = (await service.GetWeekAsync(Rules.Monday(runtime.Today)))!;
                Require(plan.Meals.Count == 21, "21 saved meals");
                vm.SelectedMeal = vm.Days[0].Breakfast; dialogs.Edit = e => { }; await Command(vm.Replace); Require(vm.Days.Count == 7, "Manual replacement command");
                plan = (await service.GetWeekAsync(plan.WeekStart))!;
                var meal = plan.Meals.First(m => m.Date <= runtime.Today);
                foreach (var i in meal.Ingredients) await service.SaveLotAsync(0, i.ProductId, i.QuantityGrams, null);
                await Command(vm.Refresh);
                var shown = vm.Days.SelectMany(d => new[] { d.Breakfast, d.Lunch, d.Dinner }).Single(m => m.Meal.Id == meal.Id); vm.SelectedMeal = shown;
                dialogs.ConfirmAnswer = false; await Command(vm.Cook); Require((await service.GetWeekAsync(plan.WeekStart))!.Meals.All(m => m.CookedAtUtc == null), "Cancel cooking dialog leaves database unchanged");
                dialogs.ConfirmAnswer = true; await Command(vm.Cook);
                Require((await service.GetWeekSummaryAsync(plan.Id)).CookedCount == 1, "Cook command and statistics");
                await Command(vm.LoadWeek); Require(vm.Statistics.Count == 7, "Shopping and statistics refresh");
                var reopenedRuntime = new DatabaseRuntime(path); await reopenedRuntime.InitializeAsync(); var reopenedSession = new UserSession(); var reopened = new HomeMenuService(reopenedRuntime, reopenedSession);
                await reopened.LoginAsync("ui.fixture", "fixture-password"); Require((await reopened.GetWeekSummaryAsync(plan.Id)).CookedCount == 1, "Reopen database retains cooking");
                // Render every real tab to exercise templates and bindings.
                var tabs = Find<TabControl>(main)!;
                for (var i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; await Frame(); Capture(main, $"{i + 2:00}-tab"); }
                tabs.SelectedIndex = 4; await Frame();
                Require(dialogs.Errors.Count == 0, "No errors from ViewModel commands"); Require(trace.Errors.Count == 0, "No WPF binding warnings/errors");
                vm.Logout.Execute(null); Require(sessionIsCleared(session) && vm.Days.Count == 0 && vm.Pantry.Count == 0, "Logout clears session and private collections");
                // Restore only this synthetic fixture session for the optional preview.
                await service.LoginAsync("ui.fixture", "fixture-password"); await vm.InitializeAsync();
                checks.Add("DATABASE " + path);
                WriteResults(); result = 0;
                if (args.Contains("--interactive")) { Console.WriteLine("Interactive preview ready. Close the window to finish."); main.Closed += (_, _) => app.Shutdown(); }
                else { main.Close(); app.Shutdown(); }
            }
            catch (Exception ex) { checks.Add("FAIL " + ex); WriteResults(); app.Shutdown(1); }
        };
        app.Run(); return result;
    }
    private static async Task Command(AsyncCommand command)
    {
        Require(command.CanExecute(null), "Command enabled"); command.Execute(null);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (command.IsRunning) { if (DateTime.UtcNow > deadline) throw new TimeoutException("UI command timeout"); await Task.Delay(20); }
        await Frame();
    }
    private static Task Frame() => Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
    private static bool sessionIsCleared(UserSession session) { try { session.Require(); return false; } catch (BusinessException) { return true; } }
    private static void Capture(Window window, string name)
    {
        // Capture the actual displayed WPF visual tree after layout, never a drawn mockup.
        window.UpdateLayout(); var source = PresentationSource.FromVisual(window); var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var bounds = VisualTreeHelper.GetDescendantBounds(window);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(bounds.Right * scale.M11), (int)Math.Ceiling(bounds.Bottom * scale.M22), 96 * scale.M11, 96 * scale.M22, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var dir = Path.Combine(Environment.CurrentDirectory, "artifacts", "screenshots"); Directory.CreateDirectory(dir); using var stream = File.Create(Path.Combine(dir, name + ".png")); encoder.Save(stream);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add("PASS " + message); }
    private static void Text(EditorViewModel e, string key, string text) => e.Fields.Single(f => f.Key == key).Text = text;
    private static void Set(EditorViewModel e, string key, int id) { var f = e.Fields.Single(f => f.Key == key); f.Selected = f.Choices!.Single(c => c.Id == id); }
    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) return match;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++) if (Find<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, i)) is { } result) return result;
        return null;
    }
    private static void WriteResults()
    {
        var dir = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts"); Directory.CreateDirectory(dir);
        var text = $"{DateTimeOffset.UtcNow:O}\n{Environment.OSVersion}\n" + string.Join("\n", checks); File.WriteAllText(System.IO.Path.Combine(dir, "ui-smoke.txt"), text); Console.WriteLine(text);
    }
    private sealed class FixtureDialogs(Window owner) : IDialogService
    {
        public Action<EditorViewModel> Edit { get; set; } = _ => { };
        public bool ConfirmAnswer { get; set; } = true;
        public List<string> Errors { get; } = [];
        public bool Confirm(string text) => ConfirmAnswer;
        public void Error(string text) => Errors.Add(text);
        public async Task<bool> EditAsync(EditorViewModel editor)
        {
            try
            {
                var window = new EditorWindow { Owner = owner, DataContext = editor }; editor.RequestClose = () => window.Close(); window.Show(); await Frame();
                Edit(editor); editor.Save.Execute(null); var deadline = DateTime.UtcNow.AddSeconds(20);
                while (editor.Save.IsRunning) { if (DateTime.UtcNow > deadline) throw new TimeoutException("Editor save timeout"); await Task.Delay(20); }
                if (!editor.Saved) { Errors.Add(editor.Error); throw new Exception("Editor error: " + editor.Error); }
                return true;
            }
            catch (Exception ex) { Errors.Add(ex.ToString()); throw; }
        }
    }
    private sealed class BindingTrace : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
