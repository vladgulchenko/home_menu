using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using HomeMenu.Core;
using HomeMenu.Infrastructure;

namespace HomeMenu.Wpf;

public sealed class ProductOption(Product product) : ObservableObject
{
    public Product Product { get; } = product;
    private bool excluded;
    public bool Excluded { get => excluded; set => Set(ref excluded, value); }
}
public record RecipeDisplay(Recipe Recipe)
{
    public string Name => Recipe.Name;
    public string Meal => Rules.MealName(Recipe.MealType);
    public Nutrition Nutrition => RecipeCalculator.Calculate(Recipe);
    public string Details => $"{Name} • одна порция • {Recipe.CookingMinutes} мин\n{Nutrition.Calories:F2} ккал; Б {Nutrition.Protein:F2} / Ж {Nutrition.Fat:F2} / У {Nutrition.Carbs:F2} г; {Rules.Money(Nutrition.Cost)} BYN\n\n"
        + string.Join("\n", Recipe.Ingredients.Select(i => $"{i.Product.Name}: {i.QuantityGrams:F2} г")) + "\n\n" + Recipe.Instructions;
}
public record PantryDisplay(PantryStock Stock, DateOnly Today)
{
    public string Name => Stock.Product.Name;
    public decimal Grams => Stock.QuantityGrams;
    public string Expiry => Stock.ExpiryDate?.ToString("dd.MM.yyyy") ?? "Не задана";
    public string State => Stock.ExpiryDate < Today ? "ПРОСРОЧЕНО" : "Пригодно сегодня";
    public decimal Price => Stock.Product.PricePer100g;
}
public record MealDisplay(WeeklyMenu Meal, DateOnly Today, HashSet<int> Excluded)
{
    public Nutrition Nutrition => RecipeCalculator.Calculate(Meal.Ingredients);
    public string Caption => $"{Meal.RecipeNameSnapshot}\n{Nutrition.Calories:F0} ккал · {Rules.Money(Nutrition.Cost)} BYN\nБ {Nutrition.Protein:F1} / Ж {Nutrition.Fat:F1} / У {Nutrition.Carbs:F1} г\n{State}";
    public string State => Meal.CookedAtUtc is { } time ? $"Приготовлено {time.ToLocalTime():dd.MM HH:mm}" : Meal.Ingredients.Any(i => Excluded.Contains(i.ProductId)) ? "Содержит исключение" : Meal.Date > Today ? "Запланировано" : "Можно приготовить";
    public string Details => $"{Meal.Date:dd.MM.yyyy} • {Rules.MealName(Meal.MealType)} • {Meal.RecipeNameSnapshot}\n{State}\n\n"
        + string.Join("\n", Meal.Ingredients.Select(i => $"{i.ProductNameSnapshot}: {i.QuantityGrams:F2} г")) + "\n\n" + Meal.InstructionsSnapshot;
    public bool CanCook => Meal.CookedAtUtc == null && Meal.Date <= Today && !Meal.Ingredients.Any(i => Excluded.Contains(i.ProductId));
}
public record MenuDay(DateOnly Date, MealDisplay Breakfast, MealDisplay Lunch, MealDisplay Dinner)
{
    public string Label => Date.ToString("dddd\ndd.MM", CultureInfo.GetCultureInfo("ru-RU"));
}

