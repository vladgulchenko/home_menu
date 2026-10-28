# Фактические результаты проверок

Дата: 28.10.2026. Среда всех приведенных запусков: Windows 11 x64, build 10.0.26200,
SDK 10.0.103, .NET / WindowsDesktop runtime 8.0.24, SQLite runtime 3.41.2.
Логические тесты используют отдельные реальные SQLite-файлы с MigrateAsync, не EF InMemory.

Проверенный срез исходников и тестов: `eaca1373111486bccd7d6e03be66e960cd4fb828`.
Документация и сохраненные evidence добавлены следующим коммитом; изменения
производственного кода при разбиении на коммиты не вносились.

## Команды

| Команда | Результат |
| --- | --- |
| dotnet restore HomeMenu.sln | Успех, все 5 проектов |
| dotnet restore HomeMenu.sln --locked-mode | Успех, lock-файлы согласованы |
| dotnet build HomeMenu.sln -c Release --no-restore | Успех, 0 предупреждений / 0 ошибок |
| dotnet test tests/HomeMenu.Tests -c Release --no-build --logger "trx;LogFileName=tests.trx" | 40 cases: 40 Passed, 0 Failed, 0 Skipped |
| dotnet run --project tests/HomeMenu.UiSmoke -c Release --no-build | Успех, код 0; все проверки маршрута, 0 binding warnings/errors |
| dotnet tool restore | dotnet-ef 8.0.24 восстановлен |
| dotnet ef migrations list --project src/HomeMenu.Infrastructure --startup-project src/HomeMenu.Infrastructure | InitialCreate обнаружена; Pending относится к отдельному design-time пути, не рабочей БД |
| dotnet ef migrations has-pending-model-changes --project src/HomeMenu.Infrastructure --startup-project src/HomeMenu.Infrastructure | No changes have been made to the model since the last migration |
| Запуск HomeMenu.Wpf.exe с HOMEMENU_DATABASE_PATH=artifacts/startup-check.db | Созданы SQLite, миграция и seed; процесс отвечал. Ручной ввод входа не выполнялся |

Исходный результат xUnit: [evidence/tests.trx](evidence/tests.trx).
WPF-лог: [evidence/ui-smoke.txt](evidence/ui-smoke.txt); из копии лога удален только
локальный путь временной БД. Снимки действующих представлений: [../screenshots](../screenshots/README.md).

## Сценарии T01–T42

В таблице «Win» означает указанную выше Windows-среду. Имена без пространства имен
относятся к tests/HomeMenu.Tests. UI — tests/HomeMenu.UiSmoke/Program.cs.
Passed означает только проверенный объем строки; непроверенная ручная часть названа явно.

