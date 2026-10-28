# Технические источники

Основа требований — приложенные документы, перечисленные в `sources/INDEX.md`. Ни один внешний технический источник не заменяет задание и первую процентовку. Официальная документация проверена при подготовке гайда 28.10.2026; онлайн-страницы могут описывать более новые версии, поэтому Codex обязан сверять применимость к EF Core 8 и .NET 8.

| Источник | Для чего использовать |
| --- | --- |
| [Требования версии SDK .NET 8](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/version-requirements) | Совместимость SDK и Visual Studio; для net8.0 минимум VS 17.8, конкретный SDK может требовать выше |
| [Обзор WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) | Настольный Windows-интерфейс и привязки |
| [Ограничения провайдера SQLite EF Core](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations) | Ограничения decimal и автоматически генерируемых concurrency token; не переносить разделы EF9 в EF8 |
| [Типы Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types) | INTEGER/REAL/TEXT/BLOB и отображение .NET-типов, включая DateOnly и decimal |
| [Преобразования значений EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions) | Реализация явного масштабирования чисел |
| [Транзакции EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions) | Совместное сохранение нескольких связанных изменений |
| [Применение миграций](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) | Создание и обновление схемы, версия-зависимые особенности |
| [Хеширование паролей и PasswordHasher](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-8.0) | Библиотечное хеширование; не использовать простой SHA для паролей |

Числовая модель BMR и цели 30/30/40% взяты из первой процентовки, а не подаются как новая рекомендация по питанию. Пищевые значения seed должны быть обозначены учебными, а цены — оценочными. Утверждения об аналогах Mealime/Menu Planner/Yazio из первой работы не являются проверенными возможностями нашего приложения и не должны попадать в описание кода.

При будущей подготовке записки отдельно проверить первоисточник СТП 01–2024. Полный текст стандарта в комплекте не приложен; реквизиты стандарта известны из исходных работ.
