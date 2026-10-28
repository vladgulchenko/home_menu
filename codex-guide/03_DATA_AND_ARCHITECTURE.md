# Архитектура и модель данных

## Решение

Если репозиторий новый, использовать `HomeMenu.sln` и проекты:

| Проект | Target | Ответственность и зависимости |
| --- | --- | --- |
| `src/HomeMenu.Core` | net8.0 | Модели, перечисления, DTO, контракты сервисов и чистые расчеты; без WPF/EF |
| `src/HomeMenu.Infrastructure` | net8.0 | AppDbContext, Fluent API, миграции, SQLite, хеширование, реализация сервисов; зависит от Core |
| `src/HomeMenu.Wpf` | net8.0-windows | XAML, ViewModels, команды, диалоги, точка композиции; зависит от Core и Infrastructure |
| `tests/HomeMenu.Tests` | net8.0 | Модульные и интеграционные тесты Core/Infrastructure на настоящем SQLite |

`UseWPF=true` только в Wpf. `LangVersion=12.0`, nullable включить. Не добавлять абстрактный generic repository поверх EF без реальной необходимости. Не создавать наследование предметных сущностей ради формального ООП: инкапсуляция моделей, интерфейсы сервисов, наследование ObservableObject и полиморфизм реализаций контрактов достаточны.

В существующем репозитории допустимо сохранить эквивалентные имена и структуру, но все фактические отличия от предложенных имен должны попасть в `docs/report/CODE_MAP.md`. Во всех созданных документах использовать уже реальные имена. Предлагаемые ниже названия не являются описанием проверенного кода.

## Сущности

Все идентификаторы — целочисленные PK. Установить явные внешние ключи, ограничения и индексы через Fluent API.

| Сущность | Основные поля | Связи и ограничения |
| --- | --- | --- |
| User | Id, DisplayName, Login, NormalizedLogin, PasswordHash, Sex?, Age?, HeightCm?, WeightKg?, ActivityLevel?, WeeklyBudget | Unique NormalizedLogin; профиль может быть неполным до первого сохранения |
| Product | Id, Name, NormalizedName, Category, CaloriesPer100g, ProteinPer100g, FatPer100g, CarbsPer100g, PricePer100g | Unique NormalizedName; неотрицательные значения |
| Recipe | Id, Name, MealType, DishCategory?, Instructions, CookingMinutes | 1:N Ingredient; MealType как enum |
| Ingredient | Id, RecipeId, ProductId, QuantityGrams | Unique(RecipeId, ProductId); QuantityGrams>0 |
| PantryStock | Id, UserId, ProductId, QuantityGrams, ExpiryDate? | N:1 User и Product; количество ≥0; индекс(UserId, ProductId, ExpiryDate) |
| UserExcludedProduct | UserId, ProductId | Составной PK, запрет дублей; ссылки на User и Product |
| MenuPlan | Id, UserId, WeekStart, DailyCalorieTarget, ProteinTargetGrams, FatTargetGrams, CarbsTargetGrams, Budget, ExcludedProductIdsJson, CreatedAtUtc, Version | Unique(UserId, WeekStart); снимок параметров генерации; Version как программный concurrency token |
| WeeklyMenu | Id, MenuPlanId, Date, MealType, RecipeId, RecipeNameSnapshot, InstructionsSnapshot, CookedAtUtc? | Unique(MenuPlanId, Date, MealType); дата в семидневном интервале проверяется сервисом |
| MenuIngredientSnapshot | Id, WeeklyMenuId, ProductId, ProductNameSnapshot, CategorySnapshot, QuantityGrams, CaloriesPer100g, ProteinPer100g, FatPer100g, CarbsPer100g, PricePer100g | Копия данных при выборе блюда; Unique(WeeklyMenuId, ProductId) |

Это шесть исходных сущностей User, Product, Recipe, Ingredient, PantryStock, WeeklyMenu плюс три технических дополнения. `WeeklyMenu` сохраняет смысл записи сетки, а `MenuPlan` объединяет 21 запись в одну неделю. Не переименовывать все в другие сущности без отражения в записке.

В JSON плана хранить отсортированный список исключенных ProductId, валидируемый при записи. Допустим отдельный нормализованный справочник исключений плана вместо JSON, если это аргументировано и отражено в миграции, тестах и документации. Все остальные поля не прятать в произвольный JSON.

КБЖУ и стоимость порции/дня/недели вычисляются из снимков ингредиентов. Не хранить еще один независимый изменяемый Total, который может разойтись с составом. Снимок инструкции и имени обеспечивает воспроизводимость рецепта меню после изменения каталога.

## Удаление и история

Recipe → Ingredient: каскад при удалении допустим только после проверки отсутствия ссылок WeeklyMenu. MenuPlan → WeeklyMenu → MenuIngredientSnapshot: каскад для атомарной перегенерации неприготовленного плана. Ссылки Product и Recipe из истории защищены Restrict/NoAction. Удаление учетной записи пользователя не предоставлять в UI этого этапа. Ограничения, зависящие от нескольких таблиц (21 слот, категория рецепта, владелец), контролируются сервисом и интеграционными тестами.

