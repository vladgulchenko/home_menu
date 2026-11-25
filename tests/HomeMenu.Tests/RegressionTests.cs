using HomeMenu.Core;
using HomeMenu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMenu.Tests;

public sealed class RegressionTests
{
    [Fact]
    public async Task FailedLoginInvalidatesPreviouslyAuthenticatedSession()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.LoginAsync("test.user", "wrong-password"));
        Assert.Throws<BusinessException>(() => f.Session.Require());
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GetProfileAsync());
        await f.Service.LoginAsync("test.user", "test-pass-2026");
        Assert.Equal("Тест", (await f.Service.GetProfileAsync()).DisplayName);
    }

    [Fact]
    public async Task LogoutDuringFinalSaveRollsBackProfileChange()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        f.Fault.BeforeSave = f.Session.Clear;
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveProfileAsync(new(Sex.Male, 25, 175, 70, ActivityLevel.Moderate, 150)));
        await using var db = f.Runtime.CreateContext();
        Assert.Equal(20, (await db.Users.SingleAsync()).Age);
        Assert.Throws<BusinessException>(() => f.Session.Require());
        f.Fault.BeforeSave = null;
        await f.Service.LoginAsync("test.user", "test-pass-2026");
        Assert.Equal(20, (await f.Service.GetProfileAsync()).Age);
    }

    [Fact]
    public async Task FailedRecipeReplacementRestoresOriginalIngredientsAndText()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        var recipe = (await f.Service.SearchRecipesAsync())[0];
        var originalName = recipe.Name;
        var original = recipe.Ingredients.OrderBy(x => x.ProductId).Select(x => (x.ProductId, x.QuantityGrams)).ToArray();
        recipe.Name = "Не сохранённое название"; recipe.Ingredients[0].QuantityGrams += 1;
        f.Fault.SavesBeforeFailure = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveRecipeAsync(recipe));
        f.Fault.SavesBeforeFailure = null;
        var actual = (await f.Service.SearchRecipesAsync()).Single(x => x.Id == recipe.Id);
        Assert.Equal(originalName, actual.Name);
        Assert.Equal(original, actual.Ingredients.OrderBy(x => x.ProductId).Select(x => (x.ProductId, x.QuantityGrams)).ToArray());
    }

    [Fact]
    public async Task FailedExclusionsReplacementPreservesPreviousSelection()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        var products = await f.Service.SearchProductsAsync();
        await f.Service.SaveExcludedProductsAsync([products[0].Id]);
        f.Fault.SavesBeforeFailure = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveExcludedProductsAsync([products[1].Id]));
        f.Fault.SavesBeforeFailure = null;
        Assert.Equal(new[] { products[0].Id }, (await f.Service.GetExcludedProductsAsync()).Order().ToArray());
    }

    [Fact]
    public async Task SqliteFailureWhenMarkingCookedRollsBackStocksAndVersion()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan();
        var meal = plan.Meals.OrderBy(m => m.Date).ThenBy(m => m.MealType).First();
        foreach (var item in meal.Ingredients) await f.Service.SaveLotAsync(0, item.ProductId, item.QuantityGrams + 1000, null);
        var before = (await f.Service.GetPantryAsync()).OrderBy(x => x.Id).Select(x => (x.Id, x.QuantityGrams)).ToArray();
        await using (var db = f.Runtime.CreateContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectCook BEFORE UPDATE ON WeeklyMenus BEGIN SELECT RAISE(ABORT, 'test cooking failure'); END;");
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CookMealAsync(plan.Id, meal.Id, plan.Version));
        var after = (await f.Service.GetPantryAsync()).OrderBy(x => x.Id).Select(x => (x.Id, x.QuantityGrams)).ToArray();
        Assert.Equal(before, after);
        var actual = (await f.Service.GetWeekAsync(plan.WeekStart))!;
        Assert.Equal(plan.Version, actual.Version); Assert.All(actual.Meals, x => Assert.Null(x.CookedAtUtc));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("save-profile")]
    [InlineData("pantry")]
    [InlineData("products")]
    [InlineData("recipes")]
    [InlineData("exclusions")]
    [InlineData("week")]
    [InlineData("shopping")]
    [InlineData("statistics")]
    [InlineData("generate")]
    public async Task LoggedOutCallsCannotReadOrWriteApplicationData(string operation)
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan(); f.Service.Logout();
        Func<Task> action = operation switch
        {
            "profile" => () => f.Service.GetProfileAsync(),
            "save-profile" => () => f.Service.SaveProfileAsync(new(Sex.Male, 30, 180, 80, ActivityLevel.Active, 200)),
            "pantry" => () => f.Service.GetPantryAsync(),
            "products" => () => f.Service.SearchProductsAsync(),
            "recipes" => () => f.Service.SearchRecipesAsync(),
            "exclusions" => () => f.Service.GetExcludedProductsAsync(),
            "week" => () => f.Service.GetWeekAsync(plan.WeekStart),
            "shopping" => () => f.Service.BuildShoppingAsync(plan.Id),
            "statistics" => () => f.Service.GetWeekSummaryAsync(plan.Id),
            _ => () => f.Service.GenerateWeekAsync(plan.WeekStart, 2200, 150, plan.Version, true)
        };
        await Assert.ThrowsAsync<BusinessException>(action);
        await using var db = f.Runtime.CreateContext();
        Assert.Equal(plan.Version, (await db.MenuPlans.SingleAsync()).Version);
        Assert.Equal(21, await db.WeeklyMenus.CountAsync());
        Assert.Equal(20, (await db.Users.SingleAsync()).Age);
    }

    [Theory]
    [InlineData("ab", "good-password", "good-password", "Имя")]
    [InlineData("bad login", "good-password", "good-password", "Имя")]
    [InlineData("кириллица", "good-password", "good-password", "Имя")]
    [InlineData("new.user", "short", "short", "Имя")]
    [InlineData("new.user", "good-password", "another-password", "Имя")]
    [InlineData("new.user", "good-password", "good-password", " ")]
    public async Task InvalidRegistrationDoesNotCreateAccount(string login, string password, string repeat, string name)
    {
        await using var f = new TestDatabase(); await f.Runtime.InitializeAsync();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.RegisterAsync(login, name, password, repeat));
        await using var db = f.Runtime.CreateContext(); Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task ModelMatchesCommittedMigration()
    {
        await using var f = new TestDatabase(); await using var db = f.Runtime.CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
