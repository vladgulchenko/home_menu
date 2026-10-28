using HomeMenu.Core;
using HomeMenu.Infrastructure;
using Xunit;

namespace HomeMenu.Tests;

public sealed class PausingGenerator : IMenuGenerator, IDisposable
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ManualResetEventSlim Continue { get; } = new();
    public SearchResult Generate(DateOnly week, DateOnly today, decimal target, decimal budget, IReadOnlyList<Recipe> recipes, IReadOnlySet<int> excluded, IReadOnlyList<Lot> pantry, int maxVisitedNodes = 100000, CancellationToken cancellationToken = default)
    {
        Started.TrySetResult(); if (!Continue.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test barrier timed out");
        return new MenuGenerator().Generate(week, today, target, budget, recipes, excluded, pantry, maxVisitedNodes, cancellationToken);
    }
    public void Dispose() => Continue.Dispose();
}
public sealed class ConcurrencyTests
{
    [Fact]
    public void InstanceLeaseRejectsSecondThreadAndReleasesCleanly()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        using (var first = new InstanceLease(path))
        {
            var rejected = false;
            var thread = new Thread(() => { try { using var second = new InstanceLease(path); } catch (BusinessException) { rejected = true; } });
            thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5))); Assert.True(rejected);
        }
        using var reopened = new InstanceLease(path);
    }
    [Fact]
    public async Task StaleInputChangedDuringSearchIsRejectedAndOldWeekSurvives()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var old = await f.Generate();
        using var generator = new PausingGenerator(); var service = new HomeMenuService(f.Runtime, f.Session, generator);
        var pending = service.GenerateWeekAsync(old.WeekStart, 2200, 150, old.Version, true);
        await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var product = (await f.Service.SearchProductsAsync())[0]; product.PricePer100g += 0.0001m; await f.Service.SaveProductAsync(product);
        generator.Continue.Set(); var ex = await Assert.ThrowsAsync<BusinessException>(() => pending); Assert.Contains("изменились", ex.Message);
        var actual = (await f.Service.GetWeekAsync(old.WeekStart))!; Assert.Equal(old.Version, actual.Version); Assert.Equal(old.Meals.Select(m => m.Id).Order(), actual.Meals.Select(m => m.Id).Order());
    }
    [Fact]
    public async Task CancelDuringBackgroundSearchDoesNotReplaceWeek()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var old = await f.Generate();
        using var generator = new PausingGenerator(); var service = new HomeMenuService(f.Runtime, f.Session, generator); using var cts = new CancellationTokenSource();
        var pending = service.GenerateWeekAsync(old.WeekStart, 2200, 150, old.Version, true, cts.Token);
        await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(10)); cts.Cancel(); generator.Continue.Set();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending); Assert.Equal(old.Version, (await f.Service.GetWeekAsync(old.WeekStart))!.Version);
    }
    [Fact]
    public async Task SessionSwitchDuringSearchDoesNotWriteForOldOrNewOwner()
    {
        await using var f = new TestDatabase(); await f.Initialize(); using var generator = new PausingGenerator(); var service = new HomeMenuService(f.Runtime, f.Session, generator);
        var pending = service.GenerateWeekAsync(new(2026, 11, 2), 2200, 150, null, false); await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        f.Service.Logout(); await f.Service.LoginAsync("test.user", "test-pass-2026"); generator.Continue.Set();
        await Assert.ThrowsAsync<BusinessException>(() => pending); Assert.Null(await f.Service.GetWeekAsync(new(2026, 11, 2)));
    }
}