| ID | Статус | Test method / шаги и фактический результат | Среда | Ограничение |
| --- | --- | --- | --- | --- |
| T01 | Passed | ServiceTests.MigrationsSeedAndReopenPreserveData: миграция, 17 продуктов, 9 рецептов | Win | — |
| T02 | Passed | Тот же тест: повтор InitializeAsync, новый runtime, одна миграция и пользователь, 21 запись сохранены | Win | — |
| T03 | Passed | ServiceTests.AuthNormalizesLoginHashesWithSaltAndClearsSession: TEST.USER отклонен как дубль | Win | — |
| T04 | Passed | Тот же тест: разные salted-хеши одного пароля, неверный пароль отклонен, верный принят | Win | — |
| T05 | Passed | Тот же тест и UI Logout: сеанс очищен, приватные коллекции пусты, профиль недоступен | Win | — |
| T06 | Passed | ServiceTests.OwnersCannotAccessForeignLotsPlansMealsOrExclusions: чужие партия/план/списание/покупки/статистика недоступны; общий каталог виден | Win | — |
| T07 | Passed | CalculationsTests.N1_ProfileFormulaAndAllActivities; ServiceTests.GenerationRequiresCompleteProfileMondayAndExplicitReplacement | Win | — |
| T08 | Passed | DecimalParsingIsUnambiguous, InvalidNumbersRejected, ValidationRejectsPrecisionDuplicateIngredientsAndInvalidMacros; UI ввод 0,1234 | Win | Не все возможные ошибочные числа перечислены отдельными cases |
| T09 | Passed | ServiceTests.ProductCrudNormalizationFiltersAndReferenceProtection; UI product editor | Win | Ручные клики удаления отдельно не проходились |
| T10 | Passed | Тот же тест + ServiceTests.SqliteEnforcesScaledNumbersForeignKeysAndUniqueKeys: связанные удаления отклонены | Win | — |
| T11 | Passed | CalculationsTests.N2_RecipeUsesOnePortionAndExactMoney; ValidationRejectsPrecisionDuplicateIngredientsAndInvalidMacros; ServiceTests.RecipeCrudCombinedFiltersAndValidation | Win | — |
| T12 | Passed | ServiceTests.CookingRollbackUsesSnapshotsAndRejectsFutureAndNewExclusions; ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots: состав/инструкция/цена меню сохранены | Win | — |
| T13 | Passed | CalculationsTests.N4_FefoHonorsDatesAndDoesNotMutateLots: равные сроки по Id, null после дат; ServiceTests.ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots | Win | — |
| T14 | Passed | GeneratorTests.N3_ExactBudget21SlotsAndNoStockRequired; сервисный seed-план и UI generator | Win | — |
| T15 | Passed | GeneratorTests.DailyCalorieBoundsAreInclusive: 180/220 и 179,99/220,01 на 100 г | Win | — |
| T16 | Passed | GeneratorTests.N3_ExactBudget21SlotsAndNoStockRequired: 14 проходит, 13,9999 отклонен | Win | — |
| T17 | Passed | GeneratorTests.DistinguishesExclusionsNoSolutionAndCancellation и ServiceTests.GenerationFailureCancellationAndSaveFailurePreserveOldWeek | Win | — |
| T18 | Passed | GeneratorTests.N3_ActualBacktrackFromPreferred900CalorieBreakfast: VisitedNodes > 21, Backtracks > 0, альтернатива выбрана | Win | — |
| T19 | Passed | GeneratorTests.DistinguishesExclusionsNoSolutionAndCancellation: NoSolution при недоборе | Win | — |
| T20 | Passed | GeneratorTests.SearchLimitIsExact: лимиты 0/1/20 дают ровно N попыток | Win | — |
| T21 | Passed | GeneratorTests.CancellationDuringActualTreeTraversalReturnsNoPartialMenu: отмена на узле 10; ConcurrencyTests.CancelDuringBackgroundSearchDoesNotReplaceWeek; заранее отмененный токен | Win | — |
| T22 | Passed | ServiceTests.GenerationFailureCancellationAndSaveFailurePreserveOldWeek: отказ, отмена, лимит и сбой второй SaveChanges после SQL-удаления откатывают старую неделю целиком | Win | — |
| T23 | Passed | ConcurrencyTests.StaleInputChangedDuringSearchIsRejectedAndOldWeekSurvives и SessionSwitchDuringSearchDoesNotWriteForOldOrNewOwner | Win | — |
| T24 | Passed | UI Replace и ServiceTests.ManualReplacementRequiresConfirmationHonorsCategoryAndUnionOfExclusions: снимки и Version обновлены | Win | Подтверждения UI подставляет FixtureDialogs |
| T25 | Passed | Тот же тест: до согласия нет записи, после есть warning; ConfirmationCannotApplyAfterRecipeOrPriceChanged отклоняет устаревшее согласие | Win | — |
| T26 | Passed | Тот же тест: категория и объединение текущих/сохраненных исключений обязательны даже с acceptViolations | Win | — |
| T27 | Passed | ServiceTests.N5_CookingIsAllOrNothingAndExactlyOnceConcurrently: 80/500 без изменений, затем 300/1000 → 200/800 | Win | — |
| T28 | Passed (UI automation) | UI FixtureDialogs.ConfirmAnswer=false: Cook-команда не вызывает сохранение, все CookedAtUtc null | Win | Физический клик в MessageBox не выполнялся |
| T29 | Passed | ServiceTests.N5_CookingIsAllOrNothingAndExactlyOnceConcurrently: два Task.Run, ровно один успех, повтор отклонен | Win | — |
| T30 | Passed | ServiceTests.CookingRollbackUsesSnapshotsAndRejectsFutureAndNewExclusions: interceptor бросает до SaveChanges, новые контексты видят исходные партии/статус | Win | Искусственная ошибка SaveChanges, не физическое повреждение диска |
| T31 | Passed | Тот же тест: будущая дата и новое исключение отклонены | Win | — |
| T32 | Passed | ServiceTests.N5_CookingIsAllOrNothingAndExactlyOnceConcurrently: замена приготовленного и перегенерация недели запрещены | Win | — |
| T33 | Passed | ServiceTests.CookingRollbackUsesSnapshotsAndRejectsFutureAndNewExclusions: измененный рецепт по 1 г не меняет исходное списание 100/200 г | Win | — |
| T34 | Passed | ServiceTests.N4_ShoppingAcrossDatesCovers230Of280GramsWithoutWriting: 280/230/50 г и 0,20 BYN, партии 100/150 неизменны | Win | — |
| T35 | Passed | ServiceTests.ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots: после приготовления потребность 2000/4000 г на 20 оставшихся порций | Win | — |
| T36 | Passed | Тот же тест: план 14,70, новая закупка 21,00; после приготовления 20,00 | Win | — |
| T37 | Passed | Тот же тест: достаточный запас дает пустой дефицит; UI начальное состояние без плана | Win | — |
| T38 | Passed | ServiceTests.ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots; UI statistics: 1 приготовление, 500 ккал подтверждено в N2, 7 строк | Win | — |
| T39 | Passed | ServiceTests.ManualReplacementRequiresConfirmationHonorsCategoryAndUnionOfExclusions: старый Version отклонен; конкурентный cook тоже проверяет Version | Win | — |
| T40 | Partial / Manual NotRun | Реальные окна и команды UI прошли профиль → каталоги → партия → исключение → генерация → замена → покупки → отмена/приготовление → выход; bindings без ошибок | Win | PasswordBox и физические клики/MessageBox не автоматизировались; Computer Use native pipe недоступен |
| T41 | Partial / Manual NotRun | UI и ServiceTests.MigrationsSeedAndReopenPreserveData: новый runtime/сеанс видят сохраненный план и приготовление | Win | Ручной перезапуск всего exe с повторным вводом пароля не пройден |
| T42 | Passed (mutex) / process NotRun | ConcurrencyTests.InstanceLeaseRejectsSecondThreadAndReleasesCleanly: второй поток отвергнут, после освобождения захват успешен | Win | Диалог отказа второго продуктового процесса вручную не проверялся |

