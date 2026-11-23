param([switch]$SelfContained)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $workspace
try {
    $destination = Join-Path $workspace 'artifacts\DiveHubManagement'
    $standalone = if ($SelfContained) { 'true' } else { 'false' }
    dotnet publish DiveCenterManager\DiveCenterManager.csproj -c Release -r win-x64 --self-contained $standalone -o $destination
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $workspace 'README.md') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $workspace 'docs') -Destination $destination -Recurse -Force
    Write-Output ('Run: ' + (Join-Path $destination 'DiveCenterManager.exe'))
}
finally { Pop-Location }
