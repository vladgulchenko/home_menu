using HomeMenu.Core;
using HomeMenu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HomeMenu.Tests;

public sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
public sealed class SaveFault : SaveChangesInterceptor
{
    public bool Enabled { get; set; }
    public int? SavesBeforeFailure { get; set; }
    public Action? BeforeSave { get; set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        BeforeSave?.Invoke();
        if (Enabled || SavesBeforeFailure == 0) throw new InvalidOperationException("Injected save failure");
        if (SavesBeforeFailure.HasValue) SavesBeforeFailure--;
        return ValueTask.FromResult(result);
    }
}
public sealed class TestDatabase : IAsyncDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HomeMenuTests", Guid.NewGuid() + ".db");
    public SaveFault Fault { get; } = new();
    public DatabaseRuntime Runtime { get; }
    public UserSession Session { get; } = new();
    public HomeMenuService Service { get; }
    public TestDatabase() { Runtime = new(Path, new FixedClock(), Fault); Service = new(Runtime, Session); }
    public async Task Initialize()
    {
        await Runtime.InitializeAsync(); await Service.RegisterAsync("test.user", "Тест", "test-pass-2026", "test-pass-2026");
        await Service.LoginAsync("test.user", "test-pass-2026"); await Service.SaveProfileAsync(new(Sex.Male, 20, 175, 70, ActivityLevel.Moderate, 150));
    }
    public Task<MenuPlan> Generate() => Service.GenerateWeekAsync(new(2026, 11, 2), 2200, 150, null, false);
    public async Task<MenuPlan> OatPlan()
    {
        var r = CalculationsTests.OatRecipe(); r.Id = 0;
        foreach (var i in r.Ingredients)
        {
            i.Product.Id = (await Service.SearchProductsAsync(i.Product.Name)).FirstOrDefault(p => p.Name == i.Product.Name)?.Id ?? 0;
            i.ProductId = await Service.SaveProductAsync(i.Product);
        }
        var recipes = new List<Recipe>();
        foreach (var type in Enum.GetValues<MealType>())
        {
            var copy = new Recipe
            {
                Name = "Тестовая каша " + type,
                MealType = type,
                Instructions = "Исходная инструкция",
                CookingMinutes = 15,
                Ingredients = r.Ingredients.Select(i => new Ingredient { ProductId = i.ProductId, QuantityGrams = i.QuantityGrams }).ToList()
            };
            await Service.SaveRecipeAsync(copy);
        }
        recipes = (await Service.SearchRecipesAsync("Тестовая каша"));
        await using var db = Runtime.CreateContext();
        var plan = new MenuPlan { UserId = Session.Require().UserId, WeekStart = new(2026, 11, 2), DailyCalorieTarget = 1500, ProteinTargetGrams = 112.5m, FatTargetGrams = 50, CarbsTargetGrams = 150, Budget = 150, CreatedAtUtc = Runtime.Clock.GetUtcNow().UtcDateTime, Version = 1 };
        for (var d = 0; d < 7; d++) foreach (var recipe in recipes) plan.Meals.Add(RecipeCalculator.Meal(recipe, plan.WeekStart.AddDays(d)));
        db.MenuPlans.Add(plan); await db.SaveChangesAsync(); return plan;
    }
    public async ValueTask DisposeAsync()
    {
        await Task.Yield(); if (File.Exists(Path)) File.Delete(Path);
    }
}
