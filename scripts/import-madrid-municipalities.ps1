param(
    [Parameter(Mandatory = $true)]
    [string]$SourceCsv,

    [string]$MunicipalitiesPath = "config/municipalities.json",

    [string]$CentroidSourcesPath = "config/municipality-centroids.json",

    [string]$FixturePath =
        "test-data/geography/ign-ngmep-municipalities-madrid-2026.csv",

    [datetime]$CheckedAtUtc = [datetime]::UtcNow
)

$ErrorActionPreference = "Stop"

function Resolve-WorkspacePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

function Convert-ToInvariantDouble {
    param([string]$Value)

    return [double]::Parse(
        $Value.Replace(",", "."),
        [System.Globalization.CultureInfo]::InvariantCulture)
}

function Write-Utf8WithoutBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = [System.IO.Path]::GetDirectoryName($Path)
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Get-NonBlankValues {
    param([object[]]$Values)

    return @($Values |
            ForEach-Object { [string]$_ } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

$sourcePath = Resolve-WorkspacePath $SourceCsv
$municipalitiesTarget = Resolve-WorkspacePath $MunicipalitiesPath
$centroidsTarget = Resolve-WorkspacePath $CentroidSourcesPath
$fixtureTarget = Resolve-WorkspacePath $FixturePath

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "No existe el CSV oficial: $sourcePath"
}

$windows1252 = [System.Text.Encoding]::GetEncoding(1252)
$sourceText = [System.IO.File]::ReadAllText($sourcePath, $windows1252)
$sourceRows = @($sourceText |
        ConvertFrom-Csv -Delimiter ";" |
        Where-Object { $_.COD_PROV -eq "28" } |
        Sort-Object NOMBRE_ACTUAL)

if ($sourceRows.Count -ne 179) {
    throw "Se esperaban 179 municipios de Madrid y se obtuvieron $($sourceRows.Count)."
}

$existingRoot = Get-Content -Raw -LiteralPath $municipalitiesTarget |
    ConvertFrom-Json
$existingByName = @{}
foreach ($municipality in $existingRoot.municipalities) {
    $existingByName[$municipality.officialName] = $municipality
}

$municipalities = foreach ($row in $sourceRows) {
    $existing = $existingByName[[string]$row.NOMBRE_ACTUAL]
    $aliases = [object[]]@(Get-NonBlankValues @($existing.aliases))
    $localities = [object[]]@(Get-NonBlankValues @($existing.localities))
    $searchTerms = [object[]]@(Get-NonBlankValues @($existing.searchTerms))
    $existingRecognitionTerms =
        [object[]]@(Get-NonBlankValues @($existing.recognitionTerms))
    $recognitionTerms = [object[]]@(if ($existingRecognitionTerms.Count -gt 0) {
        $existingRecognitionTerms
    }
    elseif ($row.NOMBRE_ACTUAL -eq "Madrid") {
        @(
            "Madrid capital",
            "municipio de Madrid",
            "Ayuntamiento de Madrid"
        )
    }
    else {
        @()
    })
    $existingRecognitionExclusions =
        [object[]]@(Get-NonBlankValues @($existing.recognitionExclusions))
    $recognitionExclusions = [object[]]@(if (
        $existingRecognitionExclusions.Count -gt 0) {
        $existingRecognitionExclusions
    }
    elseif ($row.NOMBRE_ACTUAL -eq "Madrid") {
        @(
            "Comunidad de Madrid",
            "Comunidad Autónoma de Madrid"
        )
    }
    else {
        @()
    })

    [ordered]@{
        officialName = [string]$row.NOMBRE_ACTUAL
        aliases = $aliases
        localities = $localities
        latitude = Convert-ToInvariantDouble $row.LATITUD_ETRS89_REGCAN95
        longitude = Convert-ToInvariantDouble $row.LONGITUD_ETRS89_REGCAN95
        enabled = $true
        searchTerms = $searchTerms
        recognitionTerms = $recognitionTerms
        recognitionExclusions = $recognitionExclusions
    }
}

$municipalityRoot = [ordered]@{
    _coordinatesNote =
        "Los 179 centroides proceden del NGMEP 2026 del IGN en ETRS89, " +
        "compatible con WGS84 en la península. Consulta " +
        "municipality-centroids.json para la trazabilidad CC-BY 4.0."
    _searchScopeNote =
        "Cobertura completa de los 179 municipios de la Comunidad de Madrid; " +
        "los 29 municipios originales conservan sus alias y localidades."
    municipalities = @($municipalities)
}

$checkedAt = [datetimeoffset]::new(
    $CheckedAtUtc.ToUniversalTime(),
    [timespan]::Zero)
$sourceUrl =
    "https://centrodedescargas.cnig.es/CentroDescargas/detalleArchivo?sec=9000004"
$sources = foreach ($row in $sourceRows) {
    [ordered]@{
        municipality = [string]$row.NOMBRE_ACTUAL
        sourceRecordId = [string]$row.COD_GEO
        latitude = Convert-ToInvariantDouble $row.LATITUD_ETRS89_REGCAN95
        longitude = Convert-ToInvariantDouble $row.LONGITUD_ETRS89_REGCAN95
        coordinateOrigin = [string]$row.ORIGENCOOR
        sourceUrl = $sourceUrl
        checkedAtUtc = $checkedAt.ToString("O")
    }
}

$centroidRoot = [ordered]@{
    schemaVersion = "1.1"
    coordinateReferenceSystem = "ETRS89"
    datasetName =
        "Nomenclátor Geográfico de Municipios y Entidades de Población"
    datasetEdition = "2026-03"
    sourceFile = "BD_MUNICIPIOS-ENTIDADES.ZIP"
    sourceFileSha256 =
        "496e3079d3b1844e2827d9dfc328fcd2e629e72c2640b46840daa3711b915116"
    license = "CC-BY 4.0"
    attribution =
        "Obra derivada de NGMEP 2026 CC-BY 4.0 Instituto Geográfico Nacional"
    sources = @($sources)
}

$fixtureLines = [System.Collections.Generic.List[string]]::new()
$fixtureLines.Add(
    "COD_GEO;NOMBRE_ACTUAL;LONGITUD_ETRS89;LATITUD_ETRS89;ORIGENCOOR")
foreach ($row in $sourceRows) {
    $longitude = (Convert-ToInvariantDouble $row.LONGITUD_ETRS89_REGCAN95).
        ToString("G17", [System.Globalization.CultureInfo]::InvariantCulture)
    $latitude = (Convert-ToInvariantDouble $row.LATITUD_ETRS89_REGCAN95).
        ToString("G17", [System.Globalization.CultureInfo]::InvariantCulture)
    $fixtureLines.Add(
        "$($row.COD_GEO);$($row.NOMBRE_ACTUAL);$longitude;$latitude;" +
        "$($row.ORIGENCOOR)")
}

$jsonNewLine = [Environment]::NewLine
Write-Utf8WithoutBom $municipalitiesTarget (
    ($municipalityRoot | ConvertTo-Json -Depth 10) + $jsonNewLine)
Write-Utf8WithoutBom $centroidsTarget (
    ($centroidRoot | ConvertTo-Json -Depth 10) + $jsonNewLine)
Write-Utf8WithoutBom $fixtureTarget (
    [string]::Join($jsonNewLine, $fixtureLines) + $jsonNewLine)

Write-Output (
    "Importados $($sourceRows.Count) municipios de Madrid con centroides " +
    "NGMEP 2026 y fixture reducida.")
