using HomeMenu.Core;
using Microsoft.EntityFrameworkCore;

namespace HomeMenu.Infrastructure;

internal static class SeedData
{
    // Stable names form the idempotency keys. No users or passwords are seeded.
    public static async Task ApplyAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync()) return;
        await using var tx = await db.Database.BeginTransactionAsync();
        Product P(string name, string category, decimal k, decimal p, decimal f, decimal c, decimal price)
        {
            var product = new Product { Name = name, Category = category, CaloriesPer100g = k, ProteinPer100g = p, FatPer100g = f, CarbsPer100g = c, PricePer100g = price };
            Rules.Product(product); db.Products.Add(product); return product;
        }
        var oats = P("Овсяные хлопья", "Крупы", 380, 13, 7, 68, 0.40m);
        var milk = P("Молоко", "Молочные", 60, 3, 3.2m, 4.7m, 0.15m);
        var banana = P("Банан", "Фрукты", 89, 1.1m, 0.3m, 23, 0.35m);
        var egg = P("Яйцо", "Белковые", 157, 12.7m, 11, 0.7m, 0.65m);
        var bread = P("Хлеб цельнозерновой", "Хлеб", 230, 8, 3, 43, 0.30m);
        var cheese = P("Творог", "Молочные", 120, 18, 4, 3, 0.75m);
        var rice = P("Рис сухой", "Крупы", 340, 7, 1, 75, 0.28m);
        var chicken = P("Куриное филе", "Мясо", 113, 23, 2, 0, 0.95m);
        var oil = P("Масло растительное", "Масла", 900, 0, 100, 0, 0.70m);
        var tomato = P("Помидор", "Овощи", 20, 1, 0.2m, 4, 0.40m);
        var buckwheat = P("Гречка сухая", "Крупы", 330, 12, 3, 62, 0.30m);
        var beef = P("Говядина", "Мясо", 187, 19, 12, 0, 1.60m);
        var pasta = P("Макароны сухие", "Крупы", 350, 12, 1.5m, 72, 0.32m);
        var fish = P("Рыба белая", "Рыба", 90, 18, 2, 0, 1.20m);
        var potato = P("Картофель", "Овощи", 77, 2, 0.4m, 16, 0.12m);
        var carrot = P("Морковь", "Овощи", 35, 1.3m, 0.1m, 7, 0.10m);
        var apple = P("Яблоко", "Фрукты", 52, 0.3m, 0.2m, 14, 0.22m);
        await db.SaveChangesAsync();
        void R(string name, MealType meal, int minutes, string instructions, params (Product p, decimal q)[] ingredients)
        {
            db.Recipes.Add(new Recipe
            {
                Name = name,
                MealType = meal,
                CookingMinutes = minutes,
                DishCategory = "Домашняя кухня",
                Instructions = instructions,
                Ingredients = ingredients.Select(x => new Ingredient { ProductId = x.p.Id, QuantityGrams = x.q }).ToList()
            });
        }
        R("Овсянка с бананом", MealType.Breakfast, 15, "1. Сварите хлопья в молоке.\n2. Добавьте нарезанный банан.", (oats, 100), (milk, 200), (banana, 120));
        R("Омлет с тостами", MealType.Breakfast, 15, "1. Взбейте яйца с молоком.\n2. Приготовьте на масле. Подайте с хлебом и помидором.", (egg, 150), (milk, 100), (bread, 100), (tomato, 100), (oil, 5));
        R("Творог с яблоком и хлопьями", MealType.Breakfast, 10, "1. Измельчите яблоко.\n2. Смешайте творог с молоком, хлопьями и яблоком.", (cheese, 200), (apple, 150), (oats, 60), (milk, 100));
        R("Курица с рисом", MealType.Lunch, 35, "1. Отварите рис.\n2. Потушите курицу с морковью на масле.\n3. Подайте с помидорами.", (rice, 140), (chicken, 180), (oil, 12), (carrot, 100), (tomato, 100));
        R("Гречка с говядиной", MealType.Lunch, 45, "1. Сварите гречку.\n2. Потушите говядину с овощами и маслом.", (buckwheat, 140), (beef, 180), (oil, 10), (carrot, 100), (tomato, 100));
        R("Макароны с курицей", MealType.Lunch, 30, "1. Отварите макароны.\n2. Приготовьте курицу с помидорами на масле.\n3. Соедините.", (pasta, 140), (chicken, 180), (oil, 12), (tomato, 150));
        R("Рыба с картофелем", MealType.Dinner, 40, "1. Запеките рыбу и картофель с маслом.\n2. Подайте с овощами и хлебом.", (fish, 200), (potato, 350), (oil, 15), (tomato, 100), (bread, 50));
        R("Гречка с яйцом", MealType.Dinner, 25, "1. Отварите гречку и яйца.\n2. Нарежьте овощи, заправьте маслом. Подайте вместе.", (buckwheat, 100), (egg, 150), (oil, 10), (tomato, 150));
        R("Рис с рыбой и овощами", MealType.Dinner, 30, "1. Сварите рис.\n2. Потушите рыбу с морковью на масле. Подайте с помидором.", (rice, 110), (fish, 200), (carrot, 100), (oil, 12), (tomato, 100));
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
