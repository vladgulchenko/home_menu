# Результаты проверки HomeMenu

Дата: 25.11.2026. Проверенный срез кода: `e745535541264b6fa99ad6d8d065d4d5ba6b0fbe`. Исполняемые исходники на момент проверки чистые. Последующий коммит содержит документацию и доказательства.

Среда: Windows 10.0.26300, SDK 10.0.103, целевая платформа .NET 8, SQLite 3.41.2.

| Проверка | Результат |
| --- | --- |
| Restore --locked-mode | Успешно |
| Release build -warnaserror | Успешно |
| xUnit, SQLite | 62 Passed, 0 Failed, 0 Skipped |
| WPF smoke | 27 Passed |
| Перезапуск настоящего WPF App в двух процессах | 13 Passed |
| Межпроцессная блокировка БД | 3 Passed |

Итого: 62 теста и 43 дополнительные проверки; это не измерение покрытия строк кода.

Доказательства: [summary.json](evidence/summary.json), [62 случая](evidence/test-cases.csv), [UI](evidence/ui-smoke.txt), [перезапуск](evidence/restart.txt), [блокировка](evidence/process-lock.txt), [снимки](../screenshots/README.md). Полный TRX остаётся локально в игнорируемой папке artifacts/verification.

Повторить: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify.ps1 -ExportEvidence`.

Добавлены 22 регрессионных случая: неверный вход сбрасывает сессию; выход во время финального SaveChanges откатывает запись; неудачная замена рецепта и исключений сохраняет старые данные; реальная ошибка SQLite при приготовлении откатывает остатки и версию меню; проверяются операции без сессии, некорректная регистрация и соответствие модели миграциям.

Исправлены сохранение прежней авторизации при неверном входе, запись после выхода во время сохранения и зависание закрытия главного окна при повторном Shutdown.

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
| T40 | Partial / Manual NotRun | Реальные окна и команды UI прошли профиль → каталоги → партия → исключение → генерация → замена → покупки → отмена/приготовление → выход; bindings без ошибок | Win | PasswordBox и физические клики/MessageBox не автоматизировались; ручная проверка не проводилась |
| T41 | Passed (processes) | OperationalProbes: два отдельных процесса WPF App; сохранены 21 приём пищи, приготовление, остатки, версия меню; выход очищает данные | Win | Физический ввод пароля не проверялся |
| T42 | Passed (process lock) | Второй процесс для той же БД отклонён, другая БД разрешена, после освобождения повторный захват успешен | Win | Штатное сообщение вручную не проверялось |


## Границы проверки

Окна и ViewModel настоящие; подтверждения в smoke подменены. Физический ввод PasswordBox и штатные диалоги вручную не проверялись. Ручной маршрут: [MANUAL_CHECKS.txt](MANUAL_CHECKS.txt).
