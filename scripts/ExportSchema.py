"""Export schema only (no user rows) from a migrated SQLite file to the report."""
import sqlite3
import sys
from pathlib import Path

connection = sqlite3.connect(f"file:{Path(sys.argv[1]).as_posix()}?mode=ro", uri=True)
tables = connection.execute("SELECT name,sql FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name").fetchall()
parts = ["""# Физическая база данных

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
"""]
for name, sql in tables:
    parts.append(f"\n### {name}\n\n```sql\n{sql};\n```\n")
    indexes = connection.execute("SELECT sql FROM sqlite_master WHERE type='index' AND tbl_name=? AND sql IS NOT NULL ORDER BY name", (name,)).fetchall()
    if indexes:
        parts.append("\n```sql\n" + "\n".join(sql[0] + ";" for sql in indexes) + "\n```\n")
parts.append("""
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
""")
Path("docs/report/DATABASE.md").write_text("".join(parts), encoding="utf-8")
