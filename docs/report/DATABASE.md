# Физическая база данных

Схема ниже извлечена `scripts/ExportSchema.py` из реальной БД после миграции
`20261028063030_InitialCreate`. Данные пользователей не экспортируются.
Исходники модели: `src/HomeMenu.Infrastructure/AppDbContext.cs`,
миграция и snapshot: `src/HomeMenu.Infrastructure/Migrations/`.

## Представление значений

Все PK/FK/enum/возраст/время приготовления/Version — INTEGER; строки, DateOnly
(ISO yyyy-MM-dd) и события DateTime UTC — TEXT. INTEGER PRIMARY KEY использует autoincrement.
`__EFMigrationsHistory` — техническая таблица EF. DateTime восстанавливается с Kind=Utc.

Все decimal имеют физический суффикс Scaled и тип INTEGER:

| Поля C# | Масштаб |
| --- | --- |
| QuantityGrams, HeightCm, WeightKg, CaloriesPer100g, ProteinPer100g, FatPer100g, CarbsPer100g | 100 |
| PricePer100g, WeeklyBudget, Budget | 10000 |
| DailyCalorieTarget, ProteinTargetGrams, FatTargetGrams, CarbsTargetGrams | 100000000 |

Например, цена 1,2345 хранится как 12345; 125,50 г — как 12550.
`ToScaled` отвергает лишнюю точность, конвертация long выполняется checked.
В расчетах после чтения используется decimal. Суммы не округляются перед проверкой бюджета.

В приложении 9 предметных таблиц. Общие: Products, Recipes, Ingredients.
Пользовательские: Users, PantryStocks, UserExcludedProducts, MenuPlans,
WeeklyMenus, MenuIngredientSnapshots (последние две принадлежат владельцу через план).

## CREATE TABLE и индексы

SQL взят из sqlite_master; имена и CHECK соответствуют физическим столбцам.

### Ingredients

```sql
CREATE TABLE "Ingredients" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_Ingredients" PRIMARY KEY AUTOINCREMENT,

    "RecipeId" INTEGER NOT NULL,

    "ProductId" INTEGER NOT NULL,

    "QuantityGramsScaled" INTEGER NOT NULL,

    CONSTRAINT "CK_Ingredient_Grams" CHECK (QuantityGramsScaled BETWEEN 1 AND 10000000),

    CONSTRAINT "FK_Ingredients_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,

    CONSTRAINT "FK_Ingredients_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE

);
```

```sql
CREATE INDEX "IX_Ingredients_ProductId" ON "Ingredients" ("ProductId");
CREATE UNIQUE INDEX "IX_Ingredients_RecipeId_ProductId" ON "Ingredients" ("RecipeId", "ProductId");
```

### MenuIngredientSnapshots

```sql
CREATE TABLE "MenuIngredientSnapshots" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_MenuIngredientSnapshots" PRIMARY KEY AUTOINCREMENT,

    "WeeklyMenuId" INTEGER NOT NULL,

    "ProductId" INTEGER NOT NULL,

    "ProductNameSnapshot" TEXT NOT NULL,

    "CategorySnapshot" TEXT NOT NULL,

    "QuantityGramsScaled" INTEGER NOT NULL,

    "CaloriesPer100gScaled" INTEGER NOT NULL,

    "ProteinPer100gScaled" INTEGER NOT NULL,

    "FatPer100gScaled" INTEGER NOT NULL,

    "CarbsPer100gScaled" INTEGER NOT NULL,

    "PricePer100gScaled" INTEGER NOT NULL,

    CONSTRAINT "CK_Snapshot_Values" CHECK (CaloriesPer100gScaled BETWEEN 0 AND 100000 AND ProteinPer100gScaled BETWEEN 0 AND 10000 AND FatPer100gScaled BETWEEN 0 AND 10000 AND CarbsPer100gScaled BETWEEN 0 AND 10000 AND ProteinPer100gScaled + FatPer100gScaled + CarbsPer100gScaled <= 10000 AND QuantityGramsScaled BETWEEN 1 AND 10000000 AND PricePer100gScaled BETWEEN 0 AND 100000000),

    CONSTRAINT "FK_MenuIngredientSnapshots_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,

    CONSTRAINT "FK_MenuIngredientSnapshots_WeeklyMenus_WeeklyMenuId" FOREIGN KEY ("WeeklyMenuId") REFERENCES "WeeklyMenus" ("Id") ON DELETE CASCADE

);
```

