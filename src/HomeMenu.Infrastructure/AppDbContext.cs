using HomeMenu.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HomeMenu.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<PantryStock> PantryStocks => Set<PantryStock>();
    public DbSet<UserExcludedProduct> UserExcludedProducts => Set<UserExcludedProduct>();
    public DbSet<MenuPlan> MenuPlans => Set<MenuPlan>();
    public DbSet<WeeklyMenu> WeeklyMenus => Set<WeeklyMenu>();
    public DbSet<MenuIngredientSnapshot> MenuIngredientSnapshots => Set<MenuIngredientSnapshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.NormalizedLogin).IsUnique();
        b.Entity<Product>().HasIndex(x => x.NormalizedName).IsUnique();
        b.Entity<Ingredient>().HasIndex(x => new { x.RecipeId, x.ProductId }).IsUnique();
        b.Entity<Ingredient>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Recipe>().HasMany(x => x.Ingredients).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PantryStock>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PantryStock>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PantryStock>().HasIndex(x => new { x.UserId, x.ProductId, x.ExpiryDate });
        b.Entity<UserExcludedProduct>().HasKey(x => new { x.UserId, x.ProductId });
        b.Entity<UserExcludedProduct>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserExcludedProduct>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MenuPlan>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MenuPlan>().HasIndex(x => new { x.UserId, x.WeekStart }).IsUnique();
        b.Entity<MenuPlan>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<MenuPlan>().HasMany(x => x.Meals).WithOne().HasForeignKey(x => x.MenuPlanId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<WeeklyMenu>().HasIndex(x => new { x.MenuPlanId, x.Date, x.MealType }).IsUnique();
        b.Entity<WeeklyMenu>().HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WeeklyMenu>().HasMany(x => x.Ingredients).WithOne().HasForeignKey(x => x.WeeklyMenuId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MenuIngredientSnapshot>().HasIndex(x => new { x.WeeklyMenuId, x.ProductId }).IsUnique();
        b.Entity<MenuIngredientSnapshot>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                if (t == typeof(decimal))
                {
                    var scale = entity.ClrType == typeof(MenuPlan) && prop.Name.Contains("Target") ? 100000000m
                        : prop.Name.Contains("Price") || prop.Name.Contains("Budget") ? 10000m : 100m;
                    prop.SetValueConverter(new ValueConverter<decimal, long>(v => ToScaled(v, scale), v => v / scale));
                    prop.SetColumnName(prop.Name + "Scaled");
                }
                if (t == typeof(DateTime)) prop.SetValueConverter(new ValueConverter<DateTime, DateTime>(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
            }
        }
        b.Entity<User>().ToTable("Users", t =>
        {
            t.HasCheckConstraint("CK_User_Profile", "(Sex IS NULL OR Sex IN (0,1)) AND (Age IS NULL OR Age BETWEEN 18 AND 100) AND (HeightCmScaled IS NULL OR HeightCmScaled BETWEEN 10000 AND 25000) AND (WeightKgScaled IS NULL OR WeightKgScaled BETWEEN 3000 AND 30000) AND (ActivityLevel IS NULL OR ActivityLevel BETWEEN 0 AND 4) AND WeeklyBudgetScaled BETWEEN 0 AND 10000000000");
        });
        b.Entity<Product>().ToTable("Products", t => t.HasCheckConstraint("CK_Product_Values", NutrientCheck() + " AND PricePer100gScaled BETWEEN 0 AND 100000000"));
        b.Entity<Recipe>().ToTable("Recipes", t => t.HasCheckConstraint("CK_Recipe_Values", "MealType IN (0,1,2) AND CookingMinutes BETWEEN 1 AND 1440"));
        b.Entity<Ingredient>().ToTable("Ingredients", t => t.HasCheckConstraint("CK_Ingredient_Grams", "QuantityGramsScaled BETWEEN 1 AND 10000000"));
        b.Entity<PantryStock>().ToTable("PantryStocks", t => t.HasCheckConstraint("CK_Stock_Grams", "QuantityGramsScaled BETWEEN 0 AND 1000000000"));
        b.Entity<MenuPlan>().ToTable("MenuPlans", t => t.HasCheckConstraint("CK_Plan_Values", "DailyCalorieTargetScaled > 0 AND ProteinTargetGramsScaled >= 0 AND FatTargetGramsScaled >= 0 AND CarbsTargetGramsScaled >= 0 AND BudgetScaled BETWEEN 0 AND 10000000000 AND Version > 0"));
        b.Entity<WeeklyMenu>().ToTable("WeeklyMenus", t => t.HasCheckConstraint("CK_Menu_Meal", "MealType IN (0,1,2)"));
        b.Entity<MenuIngredientSnapshot>().ToTable("MenuIngredientSnapshots", t => t.HasCheckConstraint("CK_Snapshot_Values", NutrientCheck() + " AND QuantityGramsScaled BETWEEN 1 AND 10000000 AND PricePer100gScaled BETWEEN 0 AND 100000000"));
    }
    private static string NutrientCheck() => "CaloriesPer100gScaled BETWEEN 0 AND 100000 AND ProteinPer100gScaled BETWEEN 0 AND 10000 AND FatPer100gScaled BETWEEN 0 AND 10000 AND CarbsPer100gScaled BETWEEN 0 AND 10000 AND ProteinPer100gScaled + FatPer100gScaled + CarbsPer100gScaled <= 10000";
    private static long ToScaled(decimal value, decimal scale)
    {
        var scaled = value * scale;
        if (decimal.Truncate(scaled) != scaled) throw new BusinessException("Лишняя точность числа при сохранении.");
        return checked((long)scaled);
    }
}

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=homemenu-design.db;Foreign Keys=True").Options);
}
