using System.Globalization;
using System.Text.RegularExpressions;

namespace HomeMenu.Core;

public static class Rules
{
    public static string Text(string? value, string field, int max)
    {
        var text = (value ?? "").Trim();
        if (text.Length < 1 || text.Length > max) throw new BusinessException($"{field}: от 1 до {max} символов.");
        return text;
    }
    public static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToUpperInvariant();
    public static decimal Number(decimal value, decimal min, decimal max, int places, string field)
    {
        if (value < min || value > max || decimal.Round(value, places) != value)
            throw new BusinessException($"{field}: от {min} до {max}, не более {places} знаков после запятой.");
        return value;
    }
    public static decimal Parse(string text, string field)
    {
        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var result)) throw new BusinessException($"{field}: введите число без разделителей тысяч.");
        return result;
    }
    public static void Profile(ProfileInput p)
    {
        if (!Enum.IsDefined(p.Sex) || !Enum.IsDefined(p.ActivityLevel)) throw new BusinessException("Выберите пол и активность.");
        Number(p.Age, 18, 100, 0, "Возраст"); Number(p.HeightCm, 100, 250, 2, "Рост"); Number(p.WeightKg, 30, 300, 2, "Вес");
        Number(p.WeeklyBudget, 0, 1000000, 4, "Бюджет");
    }
    public static void Product(Product p)
    {
        p.Name = Text(p.Name, "Название продукта", 100); p.Category = Text(p.Category, "Категория", 60); p.NormalizedName = Normalize(p.Name);
        Number(p.CaloriesPer100g, 0, 1000, 2, "Ккал"); Number(p.ProteinPer100g, 0, 100, 2, "Белки");
        Number(p.FatPer100g, 0, 100, 2, "Жиры"); Number(p.CarbsPer100g, 0, 100, 2, "Углеводы");
        Number(p.PricePer100g, 0, 10000, 4, "Цена");
        if (p.ProteinPer100g + p.FatPer100g + p.CarbsPer100g > 100) throw new BusinessException("Сумма БЖУ не может превышать 100 г.");
    }
    public static void Recipe(Recipe r)
    {
        r.Name = Text(r.Name, "Название рецепта", 120); r.Instructions = Text(r.Instructions, "Инструкция", 10000);
        if (r.DishCategory?.Length > 60) throw new BusinessException("Кулинарная категория: до 60 символов.");
        if (!Enum.IsDefined(r.MealType)) throw new BusinessException("Выберите прием пищи.");
        Number(r.CookingMinutes, 1, 1440, 0, "Время");
        if (r.Ingredients.Count is < 1 or > 50 || r.Ingredients.Select(i => i.ProductId).Distinct().Count() != r.Ingredients.Count)
            throw new BusinessException("Рецепт должен содержать от 1 до 50 разных продуктов.");
        foreach (var i in r.Ingredients) Number(i.QuantityGrams, 0.01m, 100000, 2, "Вес ингредиента");
    }
    public static DateOnly Monday(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    public static string MealName(MealType type) => type switch { MealType.Breakfast => "Завтрак", MealType.Lunch => "Обед", _ => "Ужин" };
    public static string Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.GetCultureInfo("ru-RU"));
}

public static class NutritionCalculator
{
    public static decimal Factor(ActivityLevel activity) => activity switch
    {
        ActivityLevel.Sedentary => 1.2m,
        ActivityLevel.Light => 1.375m,
        ActivityLevel.Moderate => 1.55m,
        ActivityLevel.Active => 1.725m,
        ActivityLevel.VeryActive => 1.9m,
        _ => throw new BusinessException("Неверная активность.")
    };
    public static NutritionTargets Calculate(ProfileInput p)
    {
        Rules.Profile(p);
        var bmr = 10 * p.WeightKg + 6.25m * p.HeightCm - 5 * p.Age + (p.Sex == Sex.Male ? 5 : -161);
        return FromCalories(bmr * Factor(p.ActivityLevel)) with { Bmr = bmr };
    }
    public static NutritionTargets FromCalories(decimal k) => new(0, k, k * 0.3m / 4, k * 0.3m / 9, k * 0.4m / 4);
    public static ProfileInput FromUser(User u)
    {
        if (u.Sex is null || u.Age is null || u.HeightCm is null || u.WeightKg is null || u.ActivityLevel is null)
            throw new BusinessException("Сначала заполните и сохраните профиль.");
        return new(u.Sex.Value, u.Age.Value, u.HeightCm.Value, u.WeightKg.Value, u.ActivityLevel.Value, u.WeeklyBudget);
    }
}

