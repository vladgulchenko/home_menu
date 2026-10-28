# Матрица требований и реализации

Пути относительны корню. Реализованы R01–R16; ручная проверка T40/T41 частична,
автоматические подтверждения и ограничения перечислены в TEST_RESULTS.md.

| ID | Реальные файлы и символы | Экран / команда | Проверки |
| --- | --- | --- | --- |
| R01 | src/HomeMenu.Infrastructure/DatabaseRuntime.cs: DatabaseRuntime.InitializeAsync; SeedData.cs: SeedData.ApplyAsync; Migrations/20261028063030_InitialCreate.cs | src/HomeMenu.Wpf/App.xaml.cs: App.OnStartup | ServiceTests.MigrationsSeedAndReopenPreserveData |
| R02 | src/HomeMenu.Infrastructure/HomeMenuService.cs: RegisterAsync, LoginAsync, Logout; DatabaseRuntime.cs: UserSession | AuthWindow.xaml; AuthViewModel.SubmitAsync; MainViewModel.Logout | ServiceTests.AuthNormalizesLoginHashesWithSaltAndClearsSession; UiSmoke.Program — Logout |
| R03 | src/HomeMenu.Core/Calculations.cs: NutritionCalculator.Calculate, Rules.Profile; HomeMenuService.GetProfileAsync, SaveProfileAsync | MainViewModel.EditProfile, EditProfileAsync; EditorWindow | CalculationsTests.N1_ProfileFormulaAndAllActivities; UiSmoke profile |
| R04 | src/HomeMenu.Infrastructure/HomeMenuService.cs: SearchProductsAsync, SaveProductAsync, DeleteProductAsync | MainWindow «Продукты»; MainViewModel.ProductEditor, SearchProducts, AddProduct, EditProduct, DeleteProduct | ServiceTests.ProductCrudNormalizationFiltersAndReferenceProtection; UiSmoke product |
| R05 | src/HomeMenu.Core/Calculations.cs: RecipeCalculator; HomeMenuService.SearchRecipesAsync, SaveRecipeAsync, DeleteRecipeAsync | MainViewModel.RecipeEditor, SearchRecipes, AddRecipe, EditRecipe, DeleteRecipe; EditorViewModel.Ingredients | CalculationsTests.N2_RecipeUsesOnePortionAndExactMoney; ServiceTests.RecipeCrudCombinedFiltersAndValidation; UiSmoke recipe |
| R06 | src/HomeMenu.Infrastructure/HomeMenuService.cs: GetPantryAsync, SaveLotAsync, DeleteLotAsync | MainWindow «Холодильник»; MainViewModel.LotEditor, AddLot, EditLot, DeleteLot; PantryDisplay.State | CalculationsTests.N4_FefoHonorsDatesAndDoesNotMutateLots; ServiceTests.ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots; UiSmoke pantry |
| R07 | src/HomeMenu.Infrastructure/HomeMenuService.cs: GetExcludedProductsAsync, SaveExcludedProductsAsync, Exclusions, Allowed | MainWindow «Профиль»; MainViewModel.SavePreferences | ServiceTests.ManualReplacementRequiresConfirmationHonorsCategoryAndUnionOfExclusions; GeneratorTests.DistinguishesExclusionsNoSolutionAndCancellation |
| R08 | src/HomeMenu.Core/MenuGenerator.cs: MenuGenerator.Generate; HomeMenuService.GenerateWeekAsync | MainViewModel.Generate, GenerateAsync; сетка MenuDay | GeneratorTests.N3_ExactBudget21SlotsAndNoStockRequired; DailyCalorieBoundsAreInclusive; UiSmoke generator |
| R09 | src/HomeMenu.Core/MenuGenerator.cs: SearchResult, SearchStatus, Generate.Search; HomeMenuService.GenerateWeekAsync | MainViewModel.CancelSearch; Status | GeneratorTests.N3_ActualBacktrackFromPreferred900CalorieBreakfast, SearchLimitIsExact; ServiceTests.GenerationFailureCancellationAndSaveFailurePreserveOldWeek; ConcurrencyTests.CancelDuringBackgroundSearchDoesNotReplaceWeek |
| R10 | src/HomeMenu.Infrastructure/HomeMenuService.cs: ReplaceMealAsync; src/HomeMenu.Core/Calculations.cs: PlanAnalysis.Warnings | MainViewModel.Replace, ReplaceAsync; EditorWindow; DialogService.Confirm | ServiceTests.ManualReplacementRequiresConfirmationHonorsCategoryAndUnionOfExclusions; UiSmoke replacement |
| R11 | src/HomeMenu.Infrastructure/HomeMenuService.cs: CookMealAsync; src/HomeMenu.Core/Calculations.cs: PantryAllocator.Allocate | MainViewModel.Cook, CookAsync; DialogService.Confirm | ServiceTests.N5_CookingIsAllOrNothingAndExactlyOnceConcurrently; CookingRollbackUsesSnapshotsAndRejectsFutureAndNewExclusions; UiSmoke cancel/cook |
| R12 | src/HomeMenu.Infrastructure/HomeMenuService.cs: BuildShoppingAsync, Shopping | MainWindow «Покупки»; MainViewModel.LoadWeek, LoadPlan, ShoppingHeader | ServiceTests.N4_ShoppingAcrossDatesCovers230Of280GramsWithoutWriting; ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots |
| R13 | src/HomeMenu.Infrastructure/HomeMenuService.cs: GetWeekSummaryAsync; src/HomeMenu.Core/Models.cs: DaySummary, WeekSummary | MainWindow «Статистика»; MainViewModel.LoadPlan, SummaryText, Statistics | ServiceTests.ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots; UiSmoke statistics |
| R14 | src/HomeMenu.Infrastructure/HomeMenuService.cs: Read, Write, Owned, Version; DatabaseRuntime.Gate, Revision; UserSession.Verify | CanExecute и Busy в MainViewModel; повторные проверки в сервисе | ServiceTests.OwnersCannotAccessForeignLotsPlansMealsOrExclusions; SqliteEnforcesScaledNumbersForeignKeysAndUniqueKeys; ConcurrencyTests.StaleInputChangedDuringSearchIsRejectedAndOldWeekSurvives, SessionSwitchDuringSearchDoesNotWriteForOldOrNewOwner, InstanceLeaseRejectsSecondThreadAndReleasesCleanly |
| R15 | src/HomeMenu.Wpf/MainWindow.xaml, AuthWindow.xaml, EditorWindow.xaml; Mvvm.cs: ObservableObject, AsyncCommand; Editors.cs: DialogService, EditorViewModel; MainViewModel.Run | Все семь вкладок, вход, редакторы, отмена | tests/HomeMenu.UiSmoke/Program.cs: Main, FixtureDialogs, BindingTrace; ручные ограничения T40/T41 |
| R16 | docs/report/*.md; docs/diagrams/*.mmd, use-cases.puml; scripts/ExportSchema.py | README и HANDOFF | Экспорт схемы реальной БД, перечень 40 xUnit cases, фактические логи и снимки |

Весь код тестов логики находится в `tests/HomeMenu.Tests/`: CalculationsTests.cs,
GeneratorTests.cs, ServiceTests.cs, ConcurrencyTests.cs, вспомогательные TestDatabase.cs.
`tests/HomeMenu.UiSmoke/Program.cs` проверяет реальные представления, но не эмулирует
ввод настоящего пароля через системные средства управления рабочим столом.

Предложенные гайдoм IProductService/IRecipeService объединены в ICatalogService,
IPreferenceService — в IProfileService, IShoppingListService/IStatisticsService —
в IMenuService. Контракты объявлены в src/HomeMenu.Core/Models.cs; реализация — HomeMenuService.
Отдельные generic repository, сервер, роли администратора и модули ПВЗ отсутствуют.
