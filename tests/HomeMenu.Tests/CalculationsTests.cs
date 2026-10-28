using HomeMenu.Core;
using Xunit;

namespace HomeMenu.Tests;

public sealed class CalculationsTests
{
    [Fact]
    public void N1_ProfileFormulaAndAllActivities()
    {
        var p = new ProfileInput(Sex.Male, 20, 175, 70, ActivityLevel.Moderate, 150);
        var n = NutritionCalculator.Calculate(p);
        Assert.Equal(new NutritionTargets(1698.75m, 2633.0625m, 197.4796875m, 87.76875m, 263.30625m), n);
        var female = NutritionCalculator.Calculate(p with { Sex = Sex.Female }); Assert.Equal(1532.75m, female.Bmr); Assert.Equal(2375.7625m, female.Calories);
        foreach (var a in Enum.GetValues<ActivityLevel>()) Assert.Equal(1698.75m * NutritionCalculator.Factor(a), NutritionCalculator.Calculate(p with { ActivityLevel = a }).Calories);
        Assert.Throws<BusinessException>(() => NutritionCalculator.Calculate(p with { Age = 17 }));
        Assert.Throws<BusinessException>(() => NutritionCalculator.Calculate(p with { WeightKg = 301 }));
        Assert.Throws<BusinessException>(() => NutritionCalculator.FromUser(new User()));
    }
    [Fact]
    public void N2_RecipeUsesOnePortionAndExactMoney()
    {
        var r = OatRecipe(); var n = RecipeCalculator.Calculate(r);
        Assert.Equal(new Nutrition(500, 19, 13.4m, 77.4m, 0.70m), n);
        Assert.Equal("0,01", Rules.Money(0.004m + 0.004m)); Assert.Equal("0,00", Rules.Money(0.004m));
    }
    [Theory]
    [InlineData("1,5", "1.5")]
    [InlineData("1.5", "1.5")]
    public void DecimalParsingIsUnambiguous(string text, string expected) => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Rules.Parse(text, "Число"));
    [Theory]
    [InlineData("1,000.2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1 500")]
    public void InvalidNumbersRejected(string text) => Assert.Throws<BusinessException>(() => Rules.Parse(text, "Число"));
    [Fact]
    public void ValidationRejectsPrecisionDuplicateIngredientsAndInvalidMacros()
    {
        var p = OatRecipe().Ingredients[0].Product; p.PricePer100g = 0.12345m; Assert.Throws<BusinessException>(() => Rules.Product(p));
        p.PricePer100g = 1; p.ProteinPer100g = 90; Assert.Throws<BusinessException>(() => Rules.Product(p));
        var r = OatRecipe(); r.Ingredients.Add(r.Ingredients[0]); Assert.Throws<BusinessException>(() => Rules.Recipe(r));
        r.Ingredients.Clear(); Assert.Throws<BusinessException>(() => Rules.Recipe(r));
        Assert.Equal("МОЛОКО СВЕЖЕЕ", Rules.Normalize("  молоко   свежее  "));
    }
    [Fact]
    public void N4_FefoHonorsDatesAndDoesNotMutateLots()
    {
        var today = new DateOnly(2026, 11, 2); var lots = new List<Lot> { new(1, 1, 100, today), new(2, 1, 150, today.AddDays(8)) };
        var first = PantryAllocator.Allocate(lots, [new(1, 80)], today);
        var second = PantryAllocator.Allocate(first.Remaining, [new(1, 200)], today.AddDays(1));
        Assert.Empty(first.Missing); Assert.Equal(50, Assert.Single(second.Missing).Grams); Assert.Equal(0.20m, 50 / 100m * 0.4m);
        Assert.Equal(100, lots[0].Quantity); Assert.Equal(150, lots[1].Quantity);
        var same = PantryAllocator.Allocate([new(3, 1, 100, null), new(2, 1, 100, today), new(1, 1, 100, today)], [new(1, 150)], today);
        Assert.Equal(0, same.Remaining.Single(l => l.Id == 1).Quantity); Assert.Equal(50, same.Remaining.Single(l => l.Id == 2).Quantity); Assert.Equal(100, same.Remaining.Single(l => l.Id == 3).Quantity);
    }
    internal static Recipe OatRecipe() => new()
    {
        Id = 1,
        Name = "Каша",
        MealType = MealType.Breakfast,
        CookingMinutes = 15,
        Instructions = "Сварить",
        Ingredients =
    [new(){ProductId=1,QuantityGrams=100,Product=new(){Id=1,Name="Овсянка",Category="Крупы",CaloriesPer100g=380,ProteinPer100g=13,FatPer100g=7,CarbsPer100g=68,PricePer100g=0.4m}},
     new(){ProductId=2,QuantityGrams=200,Product=new(){Id=2,Name="Молоко",Category="Молочные",CaloriesPer100g=60,ProteinPer100g=3,FatPer100g=3.2m,CarbsPer100g=4.7m,PricePer100g=0.15m}}]
    };
}