public static class RecipeCalculator
{
    public static List<MenuIngredientSnapshot> Snapshot(Recipe recipe) => recipe.Ingredients.Select(i => new MenuIngredientSnapshot
    {
        ProductId = i.ProductId,
        ProductNameSnapshot = i.Product.Name,
        CategorySnapshot = i.Product.Category,
        QuantityGrams = i.QuantityGrams,
        CaloriesPer100g = i.Product.CaloriesPer100g,
        ProteinPer100g = i.Product.ProteinPer100g,
        FatPer100g = i.Product.FatPer100g,
        CarbsPer100g = i.Product.CarbsPer100g,
        PricePer100g = i.Product.PricePer100g
    }).ToList();
    public static Nutrition Calculate(Recipe recipe) => Calculate(Snapshot(recipe));
    public static Nutrition Calculate(IEnumerable<MenuIngredientSnapshot> ingredients) => ingredients.Aggregate(Nutrition.Zero, (n, i) => n + new Nutrition(
        i.QuantityGrams / 100 * i.CaloriesPer100g, i.QuantityGrams / 100 * i.ProteinPer100g, i.QuantityGrams / 100 * i.FatPer100g,
        i.QuantityGrams / 100 * i.CarbsPer100g, i.QuantityGrams / 100 * i.PricePer100g));
    public static WeeklyMenu Meal(Recipe recipe, DateOnly date) => new()
    {
        Date = date,
        MealType = recipe.MealType,
        RecipeId = recipe.Id,
        RecipeNameSnapshot = recipe.Name,
        InstructionsSnapshot = recipe.Instructions,
        Ingredients = Snapshot(recipe)
    };
}

/// <summary>Pure FEFO allocator. Input lots are never mutated; the returned remainder can be used by the next slot.</summary>
public static class PantryAllocator
{
    public static (List<Lot> Remaining, List<Requirement> Missing) Allocate(IEnumerable<Lot> source, IEnumerable<Requirement> needs, DateOnly date)
    {
        var lots = source.OrderBy(l => l.Expiry ?? DateOnly.MaxValue).ThenBy(l => l.Id).ToList();
        var missing = new List<Requirement>();
        foreach (var need in needs.GroupBy(n => n.ProductId).Select(g => new Requirement(g.Key, g.Sum(n => n.Grams))))
        {
            var rest = need.Grams;
            for (var i = 0; i < lots.Count && rest > 0; i++)
            {
                var lot = lots[i];
                if (lot.ProductId != need.ProductId || lot.Expiry < date) continue;
                var take = Math.Min(rest, lot.Quantity); rest -= take; lots[i] = lot with { Quantity = lot.Quantity - take };
            }
            if (rest > 0) missing.Add(new(need.ProductId, rest));
        }
        return (lots, missing);
    }
    public static List<Requirement> Needs(IEnumerable<MenuIngredientSnapshot> ingredients) => ingredients
        .GroupBy(i => i.ProductId).Select(g => new Requirement(g.Key, g.Sum(i => i.QuantityGrams))).ToList();
}

public static class PlanAnalysis
{
    public static List<string> Warnings(MenuPlan p)
    {
        var warnings = new List<string>();
        foreach (var day in p.Meals.GroupBy(m => m.Date))
        {
            var k = RecipeCalculator.Calculate(day.SelectMany(m => m.Ingredients)).Calories;
            if (k < p.DailyCalorieTarget * 0.9m || k > p.DailyCalorieTarget * 1.1m)
                warnings.Add($"{day.Key:dd.MM}: {k:F2} ккал при допустимых {p.DailyCalorieTarget * 0.9m:F2}–{p.DailyCalorieTarget * 1.1m:F2}.");
        }
        var cost = RecipeCalculator.Calculate(p.Meals.SelectMany(m => m.Ingredients)).Cost;
        if (cost > p.Budget) warnings.Add($"Стоимость {Rules.Money(cost)} BYN превышает бюджет {Rules.Money(p.Budget)} BYN.");
        return warnings;
    }
}
