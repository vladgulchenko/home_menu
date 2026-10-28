using System.Text.Json;
using System.Text.RegularExpressions;
using HomeMenu.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;

namespace HomeMenu.Infrastructure;

/// <summary>Application boundary. Each operation owns a short-lived context; all writes share the runtime gate.</summary>
public sealed class HomeMenuService(DatabaseRuntime runtime, UserSession session, IMenuGenerator? generator = null) : IAuthService, IProfileService, ICatalogService, IPantryService, IMenuService
{
    private readonly PasswordHasher<User> hasher = new(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = 100000 }));

    private async Task<T> Read<T>(Func<AppDbContext, int, Task<T>> action)
    {
        var ticket = session.Require();
        await runtime.Gate.WaitAsync();
        try { session.Verify(ticket); await using var db = runtime.CreateContext(); return await action(db, ticket.UserId); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { throw new BusinessException("База данных занята. Подождите и повторите действие."); }
        finally { runtime.Gate.Release(); }
    }
    private async Task<T> Write<T>(Func<AppDbContext, int, Task<T>> action, SessionTicket? captured = null)
    {
        var ticket = captured ?? session.Require();
        await runtime.Gate.WaitAsync();
        try
        {
            session.Verify(ticket);
            await using var db = runtime.CreateContext(); await using var tx = await db.Database.BeginTransactionAsync();
            var result = await action(db, ticket.UserId); session.Verify(ticket);
            await db.SaveChangesAsync(); await tx.CommitAsync(); runtime.Revision++; return result;
        }
        catch (DbUpdateConcurrencyException) { throw new BusinessException("Данные изменились. Обновите экран и повторите действие."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 5 or 6 }) { throw new BusinessException("База данных занята. Подождите и повторите действие."); }
        catch (DbUpdateException) { throw new BusinessException("Не удалось сохранить: запись связана с другими данными или нарушает ограничение. Обновите экран и проверьте ввод."); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { throw new BusinessException("База данных занята. Подождите и повторите действие."); }
        finally { runtime.Gate.Release(); }
    }

    public async Task RegisterAsync(string login, string name, string password, string repeat)
    {
        login = login.Trim(); name = Rules.Text(name, "Имя", 100);
        if (!Regex.IsMatch(login, @"^[A-Za-z0-9._-]{3,40}$")) throw new BusinessException("Логин: 3–40 латинских букв, цифр, точек, дефисов или подчеркиваний.");
        if (password.Length is < 8 or > 128 || string.IsNullOrWhiteSpace(password)) throw new BusinessException("Пароль: 8–128 символов, не только пробелы.");
        if (password != repeat) throw new BusinessException("Пароли не совпадают.");
        await runtime.Gate.WaitAsync();
        try
        {
            await using var db = runtime.CreateContext(); var normalized = login.ToUpperInvariant();
            if (await db.Users.AnyAsync(x => x.NormalizedLogin == normalized)) throw new BusinessException("Этот логин уже занят.");
            var user = new User { Login = login, NormalizedLogin = normalized, DisplayName = name };
            user.PasswordHash = hasher.HashPassword(user, password); db.Users.Add(user); await db.SaveChangesAsync(); runtime.Revision++;
        }
        finally { runtime.Gate.Release(); }
    }
    public async Task LoginAsync(string login, string password)
    {
        await runtime.Gate.WaitAsync();
        try
        {
            await using var db = runtime.CreateContext(); var normalized = login.Trim().ToUpperInvariant();
            var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedLogin == normalized);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
                throw new BusinessException("Неверный логин или пароль.");
            session.Set(user);
        }
        finally { runtime.Gate.Release(); }
    }
    public void Logout() => session.Clear();
    public Task<User> GetProfileAsync() => Read(async (db, id) =>
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        user.PasswordHash = ""; return user;
    });
    public Task SaveProfileAsync(ProfileInput input)
    {
        Rules.Profile(input);
        return Write(async (db, id) =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == id); u.Sex = input.Sex; u.Age = input.Age; u.HeightCm = input.HeightCm;
            u.WeightKg = input.WeightKg; u.ActivityLevel = input.ActivityLevel; u.WeeklyBudget = input.WeeklyBudget; return true;
        });
    }
    public Task<HashSet<int>> GetExcludedProductsAsync() => Read(async (db, id) => (await db.UserExcludedProducts.Where(x => x.UserId == id).Select(x => x.ProductId).ToListAsync()).ToHashSet());
    public Task SaveExcludedProductsAsync(IEnumerable<int> ids)
    {
        var selected = ids.Distinct().Order().ToArray();
        return Write(async (db, id) =>
        {
            if (await db.Products.CountAsync(x => selected.Contains(x.Id)) != selected.Length) throw new BusinessException("Продукт исключения не найден.");
            db.UserExcludedProducts.RemoveRange(await db.UserExcludedProducts.Where(x => x.UserId == id).ToListAsync());
            await db.SaveChangesAsync();
            db.UserExcludedProducts.AddRange(selected.Select(p => new UserExcludedProduct { UserId = id, ProductId = p })); return true;
        });
    }
    public Task<List<Product>> SearchProductsAsync(string name = "", string category = "") => Read(async (db, id) =>
    {
        var n = Rules.Normalize(name); var c = Rules.Normalize(category);
        return (await db.Products.AsNoTracking().ToListAsync()).Where(p => p.NormalizedName.Contains(n) && Rules.Normalize(p.Category).Contains(c)).OrderBy(p => p.Name).ToList();
    });
    public Task<int> SaveProductAsync(Product input)
    {
        Rules.Product(input);
        return Write(async (db, id) =>
        {
            if (await db.Products.AnyAsync(x => x.NormalizedName == input.NormalizedName && x.Id != input.Id)) throw new BusinessException("Продукт с таким названием уже есть.");
            var p = input.Id == 0 ? new Product() : await db.Products.FindAsync(input.Id) ?? throw new BusinessException("Продукт не найден.");
            p.Name = input.Name; p.NormalizedName = input.NormalizedName; p.Category = input.Category; p.CaloriesPer100g = input.CaloriesPer100g;
            p.ProteinPer100g = input.ProteinPer100g; p.FatPer100g = input.FatPer100g; p.CarbsPer100g = input.CarbsPer100g; p.PricePer100g = input.PricePer100g;
            if (p.Id == 0) db.Products.Add(p); await db.SaveChangesAsync(); return p.Id;
        });
    }
    public Task DeleteProductAsync(int productId) => Write(async (db, id) =>
    {
        if (await db.Ingredients.AnyAsync(x => x.ProductId == productId) || await db.PantryStocks.AnyAsync(x => x.ProductId == productId)
            || await db.MenuIngredientSnapshots.AnyAsync(x => x.ProductId == productId) || await db.UserExcludedProducts.AnyAsync(x => x.ProductId == productId))
            throw new BusinessException("Продукт используется в рецепте, запасах, исключениях или истории меню; удаление запрещено.");
        db.Products.Remove(await db.Products.FindAsync(productId) ?? throw new BusinessException("Продукт не найден.")); return true;
    });
    public Task<List<Recipe>> SearchRecipesAsync(string name = "", MealType? meal = null, int? ingredient = null, decimal? maxPrice = null) => Read(async (db, id) =>
    {
        if (maxPrice < 0) throw new BusinessException("Максимальная цена не может быть отрицательной.");
        var n = Rules.Normalize(name);
        var recipes = await db.Recipes.AsNoTracking().Include(r => r.Ingredients).ThenInclude(i => i.Product).ToListAsync();
        return recipes.Where(r => Rules.Normalize(r.Name).Contains(n) && (meal == null || r.MealType == meal)
            && (ingredient == null || r.Ingredients.Any(i => i.ProductId == ingredient)) && (maxPrice == null || RecipeCalculator.Calculate(r).Cost <= maxPrice))
            .OrderBy(r => r.Name).ToList();
    });
    public Task<int> SaveRecipeAsync(Recipe input)
    {
        Rules.Recipe(input);
        return Write(async (db, id) =>
        {
            var ids = input.Ingredients.Select(i => i.ProductId).ToArray();
            if (await db.Products.CountAsync(p => ids.Contains(p.Id)) != ids.Length) throw new BusinessException("Один из продуктов рецепта не найден.");
            var r = input.Id == 0 ? new Recipe() : await db.Recipes.Include(r => r.Ingredients).SingleOrDefaultAsync(r => r.Id == input.Id) ?? throw new BusinessException("Рецепт не найден.");
            r.Name = input.Name; r.MealType = input.MealType; r.DishCategory = input.DishCategory; r.CookingMinutes = input.CookingMinutes; r.Instructions = input.Instructions;
            if (r.Id == 0) db.Recipes.Add(r);
            else { db.Ingredients.RemoveRange(r.Ingredients); await db.SaveChangesAsync(); r.Ingredients.Clear(); }
            r.Ingredients = input.Ingredients.Select(i => new Ingredient { ProductId = i.ProductId, QuantityGrams = i.QuantityGrams }).ToList();
            await db.SaveChangesAsync(); return r.Id;
        });
    }
    public Task DeleteRecipeAsync(int recipeId) => Write(async (db, id) =>
    {
        if (await db.WeeklyMenus.AnyAsync(m => m.RecipeId == recipeId)) throw new BusinessException("Рецепт используется в меню; удаление запрещено.");
        db.Recipes.Remove(await db.Recipes.FindAsync(recipeId) ?? throw new BusinessException("Рецепт не найден.")); return true;
    });
    public Task<List<PantryStock>> GetPantryAsync() => Read((db, id) => db.PantryStocks.AsNoTracking().Where(x => x.UserId == id).Include(x => x.Product).ToListAsync());
    public Task<int> SaveLotAsync(int lotId, int productId, decimal grams, DateOnly? expiry)
    {
        Rules.Number(grams, lotId == 0 ? 0.01m : 0, 10000000, 2, "Количество");
        return Write(async (db, id) =>
        {
            var lot = lotId == 0 ? new PantryStock { UserId = id } : await db.PantryStocks.SingleOrDefaultAsync(x => x.Id == lotId && x.UserId == id) ?? throw new BusinessException("Партия недоступна.");
            if (!await db.Products.AnyAsync(x => x.Id == productId)) throw new BusinessException("Продукт не найден.");
            lot.ProductId = productId; lot.QuantityGrams = grams; lot.ExpiryDate = expiry;
            if (lotId == 0) db.PantryStocks.Add(lot); await db.SaveChangesAsync(); return lot.Id;
        });
    }
    public Task DeleteLotAsync(int lotId) => Write(async (db, id) =>
    {
        db.PantryStocks.Remove(await db.PantryStocks.SingleOrDefaultAsync(x => x.Id == lotId && x.UserId == id) ?? throw new BusinessException("Партия недоступна.")); return true;
    });

    private static IQueryable<MenuPlan> Plans(AppDbContext db) => db.MenuPlans.Include(p => p.Meals).ThenInclude(m => m.Ingredients);
    private static async Task<MenuPlan> Owned(AppDbContext db, int id, int planId) => await Plans(db).SingleOrDefaultAsync(p => p.Id == planId && p.UserId == id) ?? throw new BusinessException("План недоступен.");
    private static void Version(MenuPlan plan, int expected)
    {
        if (plan.Version != expected) throw new BusinessException("План изменился. Обновите данные и повторите действие.");
    }
    private static async Task<HashSet<int>> Exclusions(AppDbContext db, int id, MenuPlan plan)
    {
        var ids = (JsonSerializer.Deserialize<int[]>(plan.ExcludedProductIdsJson) ?? []).ToHashSet();
        ids.UnionWith(await db.UserExcludedProducts.Where(x => x.UserId == id).Select(x => x.ProductId).ToListAsync()); return ids;
    }
    private static void Allowed(IEnumerable<MenuIngredientSnapshot> ingredients, HashSet<int> excluded)
    {
        if (ingredients.Any(i => excluded.Contains(i.ProductId))) throw new BusinessException("Блюдо содержит исключенный продукт. Выберите другой рецепт.");
    }
    public Task<MenuPlan?> GetWeekAsync(DateOnly week) => Read((db, id) => Plans(db).AsNoTracking().SingleOrDefaultAsync(p => p.UserId == id && p.WeekStart == week));
    public async Task<MenuPlan> GenerateWeekAsync(DateOnly week, decimal? calorieOverride, decimal budget, int? expectedVersion, bool replace, CancellationToken cancellationToken = default, int maxNodes = 100000)
    {
        var ticket = session.Require();
        if (week.DayOfWeek != DayOfWeek.Monday || week < Rules.Monday(runtime.Today)) throw new BusinessException("Выберите понедельник текущей или будущей недели.");
        Rules.Number(budget, 0, 1000000, 4, "Бюджет");
        if (calorieOverride.HasValue) Rules.Number(calorieOverride.Value, 500, 6000, 2, "Цель калорийности");
        var snapshot = await Read(async (db, id) =>
        {
            session.Verify(ticket);
            var user = await db.Users.SingleAsync(u => u.Id == id); var targets = NutritionCalculator.Calculate(NutritionCalculator.FromUser(user));
            if (calorieOverride.HasValue) targets = NutritionCalculator.FromCalories(calorieOverride.Value);
            var old = await Plans(db).SingleOrDefaultAsync(p => p.UserId == id && p.WeekStart == week);
            CheckRegeneration(old, expectedVersion, replace);
            var recipes = await db.Recipes.AsNoTracking().Include(r => r.Ingredients).ThenInclude(i => i.Product).ToListAsync();
            var excluded = (await db.UserExcludedProducts.Where(x => x.UserId == id).Select(x => x.ProductId).ToListAsync()).ToHashSet();
            var lots = (await db.PantryStocks.AsNoTracking().Where(x => x.UserId == id).ToListAsync()).Select(s => new Lot(s.Id, s.ProductId, s.QuantityGrams, s.ExpiryDate)).ToList();
            return (targets, recipes, excluded, lots, revision: runtime.Revision, today: runtime.Today);
        });
        var k = decimal.Round(snapshot.targets.Calories, 8, MidpointRounding.AwayFromZero);
        var result = await Task.Run(() => (generator ?? new MenuGenerator()).Generate(week, snapshot.today, k, budget, snapshot.recipes, snapshot.excluded, snapshot.lots, maxNodes, cancellationToken), CancellationToken.None);
        if (result.Status == SearchStatus.Cancelled) throw new OperationCanceledException(cancellationToken);
        if (result.Status != SearchStatus.Success) throw new BusinessException(result.Message);
        cancellationToken.ThrowIfCancellationRequested();
        return await Write(async (db, id) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (runtime.Revision != snapshot.revision || runtime.Today != snapshot.today) throw new BusinessException("Исходные данные изменились во время подбора. Повторите генерацию.");
            var old = await Plans(db).SingleOrDefaultAsync(p => p.UserId == id && p.WeekStart == week); CheckRegeneration(old, expectedVersion, replace);
            var plan = old ?? new MenuPlan { UserId = id, WeekStart = week };
            if (old != null) { db.WeeklyMenus.RemoveRange(plan.Meals); await db.SaveChangesAsync(); plan.Meals.Clear(); }
            plan.DailyCalorieTarget = k;
            var targets = NutritionCalculator.FromCalories(k);
            plan.ProteinTargetGrams = decimal.Round(targets.Protein, 8, MidpointRounding.AwayFromZero);
            plan.FatTargetGrams = decimal.Round(targets.Fat, 8, MidpointRounding.AwayFromZero);
            plan.CarbsTargetGrams = decimal.Round(targets.Carbs, 8, MidpointRounding.AwayFromZero);
            plan.Budget = budget; plan.CreatedAtUtc = runtime.Clock.GetUtcNow().UtcDateTime;
            plan.ExcludedProductIdsJson = JsonSerializer.Serialize(snapshot.excluded.Order()); plan.Version++;
            plan.Meals = result.Meals.ToList();
            if (plan.Meals.Count != 21 || PlanAnalysis.Warnings(plan).Count != 0) throw new BusinessException("Результат поиска не прошел итоговую проверку.");
            if (old == null) db.MenuPlans.Add(plan);
            await db.SaveChangesAsync(); return plan;
        }, ticket);
    }
    private static void CheckRegeneration(MenuPlan? old, int? version, bool replace)
    {
        if (old == null) { if (version != null) throw new BusinessException("План изменился. Обновите экран."); return; }
        if (old.Meals.Any(m => m.CookedAtUtc != null)) throw new BusinessException("В неделе есть приготовленные блюда; перегенерация запрещена.");
        if (!replace) throw new ConfirmationRequiredException("Заменить существующее меню недели?");
        if (version == null) throw new BusinessException("Обновите существующий план."); Version(old, version.Value);
    }
    public Task ReplaceMealAsync(int planId, int mealId, int recipeId, int version, bool acceptViolations = false, long? expectedRevision = null) => Write(async (db, id) =>
    {
        var plan = await Owned(db, id, planId); Version(plan, version);
        if (expectedRevision.HasValue && expectedRevision != runtime.Revision)
            throw new BusinessException("Данные изменились после подтверждения. Повторите замену и проверьте новые расчеты.");
        var meal = plan.Meals.SingleOrDefault(m => m.Id == mealId) ?? throw new BusinessException("Прием пищи недоступен.");
        if (meal.CookedAtUtc != null) throw new BusinessException("Приготовленное блюдо нельзя заменить.");
        var recipe = await db.Recipes.Include(r => r.Ingredients).ThenInclude(i => i.Product).SingleOrDefaultAsync(r => r.Id == recipeId) ?? throw new BusinessException("Рецепт не найден.");
        if (recipe.MealType != meal.MealType) throw new BusinessException("Категория рецепта не соответствует приему пищи.");
        var replacement = RecipeCalculator.Meal(recipe, meal.Date); Allowed(replacement.Ingredients, await Exclusions(db, id, plan));
        var before = meal.Ingredients; meal.Ingredients = replacement.Ingredients;
        var warnings = PlanAnalysis.Warnings(plan);
        if (warnings.Count != 0 && !acceptViolations) throw new ConfirmationRequiredException(string.Join("\n", warnings) + "\nСохранить с нарушением ограничений?", runtime.Revision);
        if (warnings.Count != 0 && acceptViolations && !expectedRevision.HasValue)
            throw new BusinessException("Подтверждение устарело. Повторите замену с актуальными расчетами.");
        meal.Ingredients = before;
        db.MenuIngredientSnapshots.RemoveRange(before); meal.Ingredients.Clear(); await db.SaveChangesAsync();
        meal.Ingredients = replacement.Ingredients;
        meal.RecipeId = recipe.Id; meal.RecipeNameSnapshot = recipe.Name; meal.InstructionsSnapshot = recipe.Instructions; plan.Version++; return true;
    });
    public Task CookMealAsync(int planId, int mealId, int version) => Write(async (db, id) =>
    {
        var plan = await Owned(db, id, planId); Version(plan, version);
        var meal = plan.Meals.SingleOrDefault(m => m.Id == mealId) ?? throw new BusinessException("Прием пищи недоступен.");
        if (meal.CookedAtUtc != null) throw new BusinessException("Это блюдо уже приготовлено.");
        if (meal.Date > runtime.Today) throw new BusinessException("Будущий прием пищи пока нельзя приготовить.");
        Allowed(meal.Ingredients, await Exclusions(db, id, plan));
        var stocks = await db.PantryStocks.Where(x => x.UserId == id).ToListAsync();
        var allocation = PantryAllocator.Allocate(stocks.Select(x => new Lot(x.Id, x.ProductId, x.QuantityGrams, x.ExpiryDate)), PantryAllocator.Needs(meal.Ingredients), runtime.Today);
        if (allocation.Missing.Count > 0) throw new BusinessException("Недостаточно продуктов: " + string.Join(", ", allocation.Missing.Select(n => $"{meal.Ingredients.First(i => i.ProductId == n.ProductId).ProductNameSnapshot}: {n.Grams:F2} г")));
        var remaining = allocation.Remaining.ToDictionary(l => l.Id);
        foreach (var stock in stocks) stock.QuantityGrams = remaining[stock.Id].Quantity;
        meal.CookedAtUtc = runtime.Clock.GetUtcNow().UtcDateTime; plan.Version++; return true;
    });
    public Task<List<ShoppingItem>> BuildShoppingAsync(int planId) => Read(async (db, id) => await Shopping(db, id, await Owned(db, id, planId)));
    private async Task<List<ShoppingItem>> Shopping(AppDbContext db, int id, MenuPlan plan)
    {
        var stocks = (await db.PantryStocks.AsNoTracking().Where(x => x.UserId == id).ToListAsync()).Select(s => new Lot(s.Id, s.ProductId, s.QuantityGrams, s.ExpiryDate)).ToList();
        var required = new Dictionary<int, decimal>(); var missing = new Dictionary<int, decimal>();
        foreach (var meal in plan.Meals.Where(m => m.CookedAtUtc == null).OrderBy(m => m.Date).ThenBy(m => m.MealType).ThenBy(m => m.Id))
        {
            var needs = PantryAllocator.Needs(meal.Ingredients);
            foreach (var n in needs) required[n.ProductId] = required.GetValueOrDefault(n.ProductId) + n.Grams;
            var allocation = PantryAllocator.Allocate(stocks, needs, meal.Date < runtime.Today ? runtime.Today : meal.Date); stocks = allocation.Remaining;
            foreach (var n in allocation.Missing) missing[n.ProductId] = missing.GetValueOrDefault(n.ProductId) + n.Grams;
        }
        var products = await db.Products.AsNoTracking().Where(p => required.Keys.Contains(p.Id)).ToListAsync();
        return products.Where(p => missing.GetValueOrDefault(p.Id) > 0).Select(p => new ShoppingItem(p.Id, p.Name, p.Category, required[p.Id], required[p.Id] - missing[p.Id], missing[p.Id], p.PricePer100g)).OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
    }
    public Task<WeekSummary> GetWeekSummaryAsync(int planId) => Read(async (db, id) =>
    {
        var p = await Owned(db, id, planId); var planned = RecipeCalculator.Calculate(p.Meals.SelectMany(m => m.Ingredients));
        var cooked = p.Meals.Where(m => m.CookedAtUtc != null).ToList(); var confirmed = RecipeCalculator.Calculate(cooked.SelectMany(m => m.Ingredients));
        var days = p.Meals.GroupBy(m => m.Date).OrderBy(g => g.Key).Select(g =>
        {
            var n = RecipeCalculator.Calculate(g.SelectMany(m => m.Ingredients));
            return new DaySummary(g.Key, n, n.Calories - p.DailyCalorieTarget, n.Protein - p.ProteinTargetGrams, n.Fat - p.FatTargetGrams, n.Carbs - p.CarbsTargetGrams);
        }).ToList();
        var warnings = PlanAnalysis.Warnings(p); var excluded = await Exclusions(db, id, p);
        if (p.Meals.Any(m => m.CookedAtUtc == null && m.Ingredients.Any(i => excluded.Contains(i.ProductId)))) warnings.Add("В неприготовленных блюдах есть исключенный продукт. Замените эти блюда перед приготовлением.");
        return new WeekSummary(planned, confirmed, cooked.Count, p.Budget - planned.Cost, (await Shopping(db, id, p)).Sum(i => i.Cost), days, warnings);
    });
}
