namespace HomeMenu.Core;

public enum Sex { Male, Female }
public enum ActivityLevel { Sedentary, Light, Moderate, Active, VeryActive }
public enum MealType { Breakfast, Lunch, Dinner }

public sealed class User
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Login { get; set; } = "";
    public string NormalizedLogin { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Sex? Sex { get; set; }
    public int? Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public ActivityLevel? ActivityLevel { get; set; }
    public decimal WeeklyBudget { get; set; }
}

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal PricePer100g { get; set; }
    public override string ToString() => Name;
}

public sealed class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public MealType MealType { get; set; }
    public string? DishCategory { get; set; }
    public string Instructions { get; set; } = "";
    public int CookingMinutes { get; set; }
    public List<Ingredient> Ingredients { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class Ingredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal QuantityGrams { get; set; }
}

public sealed class PantryStock
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal QuantityGrams { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public sealed class UserExcludedProduct
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
}

public sealed class MenuPlan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public decimal DailyCalorieTarget { get; set; }
    public decimal ProteinTargetGrams { get; set; }
    public decimal FatTargetGrams { get; set; }
    public decimal CarbsTargetGrams { get; set; }
    public decimal Budget { get; set; }
    public string ExcludedProductIdsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
    public int Version { get; set; }
    public List<WeeklyMenu> Meals { get; set; } = [];
}

public sealed class WeeklyMenu
{
    public int Id { get; set; }
    public int MenuPlanId { get; set; }
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public int RecipeId { get; set; }
    public string RecipeNameSnapshot { get; set; } = "";
    public string InstructionsSnapshot { get; set; } = "";
    public DateTime? CookedAtUtc { get; set; }
    public List<MenuIngredientSnapshot> Ingredients { get; set; } = [];
}

public sealed class MenuIngredientSnapshot
{
    public int Id { get; set; }
    public int WeeklyMenuId { get; set; }
    public int ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = "";
    public string CategorySnapshot { get; set; } = "";
    public decimal QuantityGrams { get; set; }
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal PricePer100g { get; set; }
}

public sealed class BusinessException(string message) : Exception(message);
public sealed class ConfirmationRequiredException(string message, long? revision = null) : Exception(message)
{
    public long? Revision { get; } = revision;
}
public record ProfileInput(Sex Sex, int Age, decimal HeightCm, decimal WeightKg, ActivityLevel ActivityLevel, decimal WeeklyBudget);
public record Nutrition(decimal Calories, decimal Protein, decimal Fat, decimal Carbs, decimal Cost)
{
    public static Nutrition Zero => new(0, 0, 0, 0, 0);
    public static Nutrition operator +(Nutrition a, Nutrition b) => new(a.Calories + b.Calories, a.Protein + b.Protein, a.Fat + b.Fat, a.Carbs + b.Carbs, a.Cost + b.Cost);
}
public record NutritionTargets(decimal Bmr, decimal Calories, decimal Protein, decimal Fat, decimal Carbs);
public record Lot(int Id, int ProductId, decimal Quantity, DateOnly? Expiry);
public record Requirement(int ProductId, decimal Grams);
public record ShoppingItem(int ProductId, string Name, string Category, decimal Required, decimal Covered, decimal Missing, decimal Price)
{
    public decimal Cost => Missing / 100m * Price;
}
public record DaySummary(DateOnly Date, Nutrition Nutrition, decimal CalorieDeviation, decimal ProteinDeviation, decimal FatDeviation, decimal CarbsDeviation);
public record WeekSummary(Nutrition Planned, Nutrition Confirmed, int CookedCount, decimal BudgetRemaining, decimal ShoppingCost, IReadOnlyList<DaySummary> Days, IReadOnlyList<string> Warnings);

public interface IAuthService
{
    Task RegisterAsync(string login, string name, string password, string repeat);
    Task LoginAsync(string login, string password);
    void Logout();
}
public interface IProfileService
{
    Task<User> GetProfileAsync();
    Task SaveProfileAsync(ProfileInput input);
    Task<HashSet<int>> GetExcludedProductsAsync();
    Task SaveExcludedProductsAsync(IEnumerable<int> ids);
}
public interface ICatalogService
{
    Task<List<Product>> SearchProductsAsync(string name = "", string category = "");
    Task<int> SaveProductAsync(Product product);
    Task DeleteProductAsync(int id);
    Task<List<Recipe>> SearchRecipesAsync(string name = "", MealType? meal = null, int? ingredient = null, decimal? maxPrice = null);
    Task<int> SaveRecipeAsync(Recipe recipe);
    Task DeleteRecipeAsync(int id);
}
public interface IPantryService
{
    Task<List<PantryStock>> GetPantryAsync();
    Task<int> SaveLotAsync(int id, int productId, decimal grams, DateOnly? expiry);
    Task DeleteLotAsync(int id);
}
public interface IMenuService
{
    Task<MenuPlan?> GetWeekAsync(DateOnly week);
    Task<MenuPlan> GenerateWeekAsync(DateOnly week, decimal? calorieOverride, decimal budget, int? expectedVersion, bool replace, CancellationToken cancellationToken = default, int maxNodes = 100000);
    Task ReplaceMealAsync(int planId, int mealId, int recipeId, int version, bool acceptViolations = false, long? expectedRevision = null);
    Task CookMealAsync(int planId, int mealId, int version);
    Task<List<ShoppingItem>> BuildShoppingAsync(int planId);
    Task<WeekSummary> GetWeekSummaryAsync(int planId);
}
