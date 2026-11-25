using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using HomeMenu.Core;
using HomeMenu.Infrastructure;
using HomeMenu.Wpf;

namespace HomeMenu.UiSmoke;

internal static partial class Program
{
    private static readonly string ProbePassword = Environment.GetEnvironmentVariable("HOMEMENU_TEST_PASSWORD")
        ?? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    private static int Operational(string[] args)
    {
        if (args[0] == "--lock-child")
        {
            try { using var lease = new InstanceLease(args[1]); return 0; }
            catch (BusinessException) { return 2; }
        }
        if (args[0] is "--persist-write" or "--persist-read") return PersistApplication(args[1], args[0] == "--persist-write");
        if (args[0] == "--process-lock")
        {
            var path = Path.Combine(Path.GetTempPath(), "HomeMenuLockProbe", Guid.NewGuid() + ".db");
            using (var lease = new InstanceLease(path))
            {
                Require(Child("--lock-child", path.ToUpperInvariant()).Code == 2, "Second process rejects the same database lock");
                Require(Child("--lock-child", path + "-other").Code == 0, "Different database has an independent process lock");
            }
            Require(Child("--lock-child", path).Code == 0, "Released database lock can be acquired by another process");
            WriteProbe("process-lock"); return 0;
        }
        if (args[0] != "--restart") throw new ArgumentException("Unknown operational probe.");
        var root = Path.Combine(Path.GetTempPath(), "HomeMenuRestartProbe");
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            foreach (var phase in new[] { "--persist-write", "--persist-read" })
            {
                var child = Child(phase, folder);
                foreach (var line in child.Text.Split('\n').Where(l => l.StartsWith("PASS ", StringComparison.Ordinal))) checks.Add(line.TrimEnd());
                if (child.Code != 0) throw new Exception(child.Text);
            }
            WriteProbe("restart"); return 0;
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(folder)) != Path.GetFullPath(root)
                || !Guid.TryParseExact(Path.GetFileName(folder), "N", out _)) throw new IOException("Unsafe probe cleanup path.");
            Directory.Delete(folder, true);
        }
    }

    private static int PersistApplication(string folder, bool write)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "HomeMenuRestartProbe"));
        folder = Path.GetFullPath(folder);
        if (Path.GetDirectoryName(folder) != root || !Guid.TryParseExact(Path.GetFileName(folder), "N", out _)
            || Environment.GetEnvironmentVariable("HOMEMENU_TEST_PASSWORD") is null) throw new ArgumentException("Run through --restart.");
        var path = Path.Combine(folder, "restart.db"); Environment.SetEnvironmentVariable("HOMEMENU_DATABASE_PATH", path);
        var app = new HomeMenu.Wpf.App(); app.InitializeComponent();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        var deadline = DateTime.UtcNow.AddSeconds(20);
        timer.Tick += async (_, _) =>
        {
            if (app.MainWindow is AuthWindow { DataContext: AuthViewModel auth })
            {
                timer.Stop();
                try
                {
                    Require(File.Exists(path), write ? "First application process initializes SQLite" : "Second application process opens the saved database");
                    auth.Login = "restart.user"; auth.Name = "Проверочный пользователь"; auth.Password = ProbePassword; auth.Repeat = ProbePassword;
                    if (write) auth.Toggle.Execute(null);
                    auth.Submit.Execute(null);
                    await Until(() => app.MainWindow is MainWindow { DataContext: MainViewModel m } && m.Idle && m.Products.Count >= 15);
                    var vm = (MainViewModel)app.MainWindow.DataContext;
                    Require(vm.UserName == "Проверочный пользователь", "Production login route restores the user session");
                    var runtime = new DatabaseRuntime(path); var session = new UserSession(); var service = new HomeMenuService(runtime, session);
                    await service.LoginAsync("restart.user", ProbePassword);
                    var week = Rules.Monday(runtime.Today);
                    if (write)
                    {
                        await service.SaveProfileAsync(new(Sex.Male, 20, 175, 70, ActivityLevel.Moderate, 150));
                        var created = await service.GenerateWeekAsync(week, 2200, 150, null, false);
                        var meal = created.Meals.OrderBy(m => m.Date).ThenBy(m => m.MealType).First();
                        foreach (var item in meal.Ingredients) await service.SaveLotAsync(0, item.ProductId, item.QuantityGrams + 100, null);
                        await service.CookMealAsync(created.Id, meal.Id, created.Version);
                        await Command(vm.Refresh);
                    }
                    var plan = await service.GetWeekAsync(week) ?? throw new Exception("Saved plan was lost.");
                    Require(plan.Meals.Count == 21 && plan.Meals.Count(m => m.CookedAtUtc != null) == 1 && plan.Version == 2,
                        write ? "First process saves 21 meals and one cooking with version 2" : "Second process restores 21 meals and exactly one cooking");
                    var stocks = await service.GetPantryAsync();
                    Require(stocks.Count > 0 && stocks.All(s => s.QuantityGrams == 100), "Pantry retains the exact remaining grams without repeated spending");
                    Require(vm.Days.Count == 7 && vm.Statistics.Count == 7, "Production main window displays the saved seven-day menu and statistics");
                    if (!write)
                    {
                        var previous = app.MainWindow;
                        vm.Logout.Execute(null);
                        await Until(() => app.MainWindow is AuthWindow);
                        Require(!previous.IsVisible && app.MainWindow is AuthWindow, "Logout switches to login without terminating the application");
                        Require(vm.Days.Count == 0 && vm.Pantry.Count == 0 && vm.Shopping.Count == 0, "Logout clears private menu, pantry and shopping collections");
                    }
                    app.MainWindow.Close();
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); checks.Add("FAIL " + ex); app.Shutdown(1); }
            }
            else if (DateTime.UtcNow > deadline) { timer.Stop(); checks.Add("FAIL Startup timeout"); app.Shutdown(1); }
        };
        timer.Start(); var result = app.Run();
        Console.WriteLine(string.Join("\n", checks));
        return checks.Any(x => x.StartsWith("FAIL", StringComparison.Ordinal)) ? 1 : result;
    }

    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!ready()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("Application did not reach expected state."); await Task.Delay(25); }
    }

    private static (int Code, string Text) Child(params string[] args)
    {
        var executable = Environment.ProcessPath ?? throw new Exception("Process path unavailable.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var argument in args) info.ArgumentList.Add(argument);
        info.Environment["HOMEMENU_TEST_PASSWORD"] = ProbePassword;
        using var child = Process.Start(info) ?? throw new Exception("Test child process did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(45000)) { child.Kill(); child.WaitForExit(); throw new TimeoutException("Test child process timeout. " + stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult()); }
        return (child.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static void WriteProbe(string name)
    {
        var result = string.Join("\n", checks) + $"\n{name}: PASS, {checks.Count(x => x.StartsWith("PASS ", StringComparison.Ordinal))} checks.\n";
        var dir = Path.Combine(Environment.CurrentDirectory, "artifacts"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".txt"), result); Console.Write(result);
    }
}
