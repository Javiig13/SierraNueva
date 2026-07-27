param(
    [string]$MunicipalitiesPath = "config/municipalities.json",

    [string]$DiscoverySourcesPath = "config/discovery-sources.live.json"
)

$ErrorActionPreference = "Stop"

function Convert-ToSlug {
    param([string]$Value)

    $normalized = $Value.Normalize(
        [System.Text.NormalizationForm]::FormD)
    $letters = foreach ($character in $normalized.ToCharArray()) {
        $category = [System.Globalization.CharUnicodeInfo]::GetUnicodeCategory(
            $character)
        if ($category -ne
            [System.Globalization.UnicodeCategory]::NonSpacingMark) {
            $character
        }
    }

    return ((-join $letters).ToLowerInvariant() -replace "[^a-z0-9]+", "-").
        Trim("-")
}

function Resolve-WorkspacePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

$municipalitiesTarget = Resolve-WorkspacePath $MunicipalitiesPath
$discoveryTarget = Resolve-WorkspacePath $DiscoverySourcesPath
$municipalities = @(
    (Get-Content -Raw -LiteralPath $municipalitiesTarget |
            ConvertFrom-Json).municipalities |
        Where-Object enabled |
        Sort-Object officialName)
if ($municipalities.Count -ne 179) {
    throw "Se esperaban 179 municipios habilitados y hay $($municipalities.Count)."
}

$content = Get-Content -Raw -LiteralPath $discoveryTarget
$catalog = $content | ConvertFrom-Json
$sima = @($catalog.sources |
        Where-Object id -eq "sima-residential-directory")
if ($sima.Count -ne 1) {
    throw "Debe existir una única fuente sima-residential-directory."
}

$search = @($catalog.sources |
        Where-Object id -eq "zz-web-search-matrix")
if ($search.Count -ne 1) {
    throw "Debe existir una única fuente zz-web-search-matrix."
}

$slugs = [object[]]@(
    $municipalities |
        ForEach-Object { Convert-ToSlug $_.officialName })
$newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$slugLines = @($slugs | ForEach-Object { "        `"$_`"" })
$detailList = "[" + $newLine +
    [string]::Join("," + $newLine, $slugLines) + $newLine +
    "      ]"

$searchPattern =
    '(?s)("id": "zz-web-search-matrix".*?"maxItems": )\d+'
$simaDetailsPattern =
    '(?s)("id": "sima-residential-directory".*?' +
    '"detailUrlIncludes": )\[.*?\](,\r?\n      "detailContentSelectors")'
$simaMaximumPattern =
    '(?s)("id": "sima-residential-directory".*?' +
    '"maxDetailPages": )\d+'

$updated = [regex]::Replace(
    $content,
    $searchPattern,
    '${1}10000',
    1)
$updated = [regex]::Replace(
    $updated,
    $simaDetailsPattern,
    ('$1' + $detailList + '$2'),
    1)
$updated = [regex]::Replace(
    $updated,
    $simaMaximumPattern,
    '${1}200',
    1)

$verified = $updated | ConvertFrom-Json
$verifiedSima = @($verified.sources |
        Where-Object id -eq "sima-residential-directory")[0]
$verifiedSearch = @($verified.sources |
        Where-Object id -eq "zz-web-search-matrix")[0]
if (@($verifiedSima.detailUrlIncludes).Count -ne 179 -or
    $verifiedSima.maxDetailPages -ne 200 -or
    $verifiedSearch.maxItems -ne 10000) {
    throw "El catálogo resultante no contiene los límites esperados."
}

[System.IO.File]::WriteAllText(
    $discoveryTarget,
    $updated,
    [System.Text.UTF8Encoding]::new($false))

$queries =
    $municipalities.Count * @($verifiedSearch.searchQueryTemplates).Count
Write-Output (
    "Radar live sincronizado: $($municipalities.Count) municipios, " +
    "$queries consultas web y hasta $($verifiedSima.maxDetailPages) fichas SIMA.")
