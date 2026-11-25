param([switch]$SkipUi, [switch]$SkipRestore, [switch]$ExportEvidence)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$run = Join-Path $root ('artifacts\verification\' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
New-Item -ItemType Directory -Force -Path $run | Out-Null
$steps = [System.Collections.Generic.List[object]]::new()
function Run-Step([string]$name, [string[]]$arguments) {
    Write-Host "Running $name..."
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $previous=$ErrorActionPreference
    try { $ErrorActionPreference='Continue'; $lines=@(& dotnet @arguments 2>&1 | ForEach-Object { $_.ToString() }); $exitCode=$LASTEXITCODE }
    finally { $ErrorActionPreference=$previous }
    $watch.Stop()
    $lines | Set-Content -LiteralPath (Join-Path $run ($name+'.log')) -Encoding UTF8
    $steps.Add([pscustomobject]@{name=$name;command='dotnet '+($arguments -join ' ');exit_code=$exitCode;seconds=[Math]::Round($watch.Elapsed.TotalSeconds,3)})
    if($exitCode -ne 0) { $lines | Select-Object -Last 25 | Write-Host; throw "$name failed: $exitCode" }
    Write-Host "$name PASS"
}
Push-Location $root
try {
    $commit=(& git rev-parse HEAD).Trim()
    $state=if(@(& git status --porcelain).Count -eq 0){'clean'}else{'dirty'}
    if(-not $SkipRestore) { Run-Step 'restore' @('restore','HomeMenu.sln','--locked-mode') }
    Run-Step 'build' @('build','HomeMenu.sln','-c','Release','--no-restore','-warnaserror')
    Run-Step 'tests' @('test','tests/HomeMenu.Tests/HomeMenu.Tests.csproj','-c','Release','--no-build','--no-restore','--logger','trx;LogFileName=tests.trx','--results-directory',$run)
    [xml]$trx=Get-Content -LiteralPath (Join-Path $run 'tests.trx') -Raw -Encoding UTF8
    $counts=$trx.TestRun.ResultSummary.Counters
    $rows=@($trx.TestRun.Results.UnitTestResult | Sort-Object testName | ForEach-Object { [pscustomobject]@{test=$_.testName;outcome=$_.outcome;duration=$_.duration} })
    $rows | Export-Csv -LiteralPath (Join-Path $run 'test-cases.csv') -NoTypeInformation -Encoding UTF8
    $groups=@()
    if(-not $SkipUi) {
        $tool='tests/HomeMenu.UiSmoke/bin/Release/net8.0-windows/HomeMenu.UiSmoke.dll'
        Run-Step 'wpf' @($tool)
        Run-Step 'restart' @($tool,'--restart')
        Run-Step 'process-lock' @($tool,'--process-lock')
        foreach($name in @('ui-smoke','restart','process-lock')) {
            $source=Join-Path $root ('artifacts\'+$name+'.txt')
            $lines=@(Get-Content -LiteralPath $source -Encoding UTF8 | Where-Object { $_ -notmatch '^DATABASE ' })
            $lines | Set-Content -LiteralPath (Join-Path $run ($name+'.txt')) -Encoding UTF8
            $groups += [pscustomobject]@{name=$name;passed=@($lines|Where-Object {$_ -match '^PASS '}).Count;failed=@($lines|Where-Object {$_ -match '^FAIL '}).Count}
        }
    }
    $summary=[ordered]@{date_utc=[DateTime]::UtcNow.ToString('o');code_commit=$commit;source_state=$state;os=[Environment]::OSVersion.VersionString;sdk=(& dotnet --version).Trim();tests=[ordered]@{total=[int]$counts.total;passed=[int]$counts.passed;failed=[int]$counts.failed;skipped=[int]$counts.notExecuted};ui=$groups;manual='Native confirmation dialogs and physical PasswordBox input were not exercised.';steps=$steps.ToArray()}
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding UTF8
    & dotnet --info | Set-Content -LiteralPath (Join-Path $run 'environment.log') -Encoding UTF8
    if($ExportEvidence) {
        $evidence=Join-Path $root 'docs\report\evidence'
        New-Item -ItemType Directory -Force -Path $evidence | Out-Null
        foreach($name in @('summary.json','test-cases.csv')) { Copy-Item -LiteralPath (Join-Path $run $name) -Destination (Join-Path $evidence $name) -Force }
        if(-not $SkipUi) {
            foreach($name in @('ui-smoke','restart','process-lock')) { Copy-Item -LiteralPath (Join-Path $run ($name+'.txt')) -Destination (Join-Path $evidence ($name+'.txt')) -Force }
            Copy-Item -Path (Join-Path $root 'artifacts\screenshots\*.png') -Destination (Join-Path $root 'docs\screenshots') -Force
        }
    }
    Write-Host "PASS $($counts.passed)/$($counts.total) xUnit cases; results: $run"
} finally { Pop-Location }
