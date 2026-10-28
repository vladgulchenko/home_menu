using HomeMenu.Core;
using Xunit;

namespace HomeMenu.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void CancellationDuringActualTreeTraversalReturnsNoPartialMenu()
    {
        using var cts = new CancellationTokenSource();
        var generator = new MenuGenerator(n => { if (n == 10) cts.Cancel(); });
        var result = generator.Generate(Week, Week, 2000, 14, Fixture(), new HashSet<int>(), [], cancellationToken: cts.Token);
        Assert.Equal(SearchStatus.Cancelled, result.Status);
        Assert.Equal(10, result.VisitedNodes);
        Assert.Empty(result.Meals);
    }
    private static readonly DateOnly Week = new(2026, 11, 2);
    internal static List<Recipe> Fixture()
    {
        var p = new Product { Id = 1, Name = "Синтетический", Category = "Тест", CaloriesPer100g = 200, ProteinPer100g = 15, FatPer100g = 6.67m, CarbsPer100g = 20, PricePer100g = 0.2m };
        return Enum.GetValues<MealType>().Select(t => new Recipe { Id = (int)t + 1, Name = t.ToString(), MealType = t, CookingMinutes = 1, Instructions = "Тест", Ingredients = [new Ingredient { ProductId = 1, Product = p, QuantityGrams = t == MealType.Lunch ? 400 : 300 }] }).ToList();
    }
    private static SearchResult Generate(decimal target = 2000, decimal budget = 14, List<Recipe>? recipes = null, int max = 100000, IReadOnlySet<int>? excluded = null, CancellationToken token = default, List<Lot>? pantry = null) =>
        new MenuGenerator().Generate(Week, Week, target, budget, recipes ?? Fixture(), excluded ?? new HashSet<int>(), pantry ?? [], max, token);
    [Fact]
    public void N3_ExactBudget21SlotsAndNoStockRequired()
    {
        var result = Generate(); Assert.Equal(SearchStatus.Success, result.Status); Assert.Equal(21, result.Meals.Count);
        Assert.Equal(14, RecipeCalculator.Calculate(result.Meals.SelectMany(m => m.Ingredients)).Cost);
        Assert.Equal(7000, result.Meals.SelectMany(m => m.Ingredients).Sum(i => i.QuantityGrams));
        foreach (var day in result.Meals.GroupBy(m => m.Date)) { Assert.Equal(3, day.Count()); Assert.Equal(2000, RecipeCalculator.Calculate(day.SelectMany(m => m.Ingredients)).Calories); Assert.Equal(3, day.Select(m => m.MealType).Distinct().Count()); }
        Assert.Equal(SearchStatus.NoSolution, Generate(budget: 13.9999m).Status);
    }
    [Theory]
    [InlineData(180, SearchStatus.Success)]
    [InlineData(220, SearchStatus.Success)]
    [InlineData(179.99, SearchStatus.NoSolution)]
    [InlineData(220.01, SearchStatus.NoSolution)]
    public void DailyCalorieBoundsAreInclusive(double calories, SearchStatus status)
    {
        var recipes = Fixture(); foreach (var r in recipes) r.Ingredients[0].Product.CaloriesPer100g = (decimal)calories;
        Assert.Equal(status, Generate(recipes: recipes).Status);
    }
    [Fact]
    public void N3_ActualBacktrackFromPreferred900CalorieBreakfast()
    {
        var recipes = Fixture(); var alternative = new Recipe
        {
            Id = 4,
            MealType = MealType.Breakfast,
            Name = "Предпочтительный",
            Instructions = "Тест",
            CookingMinutes = 1,
            Ingredients = [new() { ProductId = 2, QuantityGrams = 300, Product = new() { Id = 2, Name = "Другой", CaloriesPer100g = 300, PricePer100g = 0.2m } }]
        };
        recipes.Add(alternative);
        var result = Generate(recipes: recipes, pantry: [new(1, 2, 10000, null)]);
        Assert.Equal(SearchStatus.Success, result.Status); Assert.True(result.Backtracks > 0); Assert.True(result.VisitedNodes > 21);
        Assert.DoesNotContain(result.Meals, m => m.RecipeId == 4);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(20)]
    public void SearchLimitIsExact(int limit)
    {
        var result = Generate(max: limit); Assert.Equal(SearchStatus.SearchLimitReached, result.Status); Assert.Equal(limit, result.VisitedNodes);
    }
    [Fact]
    public void DistinguishesExclusionsNoSolutionAndCancellation()
    {
        Assert.Equal(SearchStatus.NoCandidates, Generate(excluded: new HashSet<int> { 1 }).Status);
        Assert.Equal(SearchStatus.NoSolution, Generate(target: 3000).Status);
        Assert.Equal(SearchStatus.Cancelled, Generate(token: new CancellationToken(true)).Status);
    }
    [Fact]
    public void DeterministicPriorityUsesRemainingSuitableLots()
    {
        var recipes = Fixture(); var first = Generate(recipes: recipes); var second = Generate(recipes: recipes);
        Assert.Equal(first.Meals.Select(m => m.RecipeId), second.Meals.Select(m => m.RecipeId));
        Assert.Equal(first.VisitedNodes, second.VisitedNodes);
    }
}
