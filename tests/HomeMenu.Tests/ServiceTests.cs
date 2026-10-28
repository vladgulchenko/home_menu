using HomeMenu.Core;
using HomeMenu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMenu.Tests;

public sealed class ServiceTests
{
    [Fact]
    public async Task ConfirmationCannotApplyAfterRecipeOrPriceChanged()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan(); var meal = plan.Meals[0];
        var product = (await f.Service.SearchProductsAsync("Овсянка")).Single();
        var recipe = new Recipe { Name = "Для подтверждения", MealType = meal.MealType, CookingMinutes = 1, Instructions = "Тест", Ingredients = [new() { ProductId = product.Id, QuantityGrams = 1000 }] };
        recipe.Id = await f.Service.SaveRecipeAsync(recipe);
        var confirmation = await Assert.ThrowsAsync<ConfirmationRequiredException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, recipe.Id, plan.Version));
        product.PricePer100g = 10; await f.Service.SaveProductAsync(product);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, recipe.Id, plan.Version, true, confirmation.Revision));
        Assert.Equal(meal.RecipeId, (await f.Service.GetWeekAsync(plan.WeekStart))!.Meals.Single(m => m.Id == meal.Id).RecipeId);
    }
    [Fact]
    public async Task MigrationsSeedAndReopenPreserveData()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.Generate();
        await f.Runtime.InitializeAsync();
        await using var db = f.Runtime.CreateContext(); Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(17, await db.Products.CountAsync()); Assert.Equal(9, await db.Recipes.CountAsync()); Assert.Single(await db.Users.ToListAsync());
        var reopened = new DatabaseRuntime(f.Path, new FixedClock()); await reopened.InitializeAsync(); var session = new UserSession(); var service = new HomeMenuService(reopened, session);
        await service.LoginAsync("TEST.USER", "test-pass-2026"); Assert.Equal(21, (await service.GetWeekAsync(plan.WeekStart))!.Meals.Count);
        Assert.Equal(plan.Id, (await service.GetWeekAsync(plan.WeekStart))!.Id);
    }
    [Fact]
    public async Task AuthNormalizesLoginHashesWithSaltAndClearsSession()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.RegisterAsync("TEST.USER", "Другой", "test-pass-2026", "test-pass-2026"));
        await f.Service.RegisterAsync("second.user", "Второй", "test-pass-2026", "test-pass-2026");
        await using (var db = f.Runtime.CreateContext())
        {
            var hashes = await db.Users.Select(u => u.PasswordHash).ToListAsync(); Assert.NotEqual(hashes[0], hashes[1]); Assert.All(hashes, h => Assert.NotEqual("test-pass-2026", h));
        }
        f.Service.Logout(); await Assert.ThrowsAsync<BusinessException>(() => f.Service.GetProfileAsync());
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.LoginAsync("test.user", "wrong-pass"));
        await f.Service.LoginAsync(" test.USER ", "test-pass-2026"); Assert.Equal("Тест", (await f.Service.GetProfileAsync()).DisplayName);
        Assert.Equal("", (await f.Service.GetProfileAsync()).PasswordHash);
    }
    [Fact]
    public async Task OwnersCannotAccessForeignLotsPlansMealsOrExclusions()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.Generate(); var product = (await f.Service.SearchProductsAsync())[0];
        var lot = await f.Service.SaveLotAsync(0, product.Id, 500, null); await f.Service.SaveExcludedProductsAsync([product.Id]);
        await f.Service.RegisterAsync("other", "Другой", "other-pass", "other-pass"); await f.Service.LoginAsync("other", "other-pass");
        Assert.Empty(await f.Service.GetPantryAsync()); Assert.Empty(await f.Service.GetExcludedProductsAsync()); Assert.Null(await f.Service.GetWeekAsync(plan.WeekStart));
        Assert.Null((await f.Service.GetProfileAsync()).Age); Assert.NotEmpty(await f.Service.SearchProductsAsync());
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveLotAsync(lot, product.Id, 100, null));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.DeleteLotAsync(lot));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.BuildShoppingAsync(plan.Id));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GetWeekSummaryAsync(plan.Id));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CookMealAsync(plan.Id, plan.Meals[0].Id, plan.Version));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, plan.Meals[0].Id, plan.Meals[0].RecipeId, plan.Version));
    }
    [Fact]
    public async Task ProductCrudNormalizationFiltersAndReferenceProtection()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        var p = new Product { Name = "  Тестовый   продукт ", Category = "Тест", CaloriesPer100g = 12.34m, PricePer100g = 1.2345m };
        p.Id = await f.Service.SaveProductAsync(p);
        var found = Assert.Single(await f.Service.SearchProductsAsync("тестовый", "тест")); Assert.Equal(12.34m, found.CaloriesPer100g); Assert.Equal(1.2345m, found.PricePer100g);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveProductAsync(new() { Name = "ТЕСТОВЫЙ продукт", Category = "Тест" }));
        p.PricePer100g = 2; await f.Service.SaveProductAsync(p); Assert.Equal(2, Assert.Single(await f.Service.SearchProductsAsync("тестовый")).PricePer100g);
        await f.Service.DeleteProductAsync(p.Id); Assert.Empty(await f.Service.SearchProductsAsync("тестовый"));
        var used = (await f.Service.SearchRecipesAsync())[0].Ingredients[0].ProductId;
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.DeleteProductAsync(used));
        var plan = await f.Generate(); await Assert.ThrowsAsync<BusinessException>(() => f.Service.DeleteRecipeAsync(plan.Meals[0].RecipeId));
    }
    [Fact]
    public async Task RecipeCrudCombinedFiltersAndValidation()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var p = (await f.Service.SearchProductsAsync())[0];
        var r = new Recipe { Name = "Проверка рецепта", MealType = MealType.Breakfast, CookingMinutes = 5, Instructions = "Смешать", Ingredients = [new() { ProductId = p.Id, QuantityGrams = 100 }] };
        r.Id = await f.Service.SaveRecipeAsync(r);
        Assert.Single(await f.Service.SearchRecipesAsync("Проверка", MealType.Breakfast, p.Id, 100));
        Assert.Empty(await f.Service.SearchRecipesAsync("Проверка", MealType.Dinner, p.Id, 100));
        r.Instructions = "Новая инструкция"; r.Ingredients[0].QuantityGrams = 200; await f.Service.SaveRecipeAsync(r);
        var saved = Assert.Single(await f.Service.SearchRecipesAsync("Проверка")); Assert.Equal(200, saved.Ingredients[0].QuantityGrams); Assert.Equal("Новая инструкция", saved.Instructions);
        r.Ingredients.Add(new() { ProductId = p.Id, QuantityGrams = 1 }); await Assert.ThrowsAsync<BusinessException>(() => f.Service.SaveRecipeAsync(r));
        await f.Service.DeleteRecipeAsync(r.Id); Assert.Empty(await f.Service.SearchRecipesAsync("Проверка"));
    }
    [Fact]
    public async Task GenerationFailureCancellationAndSaveFailurePreserveOldWeek()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var old = await f.Generate(); var ids = old.Meals.Select(m => m.Id).Order().ToArray();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 0, old.Version, true));
        await Assert.ThrowsAsync<OperationCanceledException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 150, old.Version, true, new CancellationToken(true)));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 150, old.Version, true, maxNodes: 0));
        f.Fault.SavesBeforeFailure = 1; // deletion reaches SQLite, then inserting the new plan fails inside the same transaction.
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 150, old.Version, true)); f.Fault.SavesBeforeFailure = null;
        var actual = (await f.Service.GetWeekAsync(old.WeekStart))!; Assert.Equal(ids, actual.Meals.Select(m => m.Id).Order()); Assert.Equal(old.Version, actual.Version);
        await f.Service.SaveExcludedProductsAsync((await f.Service.SearchProductsAsync()).Select(p => p.Id));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 150, old.Version, true)); Assert.Equal(ids, (await f.Service.GetWeekAsync(old.WeekStart))!.Meals.Select(m => m.Id).Order());
    }
    [Fact]
    public async Task GenerationRequiresCompleteProfileMondayAndExplicitReplacement()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var old = await f.Generate();
        await Assert.ThrowsAsync<ConfirmationRequiredException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 150, null, false));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(old.WeekStart.AddDays(1), 2200, 150, null, false));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(old.WeekStart.AddDays(-7), 2200, 150, null, false));
        await f.Service.RegisterAsync("incomplete", "Новый", "new-password", "new-password"); await f.Service.LoginAsync("incomplete", "new-password");
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(old.WeekStart, 2200, 150, null, false));
    }
    [Fact]
    public async Task ManualReplacementRequiresConfirmationHonorsCategoryAndUnionOfExclusions()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan(); var meal = plan.Meals.First();
        var recipes = await f.Service.SearchRecipesAsync(); var wrong = recipes.First(r => r.MealType != meal.MealType);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, wrong.Id, plan.Version, true));
        var p = (await f.Service.SearchProductsAsync("Овсянка")).Single();
        var huge = new Recipe { Name = "Большая каша", MealType = meal.MealType, CookingMinutes = 1, Instructions = "Тест", Ingredients = [new() { ProductId = p.Id, QuantityGrams = 1000 }] }; huge.Id = await f.Service.SaveRecipeAsync(huge);
        var confirmation = await Assert.ThrowsAsync<ConfirmationRequiredException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, huge.Id, plan.Version));
        Assert.Equal(meal.RecipeId, (await f.Service.GetWeekAsync(plan.WeekStart))!.Meals.Single(m => m.Id == meal.Id).RecipeId);
        await f.Service.ReplaceMealAsync(plan.Id, meal.Id, huge.Id, plan.Version, true, confirmation.Revision);
        var updated = (await f.Service.GetWeekAsync(plan.WeekStart))!; Assert.Equal(plan.Version + 1, updated.Version); Assert.Equal(huge.Id, updated.Meals.Single(m => m.Id == meal.Id).RecipeId);
        Assert.NotEmpty((await f.Service.GetWeekSummaryAsync(plan.Id)).Warnings);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, meal.RecipeId, plan.Version, true));
        await f.Service.SaveExcludedProductsAsync([p.Id]);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, huge.Id, updated.Version, true));
        await using var db = f.Runtime.CreateContext(); var stored = await db.MenuPlans.FindAsync(plan.Id); stored!.ExcludedProductIdsJson = $"[{p.Id}]"; await db.SaveChangesAsync();
        await f.Service.SaveExcludedProductsAsync([]);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, huge.Id, updated.Version, true));
    }
    [Fact]
    public async Task N5_CookingIsAllOrNothingAndExactlyOnceConcurrently()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan(); var meal = plan.Meals.First(m => m.Date == plan.WeekStart && m.MealType == MealType.Breakfast);
        var oats = meal.Ingredients.First(i => i.ProductNameSnapshot == "Овсянка").ProductId; var milk = meal.Ingredients.First(i => i.ProductNameSnapshot == "Молоко").ProductId;
        var oatLot = await f.Service.SaveLotAsync(0, oats, 80, null); var milkLot = await f.Service.SaveLotAsync(0, milk, 500, null);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CookMealAsync(plan.Id, meal.Id, plan.Version));
        var stocks = await f.Service.GetPantryAsync(); Assert.Equal(80, stocks.Single(s => s.Id == oatLot).QuantityGrams); Assert.Equal(500, stocks.Single(s => s.Id == milkLot).QuantityGrams);
        Assert.Null((await f.Service.GetWeekAsync(plan.WeekStart))!.Meals.Single(m => m.Id == meal.Id).CookedAtUtc);
        await f.Service.SaveLotAsync(oatLot, oats, 300, null); await f.Service.SaveLotAsync(milkLot, milk, 1000, null);
        async Task<bool> Cook() { try { await f.Service.CookMealAsync(plan.Id, meal.Id, plan.Version); return true; } catch (BusinessException) { return false; } }
        var results = await Task.WhenAll(Task.Run(Cook), Task.Run(Cook)); Assert.Single(results, x => x);
        stocks = await f.Service.GetPantryAsync(); Assert.Equal(200, stocks.Single(s => s.Id == oatLot).QuantityGrams); Assert.Equal(800, stocks.Single(s => s.Id == milkLot).QuantityGrams);
        var actual = (await f.Service.GetWeekAsync(plan.WeekStart))!; Assert.NotNull(actual.Meals.Single(m => m.Id == meal.Id).CookedAtUtc);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CookMealAsync(plan.Id, meal.Id, actual.Version));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReplaceMealAsync(plan.Id, meal.Id, meal.RecipeId, actual.Version, true));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.GenerateWeekAsync(plan.WeekStart, 1500, 150, actual.Version, true));
        Assert.Equal(1, (await f.Service.GetWeekSummaryAsync(plan.Id)).CookedCount);
    }
    [Fact]
    public async Task CookingRollbackUsesSnapshotsAndRejectsFutureAndNewExclusions()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan(); var meal = plan.Meals.First(m => m.Date == plan.WeekStart);
        foreach (var i in meal.Ingredients) await f.Service.SaveLotAsync(0, i.ProductId, 10000, null);
        var oldRecipe = (await f.Service.SearchRecipesAsync()).Single(r => r.Id == meal.RecipeId);
        oldRecipe.Instructions = "Изменено"; oldRecipe.Ingredients.ForEach(i => i.QuantityGrams = 1); await f.Service.SaveRecipeAsync(oldRecipe);
        f.Fault.Enabled = true; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CookMealAsync(plan.Id, meal.Id, plan.Version)); f.Fault.Enabled = false;
        Assert.All(await f.Service.GetPantryAsync(), s => Assert.Equal(10000, s.QuantityGrams)); Assert.Null((await f.Service.GetWeekAsync(plan.WeekStart))!.Meals.Single(m => m.Id == meal.Id).CookedAtUtc);
        var future = plan.Meals.First(m => m.Date > plan.WeekStart); await Assert.ThrowsAsync<BusinessException>(() => f.Service.CookMealAsync(plan.Id, future.Id, plan.Version));
        await f.Service.SaveExcludedProductsAsync([meal.Ingredients[0].ProductId]); await Assert.ThrowsAsync<BusinessException>(() => f.Service.CookMealAsync(plan.Id, meal.Id, plan.Version)); await f.Service.SaveExcludedProductsAsync([]);
        await f.Service.CookMealAsync(plan.Id, meal.Id, plan.Version);
        var actual = (await f.Service.GetWeekAsync(plan.WeekStart))!; Assert.Equal("Исходная инструкция", actual.Meals.Single(m => m.Id == meal.Id).InstructionsSnapshot);
        foreach (var i in meal.Ingredients) Assert.Equal(10000 - i.QuantityGrams, (await f.Service.GetPantryAsync()).Single(s => s.ProductId == i.ProductId).QuantityGrams);
    }
    [Fact]
    public async Task ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan();
        var before = await f.Service.BuildShoppingAsync(plan.Id); Assert.Equal(14.70m, before.Sum(i => i.Cost));
        var milkId = plan.Meals[0].Ingredients.Single(i => i.ProductNameSnapshot == "Молоко").ProductId;
        var milk = (await f.Service.SearchProductsAsync("Молоко")).Single(); milk.PricePer100g = 0.30m; await f.Service.SaveProductAsync(milk);
        Assert.Equal(14.70m, (await f.Service.GetWeekSummaryAsync(plan.Id)).Planned.Cost);
        Assert.Equal(21, (await f.Service.BuildShoppingAsync(plan.Id)).Sum(i => i.Cost));
        foreach (var i in plan.Meals[0].Ingredients) await f.Service.SaveLotAsync(0, i.ProductId, 10000, plan.WeekStart.AddDays(-1));
        Assert.Equal(21, (await f.Service.BuildShoppingAsync(plan.Id)).Sum(i => i.Cost));
        foreach (var i in plan.Meals[0].Ingredients) await f.Service.SaveLotAsync(0, i.ProductId, i.QuantityGrams, null);
        await f.Service.CookMealAsync(plan.Id, plan.Meals[0].Id, plan.Version);
        var shopping = await f.Service.BuildShoppingAsync(plan.Id); Assert.Equal(2000, shopping.Single(i => i.Name == "Овсянка").Required); Assert.Equal(4000, shopping.Single(i => i.ProductId == milkId).Required);
        var stats = await f.Service.GetWeekSummaryAsync(plan.Id); Assert.Equal(500, stats.Confirmed.Calories); Assert.Equal(1, stats.CookedCount);
        Assert.Equal(10500, stats.Planned.Calories); Assert.Equal(7, stats.Days.Count); Assert.Equal(20, stats.ShoppingCost);
        foreach (var i in plan.Meals[0].Ingredients) await f.Service.SaveLotAsync(0, i.ProductId, 100000, null);
        Assert.Empty(await f.Service.BuildShoppingAsync(plan.Id));
    }
    [Fact]
    public async Task N4_ShoppingAcrossDatesCovers230Of280GramsWithoutWriting()
    {
        await using var f = new TestDatabase(); await f.Initialize(); var plan = await f.OatPlan();
        var product = (await f.Service.SearchProductsAsync("Овсянка")).Single();
        await using (var db = f.Runtime.CreateContext())
        {
            var snapshots = await db.MenuIngredientSnapshots.ToListAsync(); db.MenuIngredientSnapshots.RemoveRange(snapshots);
            var meals = await db.WeeklyMenus.Where(m => m.MenuPlanId == plan.Id).OrderBy(m => m.Date).ThenBy(m => m.MealType).ToListAsync();
            foreach (var m in meals) m.CookedAtUtc = f.Runtime.Clock.GetUtcNow().UtcDateTime;
            meals[0].CookedAtUtc = null; meals[3].CookedAtUtc = null;
            meals[0].Ingredients = [new() { ProductId = product.Id, ProductNameSnapshot = product.Name, CategorySnapshot = product.Category, QuantityGrams = 80, PricePer100g = 0.4m }];
            meals[3].Ingredients = [new() { ProductId = product.Id, ProductNameSnapshot = product.Name, CategorySnapshot = product.Category, QuantityGrams = 200, PricePer100g = 0.4m }];
            await db.SaveChangesAsync();
        }
        await f.Service.SaveLotAsync(0, product.Id, 100, plan.WeekStart); await f.Service.SaveLotAsync(0, product.Id, 150, plan.WeekStart.AddDays(8));
        var row = Assert.Single(await f.Service.BuildShoppingAsync(plan.Id)); Assert.Equal(280, row.Required); Assert.Equal(230, row.Covered); Assert.Equal(50, row.Missing); Assert.Equal(0.2m, row.Cost);
        Assert.Equal(250, (await f.Service.GetPantryAsync()).Sum(s => s.QuantityGrams));
    }
    [Fact]
    public async Task SqliteEnforcesScaledNumbersForeignKeysAndUniqueKeys()
    {
        await using var f = new TestDatabase(); await f.Initialize();
        await using var db = f.Runtime.CreateContext();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE Products SET CaloriesPer100gScaled=-1 WHERE Id=1"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM Products WHERE Id=1"));
        var connection = db.Database.GetDbConnection(); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "SELECT typeof(PricePer100gScaled) FROM Products LIMIT 1"; Assert.Equal("integer", await command.ExecuteScalarAsync());
        var p = (await f.Service.SearchProductsAsync())[0]; p.PricePer100g = 0.0001m; await f.Service.SaveProductAsync(p); Assert.Equal(0.0001m, (await f.Service.SearchProductsAsync(p.Name)).Single().PricePer100g);
    }
}
