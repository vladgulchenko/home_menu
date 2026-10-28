# Версии и среда проверки

Исходная работа: C# 12, .NET 8, WPF, EF Core **8.0.10**, SQLite 3, Visual Studio 2022.
Реализация сохраняет платформу и использует EF/Design/dotnet-ef **8.0.24**.
Номер выбран как проверенный доступный исправляющий выпуск ветки 8, без wildcard.

| Компонент | Фактически использовано |
| --- | --- |
| C# | 12.0, LangVersion в Directory.Build.props |
| Core / Infrastructure / Tests | net8.0 |
| Wpf / UiSmoke | net8.0-windows, UseWPF=true |
| SDK сборки | 10.0.103 |
| Runtime приложения / тестов | .NET 8.0.24, WindowsDesktop.App 8.0.24 |
| Прочие установленные SDK | 9.0.311 (для этой проверки не использован) |
| OS | Windows 11, build 10.0.26200, x64 |
| SQLite native runtime | 3.41.2, SELECT sqlite_version() через Microsoft.Data.Sqlite |
| dotnet-ef | 8.0.24, корневой dotnet-tools.json |
| Visual Studio | 2022 по заданию; фактический запуск IDE не проверялся |
| PasswordHasher | Microsoft.Extensions.Identity.Core 8.0.24, IdentityV3, 100000 итераций |
| Python | 3.12.7, только экспорт документации; приложению не нужен |

Нет global.json, чтобы не навязывать SDK 10 Visual Studio 2022. Для VS установить
совместимый SDK .NET 8 и workload WPF. Точные зависимости записаны в csproj,
packages.lock.json и ниже; восстановление можно проверить `dotnet restore HomeMenu.sln --locked-mode`.

## Разрешенные NuGet-пакеты

Таблица получена scripts/ExportVersions.py из реальных project.assets.json после restore
всех пяти проектов, включая тестовые и design-time зависимости.

| Пакет | Версия |
| --- | --- |
| Humanizer.Core | 2.14.1 |
| Microsoft.AspNetCore.Cryptography.Internal | 8.0.24 |
| Microsoft.AspNetCore.Cryptography.KeyDerivation | 8.0.24 |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 |
| Microsoft.CodeAnalysis.Analyzers | 3.3.3 |
| Microsoft.CodeAnalysis.CSharp | 4.5.0 |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 4.5.0 |
| Microsoft.CodeAnalysis.Common | 4.5.0 |
| Microsoft.CodeAnalysis.Workspaces.Common | 4.5.0 |
| Microsoft.CodeCoverage | 17.11.1 |
| Microsoft.Data.Sqlite.Core | 8.0.24 |
| Microsoft.EntityFrameworkCore | 8.0.24 |
| Microsoft.EntityFrameworkCore.Abstractions | 8.0.24 |
| Microsoft.EntityFrameworkCore.Analyzers | 8.0.24 |
| Microsoft.EntityFrameworkCore.Design | 8.0.24 |
| Microsoft.EntityFrameworkCore.Relational | 8.0.24 |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.24 |
| Microsoft.EntityFrameworkCore.Sqlite.Core | 8.0.24 |
| Microsoft.Extensions.Caching.Abstractions | 8.0.0 |
| Microsoft.Extensions.Caching.Memory | 8.0.1 |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 |
| Microsoft.Extensions.DependencyModel | 8.0.2 |
| Microsoft.Extensions.Identity.Core | 8.0.24 |
| Microsoft.Extensions.Logging | 8.0.1 |
| Microsoft.Extensions.Logging.Abstractions | 8.0.2 |
| Microsoft.Extensions.Options | 8.0.2 |
| Microsoft.Extensions.Primitives | 8.0.0 |
| Microsoft.NET.Test.Sdk | 17.11.1 |
| Microsoft.TestPlatform.ObjectModel | 17.11.1 |
| Microsoft.TestPlatform.TestHost | 17.11.1 |
| Mono.TextTemplating | 2.2.1 |
| Newtonsoft.Json | 13.0.1 |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.6 |
| SQLitePCLRaw.core | 2.1.6 |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.6 |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.6 |
| System.CodeDom | 4.4.0 |
| System.Collections.Immutable | 6.0.0 |
| System.Composition | 6.0.0 |
| System.Composition.AttributedModel | 6.0.0 |
| System.Composition.Convention | 6.0.0 |
| System.Composition.Hosting | 6.0.0 |
| System.Composition.Runtime | 6.0.0 |
| System.Composition.TypedParts | 6.0.0 |
| System.IO.Pipelines | 6.0.3 |
| System.Memory | 4.5.3 |
| System.Reflection.Metadata | 1.6.0, 6.0.1 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 |
| System.Text.Encoding.CodePages | 6.0.0 |
| System.Threading.Channels | 6.0.0 |
| xunit | 2.9.2 |
| xunit.abstractions | 2.0.3 |
| xunit.analyzers | 1.16.0 |
| xunit.assert | 2.9.2 |
| xunit.core | 2.9.2 |
| xunit.extensibility.core | 2.9.2 |
| xunit.extensibility.execution | 2.9.2 |
| xunit.runner.visualstudio | 2.8.2 |
