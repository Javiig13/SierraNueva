[CmdletBinding()]
param(
    [switch]$SkipPlaywrightInstall,
    [switch]$NoFrontend,
    [switch]$VerboseCrawler
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

dotnet restore SierraNueva.sln
dotnet build SierraNueva.sln -c Release --no-restore

if (-not $SkipPlaywrightInstall) {
    $browserRoot = Join-Path $env:LOCALAPPDATA 'ms-playwright'
    $chromium = if (Test-Path -LiteralPath $browserRoot) {
        Get-ChildItem -LiteralPath $browserRoot -Directory -Filter 'chromium-*' -ErrorAction SilentlyContinue |
            Select-Object -First 1
    }

    if ($null -eq $chromium) {
        $playwrightScript = Get-ChildItem -Path $repoRoot -Filter 'playwright.ps1' -Recurse |
            Where-Object { $_.FullName -match '\\bin\\Release\\net10\.0\\' } |
            Select-Object -First 1
        if ($null -eq $playwrightScript) {
            throw 'No se encontró playwright.ps1 después de compilar.'
        }

        & $playwrightScript.FullName install chromium
    }
}

$crawlerArguments = @(
    'run',
    '--project', 'src/SierraNueva.Crawler',
    '-c', 'Release',
    '--no-build',
    '--',
    'crawl',
    '--no-playwright'
)
if ($VerboseCrawler) {
    $crawlerArguments += '--verbose'
}

& dotnet @crawlerArguments
if ($LASTEXITCODE -gt 1) {
    throw "El crawler terminó con código $LASTEXITCODE."
}

dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-data
dotnet build src/SierraNueva.Web/SierraNueva.Web.csproj -c Release --no-restore

if (-not $NoFrontend) {
    Write-Host 'SierraNueva estará disponible en la URL que indique el servidor.'
    dotnet run --project src/SierraNueva.Web -c Release --no-build
}