## Числа и SQLite

В C# использовать `decimal`. Чтобы индексы, сравнения и CHECK работали с EF Core 8/SQLite однозначно, хранить измеримые величины как масштабированные INTEGER через явные value converter:

- граммы, рост, вес, ккал и БЖУ: масштаб 100;
- цена за 100 г и бюджет: масштаб 10 000;
- рассчитанные цели MenuPlan (DailyCalorieTarget и целевые граммы БЖУ): масштаб 100 000 000; округлить их один раз до 8 знаков при формировании снимка параметров и далее применять в проверках именно сохраненные значения;
- возраст, перечисления, версия и минуты: обычные INTEGER.

Например, 125.50 г хранится как 12550, 1.2345 BYN как 12345. При записи требовать точность ввода, проверять диапазоны до `checked`-конвертации в long; не отбрасывать незаметно дробную часть. У физических колонок использовать явный суффикс `Scaled` и описать его в DATABASE.md. CHECK писать относительно физических колонок INTEGER. Вычисляемые результаты не округлять перед проверкой границ; отображение денег округляется отдельно.

Альтернативное хранение `decimal` как TEXT допустимо только с явным обоснованием, числовыми проверками в сервисах и расчетами после материализации, без ненадежных SQL-сравнений/агрегатов. Базовый вариант этого гайда — масштабированные INTEGER. Нельзя полагаться на `HasPrecision` как гарантию десятичной арифметики SQLite или переносить возможности EF 9 в EF 8.

Календарные даты WeekStart, Date и ExpiryDate — `DateOnly`, в SQLite TEXT ISO yyyy-MM-dd. События CreatedAtUtc/CookedAtUtc — DateTime UTC, восстановление Kind при чтении; для отображения переводить в локальное время Windows. Использовать внедряемый `TimeProvider` для дат в тестах. Перечисления снабдить ограничениями допустимых значений.

## Сервисы

| Контракт | Основные операции |
| --- | --- |
| IAuthService | RegisterAsync, LoginAsync, Logout |
| IProfileService | GetAsync, SaveAsync, GetNutritionTargets |
| IProductService | SearchAsync, SaveAsync, DeleteAsync |
| IRecipeService | SearchAsync, GetAsync, SaveAsync, DeleteAsync |
| IPantryService | GetAsync, SaveLotAsync, DeleteLotAsync |
| IPreferenceService | GetExcludedProductsAsync, SaveExcludedProductsAsync |
| IMenuService | GetWeekAsync, GenerateWeekAsync, ReplaceMealAsync, CookMealAsync |
| IShoppingListService | BuildAsync для выбранного плана |
| IStatisticsService | GetWeekSummaryAsync |

Чистые компоненты: NutritionCalculator, RecipeCalculator, MenuGenerator и PantryAllocator; имя и точное разбиение можно упростить, сохранив ответственность. Операции записи — в сервисах. ViewModel не делает LINQ к DbSet и не списывает остатки самостоятельно. Для каждого вызова сервиса создавать короткоживущий контекст через фабрику, не передавать DbContext в фоновые задачи и не использовать один контекст параллельно.

## Запуск и атомарность

Путь БД по умолчанию `%LOCALAPPDATA%\HomeMenuCourse\homemenu.db`, переопределение `HOMEMENU_DATABASE_PATH` для демонстрации и тестов. Включить foreign keys, задать ограниченное ожидание занятости. До миграций получить именованную блокировку одного экземпляра приложения для этого пути БД. Все изменяющие операции приложения сериализовать общей блокировкой сервисов; UI-блокировка сама по себе недостаточна. Внешнее редактирование файла БД одновременно с приложением не поддерживается.

При запуске: путь → блокировка экземпляра → создание каталога → `MigrateAsync` → идемпотентный seed справочников → окно входа. Не смешивать `EnsureCreated` и миграции для рабочей БД. Хеши пользовательских паролей в seed не включать. При ошибке запуска показать причину и завершить запуск без удаления файла. Не реализовывать скрытый сброс БД.

Рецепт с ингредиентами, замена недельного плана, ручная замена со снимками, списание с CookedAtUtc сохраняются каждый в своей транзакции. Версию MenuPlan увеличивать при изменении его элементов; перед применением команды проверять ожидаемую версию. При конфликте не перезаписывать: попросить обновить данные. Для SQLite программный счетчик версии используется вместо несуществующего автоматического rowversion.

На время расчета генератора не удерживать транзакцию БД: загрузить неизменяемый снимок входных данных, выполнить чистый поиск в фоне, перед записью под блокировкой заново проверить актуальность исходных данных и версию целевого плана. Можно использовать общий счетчик ревизии успешных записей в процессе; при любом изменении данных после получения снимка результат отклоняется с предложением повторить поиск. Увеличивать ревизию только после успешного commit. Итоговый механизм описать и проверить. Отмена/ошибка до commit не должна менять старый план.