```sql
CREATE INDEX "IX_MenuIngredientSnapshots_ProductId" ON "MenuIngredientSnapshots" ("ProductId");
CREATE UNIQUE INDEX "IX_MenuIngredientSnapshots_WeeklyMenuId_ProductId" ON "MenuIngredientSnapshots" ("WeeklyMenuId", "ProductId");
```

### MenuPlans

```sql
CREATE TABLE "MenuPlans" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_MenuPlans" PRIMARY KEY AUTOINCREMENT,

    "UserId" INTEGER NOT NULL,

    "WeekStart" TEXT NOT NULL,

    "DailyCalorieTargetScaled" INTEGER NOT NULL,

    "ProteinTargetGramsScaled" INTEGER NOT NULL,

    "FatTargetGramsScaled" INTEGER NOT NULL,

    "CarbsTargetGramsScaled" INTEGER NOT NULL,

    "BudgetScaled" INTEGER NOT NULL,

    "ExcludedProductIdsJson" TEXT NOT NULL,

    "CreatedAtUtc" TEXT NOT NULL,

    "Version" INTEGER NOT NULL,

    CONSTRAINT "CK_Plan_Values" CHECK (DailyCalorieTargetScaled > 0 AND ProteinTargetGramsScaled >= 0 AND FatTargetGramsScaled >= 0 AND CarbsTargetGramsScaled >= 0 AND BudgetScaled BETWEEN 0 AND 10000000000 AND Version > 0),

    CONSTRAINT "FK_MenuPlans_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT

);
```

```sql
CREATE UNIQUE INDEX "IX_MenuPlans_UserId_WeekStart" ON "MenuPlans" ("UserId", "WeekStart");
```

### PantryStocks

```sql
CREATE TABLE "PantryStocks" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_PantryStocks" PRIMARY KEY AUTOINCREMENT,

    "UserId" INTEGER NOT NULL,

    "ProductId" INTEGER NOT NULL,

    "QuantityGramsScaled" INTEGER NOT NULL,

    "ExpiryDate" TEXT NULL,

    CONSTRAINT "CK_Stock_Grams" CHECK (QuantityGramsScaled BETWEEN 0 AND 1000000000),

    CONSTRAINT "FK_PantryStocks_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,

    CONSTRAINT "FK_PantryStocks_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT

);
```

```sql
CREATE INDEX "IX_PantryStocks_ProductId" ON "PantryStocks" ("ProductId");
CREATE INDEX "IX_PantryStocks_UserId_ProductId_ExpiryDate" ON "PantryStocks" ("UserId", "ProductId", "ExpiryDate");
```

### Products

```sql
CREATE TABLE "Products" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,

    "Name" TEXT NOT NULL,

    "NormalizedName" TEXT NOT NULL,

    "Category" TEXT NOT NULL,

    "CaloriesPer100gScaled" INTEGER NOT NULL,

    "ProteinPer100gScaled" INTEGER NOT NULL,

    "FatPer100gScaled" INTEGER NOT NULL,

    "CarbsPer100gScaled" INTEGER NOT NULL,

    "PricePer100gScaled" INTEGER NOT NULL,

    CONSTRAINT "CK_Product_Values" CHECK (CaloriesPer100gScaled BETWEEN 0 AND 100000 AND ProteinPer100gScaled BETWEEN 0 AND 10000 AND FatPer100gScaled BETWEEN 0 AND 10000 AND CarbsPer100gScaled BETWEEN 0 AND 10000 AND ProteinPer100gScaled + FatPer100gScaled + CarbsPer100gScaled <= 10000 AND PricePer100gScaled BETWEEN 0 AND 100000000)

);
```

```sql
CREATE UNIQUE INDEX "IX_Products_NormalizedName" ON "Products" ("NormalizedName");
```

### Recipes

```sql
CREATE TABLE "Recipes" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_Recipes" PRIMARY KEY AUTOINCREMENT,

    "Name" TEXT NOT NULL,

    "MealType" INTEGER NOT NULL,

    "DishCategory" TEXT NULL,

    "Instructions" TEXT NOT NULL,

    "CookingMinutes" INTEGER NOT NULL,

    CONSTRAINT "CK_Recipe_Values" CHECK (MealType IN (0,1,2) AND CookingMinutes BETWEEN 1 AND 1440)

);
```

### UserExcludedProducts

```sql
CREATE TABLE "UserExcludedProducts" (

    "UserId" INTEGER NOT NULL,

    "ProductId" INTEGER NOT NULL,

    CONSTRAINT "PK_UserExcludedProducts" PRIMARY KEY ("UserId", "ProductId"),

    CONSTRAINT "FK_UserExcludedProducts_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,

    CONSTRAINT "FK_UserExcludedProducts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT

);
```

