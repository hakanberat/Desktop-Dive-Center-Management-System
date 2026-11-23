param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $workspace
try {
    dotnet build DiveCenterManager.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $runDirectory = Join-Path $workspace ('artifacts\acceptance-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    $assembly = Join-Path $workspace 'DiveCenterManager\bin\Release\net10.0-windows\DiveCenterManager.dll'
    dotnet $assembly --self-test --data-dir $runDirectory
    if ($LASTEXITCODE -ne 0) { if (Test-Path -LiteralPath (Join-Path $runDirectory 'test-failure.txt')) { Get-Content -LiteralPath (Join-Path $runDirectory 'test-failure.txt') }; throw 'Acceptance checks failed.' }
    dotnet $assembly --verify-persistence --data-dir $runDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Restart/persistence checks failed.' }
    $acceptance = Get-Content -LiteralPath (Join-Path $runDirectory 'acceptance-results.json') -Raw | ConvertFrom-Json
    $persistence = Get-Content -LiteralPath (Join-Path $runDirectory 'persistence-results.json') -Raw | ConvertFrom-Json
    Write-Output ('Passed {0} acceptance checks and {1} restart checks.' -f $acceptance.passed, $persistence.passed)
    Write-Output ('Results: ' + $runDirectory)
}
finally { Pop-Location }