## Что проходило в WPF

Отдельный executable UiSmoke создал случайную временную SQLite, открыл AuthWindow,
реальные MainWindow и EditorWindow, передавал данные в те же ViewModels и AsyncCommand,
которые использует продукт. Только диалоги подтверждения заменены FixtureDialogs.
Учетная запись синтетическая; вход выполнялся напрямую сервисом тестовой фикстуры,
а не автоматизацией пользовательского системного диалога.

Затем последовательно открыты все семь вкладок, сняты их фактические WPF-представления,
проверены отсутствие сообщений об ошибках и WPF binding warnings/errors, выход
и очистка приватных коллекций. Это автоматический маршрут, не ручная приемка.

Ранние запуски нашли и помогли исправить XAML RowHeight="Auto" и защиту Closing
при отсутствующем DataContext. Итоговые результаты относятся к исправленному коду.

## Полный перечень выполненных xUnit cases

Список ниже экспортирован из итогового TRX; параметризованные cases считаются отдельно.

<!-- executed-cases -->

| Case | ????????? |
| --- | --- |
| `HomeMenu.Tests.CalculationsTests.DecimalParsingIsUnambiguous(text: "1,5", expected: "1.5")` | Passed |
| `HomeMenu.Tests.CalculationsTests.DecimalParsingIsUnambiguous(text: "1.5", expected: "1.5")` | Passed |
| `HomeMenu.Tests.CalculationsTests.InvalidNumbersRejected(text: "1 500")` | Passed |
| `HomeMenu.Tests.CalculationsTests.InvalidNumbersRejected(text: "1,000.2")` | Passed |
| `HomeMenu.Tests.CalculationsTests.InvalidNumbersRejected(text: "Infinity")` | Passed |
| `HomeMenu.Tests.CalculationsTests.InvalidNumbersRejected(text: "NaN")` | Passed |
| `HomeMenu.Tests.CalculationsTests.N1_ProfileFormulaAndAllActivities` | Passed |
| `HomeMenu.Tests.CalculationsTests.N2_RecipeUsesOnePortionAndExactMoney` | Passed |
| `HomeMenu.Tests.CalculationsTests.N4_FefoHonorsDatesAndDoesNotMutateLots` | Passed |
| `HomeMenu.Tests.CalculationsTests.ValidationRejectsPrecisionDuplicateIngredientsAndInvalidMacros` | Passed |
| `HomeMenu.Tests.ConcurrencyTests.CancelDuringBackgroundSearchDoesNotReplaceWeek` | Passed |
| `HomeMenu.Tests.ConcurrencyTests.InstanceLeaseRejectsSecondThreadAndReleasesCleanly` | Passed |
| `HomeMenu.Tests.ConcurrencyTests.SessionSwitchDuringSearchDoesNotWriteForOldOrNewOwner` | Passed |
| `HomeMenu.Tests.ConcurrencyTests.StaleInputChangedDuringSearchIsRejectedAndOldWeekSurvives` | Passed |
| `HomeMenu.Tests.GeneratorTests.CancellationDuringActualTreeTraversalReturnsNoPartialMenu` | Passed |
| `HomeMenu.Tests.GeneratorTests.DailyCalorieBoundsAreInclusive(calories: 179,99000000000001, status: NoSolution)` | Passed |
| `HomeMenu.Tests.GeneratorTests.DailyCalorieBoundsAreInclusive(calories: 180, status: Success)` | Passed |
| `HomeMenu.Tests.GeneratorTests.DailyCalorieBoundsAreInclusive(calories: 220, status: Success)` | Passed |
| `HomeMenu.Tests.GeneratorTests.DailyCalorieBoundsAreInclusive(calories: 220,00999999999999, status: NoSolution)` | Passed |
| `HomeMenu.Tests.GeneratorTests.DeterministicPriorityUsesRemainingSuitableLots` | Passed |
| `HomeMenu.Tests.GeneratorTests.DistinguishesExclusionsNoSolutionAndCancellation` | Passed |
| `HomeMenu.Tests.GeneratorTests.N3_ActualBacktrackFromPreferred900CalorieBreakfast` | Passed |
| `HomeMenu.Tests.GeneratorTests.N3_ExactBudget21SlotsAndNoStockRequired` | Passed |
| `HomeMenu.Tests.GeneratorTests.SearchLimitIsExact(limit: 0)` | Passed |
| `HomeMenu.Tests.GeneratorTests.SearchLimitIsExact(limit: 1)` | Passed |
| `HomeMenu.Tests.GeneratorTests.SearchLimitIsExact(limit: 20)` | Passed |
| `HomeMenu.Tests.ServiceTests.AuthNormalizesLoginHashesWithSaltAndClearsSession` | Passed |
| `HomeMenu.Tests.ServiceTests.ConfirmationCannotApplyAfterRecipeOrPriceChanged` | Passed |
| `HomeMenu.Tests.ServiceTests.CookingRollbackUsesSnapshotsAndRejectsFutureAndNewExclusions` | Passed |
| `HomeMenu.Tests.ServiceTests.GenerationFailureCancellationAndSaveFailurePreserveOldWeek` | Passed |
| `HomeMenu.Tests.ServiceTests.GenerationRequiresCompleteProfileMondayAndExplicitReplacement` | Passed |
| `HomeMenu.Tests.ServiceTests.ManualReplacementRequiresConfirmationHonorsCategoryAndUnionOfExclusions` | Passed |
| `HomeMenu.Tests.ServiceTests.MigrationsSeedAndReopenPreserveData` | Passed |
| `HomeMenu.Tests.ServiceTests.N4_ShoppingAcrossDatesCovers230Of280GramsWithoutWriting` | Passed |
| `HomeMenu.Tests.ServiceTests.N5_CookingIsAllOrNothingAndExactlyOnceConcurrently` | Passed |
| `HomeMenu.Tests.ServiceTests.OwnersCannotAccessForeignLotsPlansMealsOrExclusions` | Passed |
| `HomeMenu.Tests.ServiceTests.ProductCrudNormalizationFiltersAndReferenceProtection` | Passed |
| `HomeMenu.Tests.ServiceTests.RecipeCrudCombinedFiltersAndValidation` | Passed |
| `HomeMenu.Tests.ServiceTests.ShoppingUsesCurrentPricesAndSkipsCookedMealsAndExpiredLots` | Passed |
| `HomeMenu.Tests.ServiceTests.SqliteEnforcesScaledNumbersForeignKeysAndUniqueKeys` | Passed |