public sealed class MainViewModel : ObservableObject
{
    private readonly HomeMenuService service;
    private readonly UserSession session;
    private readonly DatabaseRuntime runtime;
    private readonly IDialogService dialogs;
    private readonly Action logout;
    private CancellationTokenSource? searchCancellation;
    private bool busy;
    private string status = "";
    private string profileText = "";
    private string summaryText = "";
    private string warnings = "";
    private DateTime? week;
    private MenuPlan? plan;
    private MealDisplay? selectedMeal;
    private RecipeDisplay? selectedRecipe;
    private string target = "2200";
    private string budget = "150";
    private bool useCustomTarget = true;
    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<RecipeDisplay> Recipes { get; } = [];
    public ObservableCollection<PantryDisplay> Pantry { get; } = [];
    public ObservableCollection<ProductOption> Preferences { get; } = [];
    public ObservableCollection<MenuDay> Days { get; } = [];
    public ObservableCollection<ShoppingItem> Shopping { get; } = [];
    public ObservableCollection<DaySummary> Statistics { get; } = [];
    private List<Product> allProducts = [];
    public List<Choice> MealFilters { get; } = [new(-1, "Все приемы"), new(0, "Завтрак"), new(1, "Обед"), new(2, "Ужин")];
    public Choice? MealFilter { get; set; }
    public Product? IngredientFilter { get; set; }
    public string ProductSearch { get; set; } = "";
    public string CategorySearch { get; set; } = "";
    public string RecipeSearch { get; set; } = "";
    public string MaxPrice { get; set; } = "";
    public Product? SelectedProduct { get; set; }
    public PantryDisplay? SelectedStock { get; set; }
    public RecipeDisplay? SelectedRecipe { get => selectedRecipe; set { Set(ref selectedRecipe, value); Changed(nameof(RecipeDetails)); } }
    public string RecipeDetails => SelectedRecipe?.Details ?? "Выберите рецепт, чтобы увидеть состав одной порции и инструкцию.";
    public MealDisplay? SelectedMeal { get => selectedMeal; set { Set(ref selectedMeal, value); Changed(nameof(MealDetails)); CommandManager.InvalidateRequerySuggested(); } }
    public string MealDetails => SelectedMeal?.Details ?? "Выберите блюдо в сетке недели, чтобы увидеть его состав и инструкцию.";
    public string UserName => session.Require().DisplayName;
    public string ProfileText { get => profileText; private set => Set(ref profileText, value); }
    public string SummaryText { get => summaryText; private set => Set(ref summaryText, value); }
    public string Warnings { get => warnings; private set => Set(ref warnings, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public bool Busy { get => busy; private set { Set(ref busy, value); Changed(nameof(Idle)); CommandManager.InvalidateRequerySuggested(); } }
    public bool Idle => !Busy;
    public bool IsSearching => searchCancellation != null;
    public DateTime? Week { get => week; set => Set(ref week, value); }
    public string Target { get => target; set => Set(ref target, value); }
    public string Budget { get => budget; set => Set(ref budget, value); }
    public bool UseCustomTarget { get => useCustomTarget; set => Set(ref useCustomTarget, value); }
    public string PlanHeader => plan == null ? "На эту неделю меню пока не создано." : $"Цель {plan.DailyCalorieTarget:F2} ккал/день · бюджет {Rules.Money(plan.Budget)} BYN · полная стоимость {Rules.Money(RecipeCalculator.Calculate(plan.Meals.SelectMany(m => m.Ingredients)).Cost)} BYN";
    public string ShoppingHeader { get; private set; } = "Сначала создайте меню недели.";
    public AsyncCommand Refresh { get; }
    public AsyncCommand EditProfile { get; }
    public AsyncCommand SavePreferences { get; }
    public AsyncCommand SearchProducts { get; }
    public AsyncCommand AddProduct { get; }
    public AsyncCommand EditProduct { get; }
    public AsyncCommand DeleteProduct { get; }
    public AsyncCommand SearchRecipes { get; }
    public AsyncCommand ResetRecipeFilters { get; }
    public AsyncCommand AddRecipe { get; }
    public AsyncCommand EditRecipe { get; }
    public AsyncCommand DeleteRecipe { get; }
    public AsyncCommand AddLot { get; }
    public AsyncCommand EditLot { get; }
    public AsyncCommand DeleteLot { get; }
    public AsyncCommand LoadWeek { get; }
    public AsyncCommand Generate { get; }
    public AsyncCommand Replace { get; }
    public AsyncCommand Cook { get; }
    public RelayCommand CancelSearch { get; }
    public RelayCommand SelectMeal { get; }
    public RelayCommand Logout { get; }
    public MainViewModel(HomeMenuService service, UserSession session, DatabaseRuntime runtime, IDialogService dialogs, Action logout)
    {
        this.service = service; this.session = session; this.runtime = runtime; this.dialogs = dialogs; this.logout = logout;
        week = Rules.Monday(runtime.Today).ToDateTime(TimeOnly.MinValue); MealFilter = MealFilters[0];
        AsyncCommand Command(Func<Task> action, Func<bool>? can = null) => new(() => Run(action), () => Idle && (can?.Invoke() ?? true));
        Refresh = Command(LoadAll); EditProfile = Command(EditProfileAsync); SavePreferences = Command(SavePreferencesAsync);
        SearchProducts = Command(LoadProducts); AddProduct = Command(() => ProductEditor(null)); EditProduct = Command(() => ProductEditor(SelectedProduct), () => SelectedProduct != null);
        DeleteProduct = Command(async () => { if (dialogs.Confirm($"Удалить продукт «{SelectedProduct!.Name}»?")) { await service.DeleteProductAsync(SelectedProduct.Id); await LoadAll(); } }, () => SelectedProduct != null);
        SearchRecipes = Command(LoadRecipes); ResetRecipeFilters = Command(async () => { RecipeSearch = ""; MaxPrice = ""; MealFilter = MealFilters[0]; IngredientFilter = null; Changed(nameof(RecipeSearch)); Changed(nameof(MaxPrice)); Changed(nameof(MealFilter)); Changed(nameof(IngredientFilter)); await LoadRecipes(); });
        AddRecipe = Command(() => RecipeEditor(null)); EditRecipe = Command(() => RecipeEditor(SelectedRecipe?.Recipe), () => SelectedRecipe != null);
        DeleteRecipe = Command(async () => { if (dialogs.Confirm($"Удалить рецепт «{SelectedRecipe!.Name}»?")) { await service.DeleteRecipeAsync(SelectedRecipe.Recipe.Id); await LoadAll(); } }, () => SelectedRecipe != null);
        AddLot = Command(() => LotEditor(null)); EditLot = Command(() => LotEditor(SelectedStock?.Stock), () => SelectedStock != null);
        DeleteLot = Command(async () => { if (dialogs.Confirm("Удалить выбранную партию?")) { await service.DeleteLotAsync(SelectedStock!.Stock.Id); await LoadAll(); } }, () => SelectedStock != null);
        LoadWeek = Command(LoadPlan); Generate = Command(GenerateAsync); Replace = Command(ReplaceAsync, () => SelectedMeal?.Meal.CookedAtUtc == null && SelectedMeal != null);
        Cook = Command(CookAsync, () => SelectedMeal?.CanCook == true);
        CancelSearch = new(_ => searchCancellation?.Cancel(), () => IsSearching);
        SelectMeal = new(p => SelectedMeal = p as MealDisplay, () => Idle);
        Logout = new(_ => { if (PreferencesDirty() && !dialogs.Confirm("Выйти и потерять несохраненные исключения?")) return; ClearPrivate(); logout(); }, () => Idle);
    }
    public Task InitializeAsync() => Run(LoadAll);
    private async Task Run(Func<Task> action)
    {
        if (Busy) return; Busy = true;
        try { Status = "Выполняется…"; await action(); Status = "Готово"; }
        catch (OperationCanceledException) { Status = "Подбор отменен; перечитываю сохраненное меню."; await LoadPlan(); }
        catch (BusinessException ex) { Status = ex.Message; dialogs.Error(ex.Message); }
        catch (Exception ex) { App.Log(ex); Status = "Операция не завершена. Обновите данные и проверьте доступ к базе."; dialogs.Error(Status); }
        finally { Busy = false; }
    }
    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> source) { target.Clear(); foreach (var row in source) target.Add(row); }
    private HashSet<int> savedExclusions = [];
    public bool PreferencesDirty() => !savedExclusions.SetEquals(Preferences.Where(p => p.Excluded).Select(p => p.Product.Id));
    private async Task LoadAll()
    {
        allProducts = await service.SearchProductsAsync(); Changed(nameof(AllProducts));
        savedExclusions = await service.GetExcludedProductsAsync(); Fill(Preferences, allProducts.Select(p => new ProductOption(p) { Excluded = savedExclusions.Contains(p.Id) }));
        var profile = await service.GetProfileAsync(); ProfileText = ProfileDescription(profile);
        await LoadProducts(); await LoadRecipes(); Fill(Pantry, (await service.GetPantryAsync()).Select(s => new PantryDisplay(s, runtime.Today))); await LoadPlan();
    }
    public List<Product> AllProducts => allProducts;
    private Task LoadProducts() => Load();
    private async Task Load() { Fill(Products, await service.SearchProductsAsync(ProductSearch, CategorySearch)); SelectedProduct = null; Changed(nameof(SelectedProduct)); }
    private async Task LoadRecipes()
    {
        Fill(Recipes, (await service.SearchRecipesAsync(RecipeSearch, MealFilter?.Id >= 0 ? (MealType)MealFilter.Id : null, IngredientFilter?.Id,
            string.IsNullOrWhiteSpace(MaxPrice) ? null : Rules.Parse(MaxPrice, "Максимальная цена"))).Select(r => new RecipeDisplay(r))); SelectedRecipe = null;
    }
    private DateOnly SelectedWeek()
    {
        if (Week == null) throw new BusinessException("Выберите дату понедельника."); var date = DateOnly.FromDateTime(Week.Value);
        if (date.DayOfWeek != DayOfWeek.Monday) throw new BusinessException("Начало недели должно быть понедельником. Выберите понедельник в календаре."); return date;
    }
    private async Task LoadPlan()
    {
        plan = await service.GetWeekAsync(SelectedWeek()); SelectedMeal = null; Days.Clear(); Shopping.Clear(); Statistics.Clear(); Warnings = ""; SummaryText = "";
        if (plan != null)
        {
            var exclusions = await service.GetExcludedProductsAsync();
            exclusions.UnionWith(System.Text.Json.JsonSerializer.Deserialize<int[]>(plan.ExcludedProductIdsJson) ?? []);
            for (var d = 0; d < 7; d++)
            {
                var date = plan.WeekStart.AddDays(d);
                MealDisplay Meal(MealType t) => new(plan.Meals.Single(m => m.Date == date && m.MealType == t), runtime.Today, exclusions);
                Days.Add(new(date, Meal(MealType.Breakfast), Meal(MealType.Lunch), Meal(MealType.Dinner)));
            }
            Fill(Shopping, await service.BuildShoppingAsync(plan.Id)); var stats = await service.GetWeekSummaryAsync(plan.Id); Fill(Statistics, stats.Days);
            Warnings = string.Join("\n", stats.Warnings);
            SummaryText = $"План недели: {stats.Planned.Calories:F2} ккал; Б {stats.Planned.Protein:F2} / Ж {stats.Planned.Fat:F2} / У {stats.Planned.Carbs:F2} г\n"
                + $"Суточные цели: {plan.DailyCalorieTarget:F2} ккал; Б {plan.ProteinTargetGrams:F2} / Ж {plan.FatTargetGrams:F2} / У {plan.CarbsTargetGrams:F2} г (30/30/40%)\n"
                + $"Полная стоимость меню: {Rules.Money(stats.Planned.Cost)} BYN; остаток бюджета: {Rules.Money(stats.BudgetRemaining)} BYN\n"
                + $"Расчетная закупка: {Rules.Money(stats.ShoppingCost)} BYN\n"
                + $"Подтверждено приготовление: {stats.CookedCount}/21; {stats.Confirmed.Calories:F2} ккал; Б {stats.Confirmed.Protein:F2} / Ж {stats.Confirmed.Fat:F2} / У {stats.Confirmed.Carbs:F2} г.";
            ShoppingHeader = $"Расчет на {runtime.Today:dd.MM.yyyy} · {Rules.Money(stats.ShoppingCost)} BYN · " + (Shopping.Count == 0 ? "Все продукты уже есть" : "Текущие цены; только неприготовленные блюда");
        }
        else ShoppingHeader = "Сначала создайте меню выбранной недели.";
        Changed(nameof(ShoppingHeader)); Changed(nameof(PlanHeader));
    }
    private static string ProfileDescription(User u)
    {
        try
        {
            var p = NutritionCalculator.FromUser(u); var n = NutritionCalculator.Calculate(p);
            return $"{u.DisplayName} · {(p.Sex == Sex.Male ? "Мужской" : "Женский")} · {p.Age} лет · {p.HeightCm} см · {p.WeightKg} кг\n"
                + $"Активность: ×{NutritionCalculator.Factor(p.ActivityLevel)} · бюджет {Rules.Money(p.WeeklyBudget)} BYN/неделю\n"
                + $"Основной обмен {n.Bmr:F2} ккал; суточная цель {n.Calories:F2} ккал\nБелки {n.Protein:F2} г · жиры {n.Fat:F2} г · углеводы {n.Carbs:F2} г\nУчебный расчет, не назначение диеты.";
        }
        catch (BusinessException) { return "Профиль пока не заполнен. Сохраните параметры перед генерацией меню."; }
    }
    private static List<Choice> Sexes => [new(0, "Мужской"), new(1, "Женский")];
    private static List<Choice> Activities => [new(0, "Минимальная ×1,2"), new(1, "Низкая ×1,375"), new(2, "Умеренная ×1,55"), new(3, "Высокая ×1,725"), new(4, "Очень высокая ×1,9")];
    private static List<Choice> Meals => Enum.GetValues<MealType>().Select(t => new Choice((int)t, Rules.MealName(t))).ToList();
    private async Task EditProfileAsync()
    {
        var u = await service.GetProfileAsync();
        var editor = new EditorViewModel("Параметры и нормы", async e =>
        {
            await service.SaveProfileAsync(new((Sex)e.Selected("sex"), e.Integer("age"), e.Number("height"), e.Number("weight"), (ActivityLevel)e.Selected("activity"), e.Number("budget")));
            Budget = e.Value("budget");
        }).Select("sex", "Пол для формулы", Sexes, (int?)u.Sex).Text("age", "Возраст, полных лет", u.Age ?? 20)
            .Text("height", "Рост, см", u.HeightCm ?? 175).Text("weight", "Вес, кг", u.WeightKg ?? 70)
            .Select("activity", "Активность", Activities, (int?)u.ActivityLevel).Text("budget", "Недельный бюджет, BYN", u.WeeklyBudget == 0 ? 150 : u.WeeklyBudget);
        if (await dialogs.EditAsync(editor)) { ProfileText = ProfileDescription(await service.GetProfileAsync()); }
    }
    private async Task SavePreferencesAsync()
    {
        await service.SaveExcludedProductsAsync(Preferences.Where(p => p.Excluded).Select(p => p.Product.Id)); savedExclusions = Preferences.Where(p => p.Excluded).Select(p => p.Product.Id).ToHashSet(); await LoadPlan();
    }
    private async Task ProductEditor(Product? p)
    {
        var editor = new EditorViewModel(p == null ? "Новый продукт" : "Изменить продукт", async e =>
            await service.SaveProductAsync(new Product { Id = p?.Id ?? 0, Name = e.Value("name"), Category = e.Value("category"), CaloriesPer100g = e.Number("k"), ProteinPer100g = e.Number("p"), FatPer100g = e.Number("f"), CarbsPer100g = e.Number("c"), PricePer100g = e.Number("price") }))
            .Text("name", "Название", p?.Name).Text("category", "Категория", p?.Category)
            .Text("k", "Ккал на 100 г", p?.CaloriesPer100g ?? 0).Text("p", "Белки, г на 100 г", p?.ProteinPer100g ?? 0)
            .Text("f", "Жиры, г на 100 г", p?.FatPer100g ?? 0).Text("c", "Углеводы, г на 100 г", p?.CarbsPer100g ?? 0).Text("price", "Цена, BYN за 100 г", p?.PricePer100g ?? 0);
        if (await dialogs.EditAsync(editor)) await LoadAll();
    }
    private async Task RecipeEditor(Recipe? recipe)
    {
        var editor = new EditorViewModel(recipe == null ? "Новый рецепт — одна порция" : "Изменить рецепт — одна порция", async e =>
        {
            var r = new Recipe { Id = recipe?.Id ?? 0, Name = e.Value("name"), MealType = (MealType)e.Selected("meal"), DishCategory = e.Value("category"), CookingMinutes = e.Integer("minutes"), Instructions = e.Value("instructions") };
            r.Ingredients = e.Ingredients.Select(i => new Ingredient { ProductId = i.Product?.Id ?? 0, QuantityGrams = Rules.Parse(i.Grams, "Вес ингредиента") }).ToList(); await service.SaveRecipeAsync(r);
        }).Text("name", "Название", recipe?.Name).Select("meal", "Прием пищи", Meals, (int?)recipe?.MealType)
            .Text("category", "Кулинарная категория (необязательно)", recipe?.DishCategory).Text("minutes", "Время, мин", recipe?.CookingMinutes ?? 20)
            .Text("instructions", "Инструкция", recipe?.Instructions, true);
        editor.HasIngredients = true; editor.Products = allProducts;
        if (recipe != null) foreach (var i in recipe.Ingredients) editor.Ingredients.Add(new IngredientRow { Product = allProducts.Single(p => p.Id == i.ProductId), Grams = i.QuantityGrams.ToString(CultureInfo.CurrentCulture) });
        if (await dialogs.EditAsync(editor)) await LoadAll();
    }
    private async Task LotEditor(PantryStock? stock)
    {
        var editor = new EditorViewModel(stock == null ? "Добавить партию" : "Изменить партию", async e =>
            await service.SaveLotAsync(stock?.Id ?? 0, e.Selected("product"), e.Number("grams"), e.Date("expiry")))
            .Select("product", "Продукт", allProducts.Select(p => new Choice(p.Id, p.Name)).ToList(), stock?.ProductId)
            .Text("grams", "Количество, г", stock?.QuantityGrams ?? 1000).Text("expiry", "Годен до (дд.мм.гггг, пусто — без срока)", stock?.ExpiryDate?.ToString("dd.MM.yyyy"));
        if (await dialogs.EditAsync(editor)) await LoadAll();
    }
    private async Task GenerateAsync()
    {
        var date = SelectedWeek();
        // Re-read the selected week before asking to replace it.
        var existing = await service.GetWeekAsync(date);
        if (existing != null && !dialogs.Confirm("Заменить существующее меню этой недели?")) return;
        searchCancellation = new(); Changed(nameof(IsSearching)); CommandManager.InvalidateRequerySuggested(); Status = "Подбор меню…";
        try { await service.GenerateWeekAsync(date, UseCustomTarget ? Rules.Parse(Target, "Калорийность") : null, Rules.Parse(Budget, "Бюджет"), existing?.Version, existing != null, searchCancellation.Token); await LoadPlan(); }
        finally { searchCancellation.Dispose(); searchCancellation = null; Changed(nameof(IsSearching)); }
    }
    private async Task ReplaceAsync()
    {
        var current = SelectedMeal!; var p = plan!; var exclusions = await service.GetExcludedProductsAsync();
        exclusions.UnionWith(System.Text.Json.JsonSerializer.Deserialize<int[]>(p.ExcludedProductIdsJson) ?? []);
        var recipes = (await service.SearchRecipesAsync(meal: current.Meal.MealType)).Where(r => r.Ingredients.All(i => !exclusions.Contains(i.ProductId))).ToList();
        if (recipes.Count == 0) throw new BusinessException("Нет подходящих рецептов без исключенных продуктов.");
        var editor = new EditorViewModel("Заменить блюдо", async e =>
        {
            try { await service.ReplaceMealAsync(p.Id, current.Meal.Id, e.Selected("recipe"), p.Version); }
            catch (ConfirmationRequiredException ex)
            {
                if (!dialogs.Confirm(ex.Message)) throw new BusinessException("Замена не сохранена.");
                await service.ReplaceMealAsync(p.Id, current.Meal.Id, e.Selected("recipe"), p.Version, true, ex.Revision);
            }
        }).Select("recipe", "Рецепт соответствующего приема пищи", recipes.Select(r => new Choice(r.Id, $"{r.Name} · {RecipeCalculator.Calculate(r).Calories:F0} ккал · {Rules.Money(RecipeCalculator.Calculate(r).Cost)} BYN")).ToList(), current.Meal.RecipeId);
        if (await dialogs.EditAsync(editor)) await LoadPlan();
    }
    private async Task CookAsync()
    {
        var meal = SelectedMeal!.Meal; var p = plan!;
        if (!dialogs.Confirm($"Подтвердить приготовление «{meal.RecipeNameSnapshot}» за {meal.Date:dd.MM.yyyy}?\nБудут списаны:\n" + string.Join("\n", meal.Ingredients.Select(i => $"{i.ProductNameSnapshot}: {i.QuantityGrams:F2} г")))) return;
        await service.CookMealAsync(p.Id, meal.Id, p.Version); await LoadAll();
    }
    private void ClearPrivate() { Preferences.Clear(); savedExclusions.Clear(); Pantry.Clear(); Days.Clear(); Shopping.Clear(); Statistics.Clear(); SelectedMeal = null; plan = null; ProfileText = ""; SummaryText = ""; Warnings = ""; }
}
