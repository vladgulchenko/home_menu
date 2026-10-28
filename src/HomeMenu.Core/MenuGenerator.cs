namespace HomeMenu.Core;

public enum SearchStatus { Success, NoCandidates, NoSolution, SearchLimitReached, Cancelled }
public record SearchResult(SearchStatus Status, IReadOnlyList<WeeklyMenu> Meals, int VisitedNodes, int Backtracks, string Message);
public interface IMenuGenerator
{
    SearchResult Generate(DateOnly week, DateOnly today, decimal target, decimal budget, IReadOnlyList<Recipe> recipes,
        IReadOnlySet<int> excluded, IReadOnlyList<Lot> pantry, int maxVisitedNodes = 100000, CancellationToken cancellationToken = default);
}
public sealed class MenuGenerator(Action<int>? onVisited = null) : IMenuGenerator
{
    public SearchResult Generate(DateOnly week, DateOnly today, decimal target, decimal budget, IReadOnlyList<Recipe> recipes,
        IReadOnlySet<int> excluded, IReadOnlyList<Lot> pantry, int maxVisitedNodes = 100000, CancellationToken cancellationToken = default)
    {
        if (week.DayOfWeek != DayOfWeek.Monday || target <= 0 || budget < 0 || maxVisitedNodes < 0) throw new BusinessException("Некорректные параметры генератора.");
        var candidates = Enum.GetValues<MealType>().ToDictionary(t => t, t => recipes.Where(r => r.MealType == t && r.Ingredients.All(i => !excluded.Contains(i.ProductId))).ToArray());
        var visited = 0; var backtracks = 0; var limit = false;
        var selected = new List<WeeklyMenu>();
        var uses = new Dictionary<int, int>();
        var values = recipes.ToDictionary(r => r.Id, RecipeCalculator.Calculate);
        var needs = recipes.ToDictionary(r => r.Id, r => PantryAllocator.Needs(RecipeCalculator.Snapshot(r)));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var pair in candidates) if (pair.Value.Length == 0) return Result(SearchStatus.NoCandidates, $"Нет допустимых рецептов: {Rules.MealName(pair.Key)}.");
            bool Search(int slot, decimal cost, decimal dayCalories, List<Lot> stocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (slot == 21) return true;
                var type = (MealType)(slot % 3); var date = week.AddDays(slot / 3); var useDate = date < today ? today : date;
                decimal Coverage(Recipe r)
                {
                    var n = needs[r.Id]; var missing = PantryAllocator.Allocate(stocks, n, useDate).Missing.Sum(m => m.Grams);
                    return 1 - missing / n.Sum(x => x.Grams);
                }
                var ordered = candidates[type].OrderByDescending(Coverage).ThenBy(r => uses.GetValueOrDefault(r.Id))
                    .ThenBy(r => Math.Abs(values[r.Id].Calories - target * (type == MealType.Lunch ? 0.4m : 0.3m)))
                    .ThenBy(r => values[r.Id].Cost).ThenBy(r => r.Id);
                foreach (var r in ordered)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (visited >= maxVisitedNodes) { limit = true; return false; }
                    visited++;
                    onVisited?.Invoke(visited);
                    var nextCost = cost + values[r.Id].Cost; var nextK = dayCalories + values[r.Id].Calories;
                    if (nextCost > budget || nextK > target * 1.1m || (type == MealType.Dinner && nextK < target * 0.9m)) { backtracks++; continue; }
                    // Safe minimum-cost bound; it never excludes a feasible completion.
                    var minRest = Enumerable.Range(slot + 1, 20 - slot).Sum(s => candidates[(MealType)(s % 3)].Min(x => values[x.Id].Cost));
                    if (nextCost + minRest > budget) { backtracks++; continue; }
                    var remaining = PantryAllocator.Allocate(stocks, needs[r.Id], useDate).Remaining;
                    selected.Add(RecipeCalculator.Meal(r, date)); uses[r.Id] = uses.GetValueOrDefault(r.Id) + 1;
                    if (Search(slot + 1, nextCost, type == MealType.Dinner ? 0 : nextK, remaining)) return true;
                    selected.RemoveAt(selected.Count - 1); uses[r.Id]--; backtracks++;
                    if (limit) return false;
                }
                return false;
            }
            if (Search(0, 0, 0, pantry.ToList())) return Result(SearchStatus.Success, "Меню подобрано.");
            return limit ? Result(SearchStatus.SearchLimitReached, "За установленное число попыток меню не найдено. Ослабьте ограничения или увеличьте лимит поиска.")
                : Result(SearchStatus.NoSolution, "С заданными ограничениями подходящего меню нет.");
        }
        catch (OperationCanceledException) { return Result(SearchStatus.Cancelled, "Подбор отменен."); }
        SearchResult Result(SearchStatus status, string message) => new(status, status == SearchStatus.Success ? selected.ToArray() : [], visited, backtracks, message);
    }
}
