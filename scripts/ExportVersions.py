"""Document exact resolved packages; run after dotnet restore."""
import json
import re
import subprocess
from pathlib import Path

packages = {}
for path in Path('.').glob('**/obj/project.assets.json'):
    for key, entry in json.loads(path.read_text(encoding='utf-8')).get('libraries', {}).items():
        if entry['type'] == 'package':
            name, version = key.rsplit('/', 1)
            packages.setdefault(name, set()).add(version)
sdk = subprocess.run(['dotnet', '--version'], capture_output=True, text=True, check=True).stdout.strip()
smoke = Path('artifacts/ui-smoke.txt').read_text(encoding='utf-8')
sqlite = re.search(r'SQLite runtime ([^\r\n]+)', smoke).group(1)
text = f"""# Версии и среда проверки

Исходная работа: C# 12, .NET 8, WPF, EF Core **8.0.10**, SQLite 3, Visual Studio 2022.
Реализация сохраняет платформу и использует EF/Design/dotnet-ef **8.0.24**.
Номер выбран как проверенный доступный исправляющий выпуск ветки 8, без wildcard.

| Компонент | Фактически использовано |
| --- | --- |
| C# | 12.0, LangVersion в Directory.Build.props |
| Core / Infrastructure / Tests | net8.0 |
| Wpf / UiSmoke | net8.0-windows, UseWPF=true |
| SDK сборки | {sdk} |
| Runtime приложения / тестов | .NET 8.0.24, WindowsDesktop.App 8.0.24 |
| Прочие установленные SDK | 9.0.311 (для этой проверки не использован) |
| OS | Windows 11, build 10.0.26200, x64 |
| SQLite native runtime | {sqlite}, SELECT sqlite_version() через Microsoft.Data.Sqlite |
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
"""
text += '\n'.join(f'| {name} | {", ".join(sorted(versions))} |' for name, versions in sorted(packages.items()))
Path('docs/report/VERSIONS.md').write_text(text+'\n', encoding='utf-8')
