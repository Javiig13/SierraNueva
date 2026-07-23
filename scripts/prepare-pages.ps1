[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$WebRoot,

    [string]$BasePath = '/SierraNueva/'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $BasePath.StartsWith('/', [StringComparison]::Ordinal) -or
    -not $BasePath.EndsWith('/', [StringComparison]::Ordinal) -or
    $BasePath.IndexOf('..', [StringComparison]::Ordinal) -ge 0) {
    throw "BasePath debe empezar y terminar por '/' y no puede contener '..'."
}

$resolvedWebRoot = (Resolve-Path -LiteralPath $WebRoot).Path
$indexPath = Join-Path $resolvedWebRoot 'index.html'
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "No existe el index publicado: $indexPath"
}

$index = Get-Content -Raw -LiteralPath $indexPath
$rootBase = '<base href="/">'
if ($index.IndexOf($rootBase, [StringComparison]::Ordinal) -lt 0) {
    throw "index.html no contiene la base esperada '$rootBase'."
}

$pagesIndex = $index.Replace($rootBase, "<base href=`"$BasePath`">")
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($indexPath, $pagesIndex, $utf8WithoutBom)

$fallbackPath = Join-Path $resolvedWebRoot '404.html'
[System.IO.File]::WriteAllText($fallbackPath, $pagesIndex, $utf8WithoutBom)

$noJekyllPath = Join-Path $resolvedWebRoot '.nojekyll'
[System.IO.File]::WriteAllText($noJekyllPath, '', $utf8WithoutBom)

$requiredFiles = @(
    'index.html',
    '404.html',
    '.nojekyll',
    '_framework/blazor.webassembly.js',
    'data/promotions.json',
    'data/promotions.csv',
    'data/promotions.geojson',
    'data/changes.json',
    'data/run.json'
)
foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $resolvedWebRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Falta un archivo obligatorio de Pages: $relativePath"
    }
}

$privateState = Get-ChildItem -LiteralPath $resolvedWebRoot -File -Recurse |
    Where-Object {
        $_.Name -like 'promotions-state*.json' -or
        $_.Name -like 'opportunity-candidates*.json' -or
        $_.Name -in @('geocoding-cache.json', 'http-cache.json') -or
        $_.FullName -match '[\\/]data[\\/]state[\\/]'
    }
if ($privateState) {
    $relativePrivatePaths = $privateState |
        ForEach-Object {
            [System.IO.Path]::GetRelativePath($resolvedWebRoot, $_.FullName)
        }
    throw "El artefacto contiene estado privado: $($relativePrivatePaths -join ', ')"
}

Write-Host "Artefacto Pages válido para '$BasePath'; fallback SPA y .nojekyll presentes."