```sql
CREATE INDEX "IX_UserExcludedProducts_ProductId" ON "UserExcludedProducts" ("ProductId");
```

### Users

```sql
CREATE TABLE "Users" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,

    "DisplayName" TEXT NOT NULL,

    "Login" TEXT NOT NULL,

    "NormalizedLogin" TEXT NOT NULL,

    "PasswordHash" TEXT NOT NULL,

    "Sex" INTEGER NULL,

    "Age" INTEGER NULL,

    "HeightCmScaled" INTEGER NULL,

    "WeightKgScaled" INTEGER NULL,

    "ActivityLevel" INTEGER NULL,

    "WeeklyBudgetScaled" INTEGER NOT NULL,

    CONSTRAINT "CK_User_Profile" CHECK ((Sex IS NULL OR Sex IN (0,1)) AND (Age IS NULL OR Age BETWEEN 18 AND 100) AND (HeightCmScaled IS NULL OR HeightCmScaled BETWEEN 10000 AND 25000) AND (WeightKgScaled IS NULL OR WeightKgScaled BETWEEN 3000 AND 30000) AND (ActivityLevel IS NULL OR ActivityLevel BETWEEN 0 AND 4) AND WeeklyBudgetScaled BETWEEN 0 AND 10000000000)

);
```

```sql
CREATE UNIQUE INDEX "IX_Users_NormalizedLogin" ON "Users" ("NormalizedLogin");
```

### WeeklyMenus

```sql
CREATE TABLE "WeeklyMenus" (

    "Id" INTEGER NOT NULL CONSTRAINT "PK_WeeklyMenus" PRIMARY KEY AUTOINCREMENT,

    "MenuPlanId" INTEGER NOT NULL,

    "Date" TEXT NOT NULL,

    "MealType" INTEGER NOT NULL,

    "RecipeId" INTEGER NOT NULL,

    "RecipeNameSnapshot" TEXT NOT NULL,

    "InstructionsSnapshot" TEXT NOT NULL,

    "CookedAtUtc" TEXT NULL,

    CONSTRAINT "CK_Menu_Meal" CHECK (MealType IN (0,1,2)),

    CONSTRAINT "FK_WeeklyMenus_MenuPlans_MenuPlanId" FOREIGN KEY ("MenuPlanId") REFERENCES "MenuPlans" ("Id") ON DELETE CASCADE,

    CONSTRAINT "FK_WeeklyMenus_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE RESTRICT

);
```

```sql
CREATE UNIQUE INDEX "IX_WeeklyMenus_MenuPlanId_Date_MealType" ON "WeeklyMenus" ("MenuPlanId", "Date", "MealType");
CREATE INDEX "IX_WeeklyMenus_RecipeId" ON "WeeklyMenus" ("RecipeId");
```

### __EFMigrationsHistory

```sql
CREATE TABLE "__EFMigrationsHistory" (

    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,

    "ProductVersion" TEXT NOT NULL

);
```

## Гарантии и жизненный цикл

Foreign keys включены в строке подключения, ожидание занятости — 5 секунд, pooling отключен.
Один экземпляр для абсолютного пути обеспечивает InstanceLease до миграций.
Каскад: Recipe → Ingredient и MenuPlan → WeeklyMenu → MenuIngredientSnapshot.
Ссылки из истории на Product/Recipe защищены RESTRICT, удаление учетных записей не предоставлено.
Составной PK исключений и уникальные индексы предотвращают дубли.
Версия плана — программный concurrency token; сервис увеличивает ее при изменении меню.

21 слот, принадлежность владельцу, соответствие категории, допустимая неделя,
достаточность запасов, исключения и полнота профиля — гарантии сервисов, не одного CHECK.
Рецепт и его ингредиенты, перегенерация, ручная замена и приготовление сохраняются в транзакциях.
Ни EnsureCreated, ни скрытый сброс рабочей БД не используются.

Каждое обращение сервиса создает отдельный AppDbContext. Общая Gate сериализует операции
одного DatabaseRuntime. Во время чистого поиска контекст и транзакция не удерживаются.
Revision проверяется перед записью результата и увеличивается после успешного commit;
пересечение локальной даты во время поиска также требует повторного запуска.

Редактирование файла сторонней программой одновременно с HomeMenu не поддерживается.
Файл БД и журналы не входят в git. Пароли хранятся только в виде salted IdentityV3-хеша.
