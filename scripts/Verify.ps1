param([switch]$SkipUi)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    New-Item -ItemType Directory -Force -Path artifacts | Out-Null
    dotnet restore HomeMenu.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    dotnet build HomeMenu.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet test tests/HomeMenu.Tests/HomeMenu.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    if (-not $SkipUi) {
        dotnet run --project tests/HomeMenu.UiSmoke/HomeMenu.UiSmoke.csproj -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'WPF smoke failed' }
    }
} finally { Pop-Location }
