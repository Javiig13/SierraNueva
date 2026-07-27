param(
    [string]$OutputPath = "config/market-actors.json",

    [string]$DiscoverySourcesPath = "config/discovery-sources.live.json",

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

function Convert-ToId {
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

function Convert-ToOfficialWebsite {
    param([object]$Value)

    $text = [System.Net.WebUtility]::HtmlDecode([string]$Value).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $candidate = @($text -split "[\s,;|]+" |
            Where-Object { $_ -match "\." } |
            Select-Object -First 1)[0]
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return $null
    }

    if ($candidate -notmatch "^https?://") {
        $candidate = "https://$candidate"
    }

    $uri = $null
    if (-not [uri]::TryCreate(
        $candidate,
        [System.UriKind]::Absolute,
        [ref]$uri)) {
        return $null
    }

    $hostName = $uri.Host.ToLowerInvariant()
    if ($hostName.StartsWith("www.")) {
        $hostName = $hostName.Substring(4)
    }

    if ($hostName -notmatch "\." -or
        $hostName -match "^(facebook|instagram|linkedin|x|twitter)\.com$") {
        return $null
    }

    return "https://$hostName/"
}

function Get-WebsiteHost {
    param([string]$Website)

    if ([string]::IsNullOrWhiteSpace($Website)) {
        return $null
    }

    return ([uri]$Website).Host.ToLowerInvariant() -replace "^www\.", ""
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

$target = Resolve-WorkspacePath $OutputPath
$discoveryPath = Resolve-WorkspacePath $DiscoverySourcesPath
$headers = @{
    "User-Agent" = "SierraNueva/1.0 (+https://github.com/Javiig13/SierraNueva)"
    "Referer" = "https://asprima.es/listado-de-socios/"
    "X-Requested-With" = "XMLHttpRequest"
}
$response = Invoke-RestMethod `
    -Method Post `
    -Uri "https://asprima.es/api/v1/web/search/organization/" `
    -Headers $headers `
    -ContentType "application/x-www-form-urlencoded; charset=UTF-8" `
    -Body "roles=1&limit=250&page=1&random_view=&orderby="

if ($response.total -ne @($response.users).Count -or
    @($response.users).Count -lt 150) {
    throw (
        "La respuesta de ASPRIMA está truncada o no coincide con su total: " +
        "total=$($response.total), registros=$(@($response.users).Count).")
}

$actors = [System.Collections.Generic.List[object]]::new()
$usedIds = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($organization in @($response.users) |
        Sort-Object @{ Expression = { [string]$_.first_name } }, id) {
    $name = [System.Net.WebUtility]::HtmlDecode(
        [string]$organization.first_name).Trim()
    if ([string]::IsNullOrWhiteSpace($name)) {
        continue
    }

    $id = Convert-ToId $name
    if ([string]::IsNullOrWhiteSpace($id) -or -not $usedIds.Add($id)) {
        $id = "$id-asprima-$($organization.id)"
        if (-not $usedIds.Add($id)) {
            throw "No se pudo generar un id único para '$name'."
        }
    }

    $actors.Add([ordered]@{
        id = $id
        name = $name
        officialWebsite = Convert-ToOfficialWebsite $organization.custom_WEB
        reviewStatus = "candidate"
        sourceIds = [object[]]@("asprima-members")
        evidenceUrls = [object[]]@(
            "https://asprima.es/listado-de-socios/")
        monitoredSourceIds = [object[]]@()
    })
}

$simaResponse = Invoke-WebRequest `
    -Uri "https://simaexpo.com/empresas/" `
    -Headers @{
        "User-Agent" =
            "SierraNueva/1.0 (+https://github.com/Javiig13/SierraNueva)"
    } `
    -TimeoutSec 60
$simaMatches = [regex]::Matches(
    $simaResponse.Content,
    '<h2>\s*<a\s+href=["'']' +
    '(?<url>https://simaexpo\.com/empresas/(?<slug>[^/]+)/?)["'']' +
    '[^>]*>(?<name>.*?)</a>\s*</h2>',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$simaCompanies = @($simaMatches |
        ForEach-Object {
            [ordered]@{
                id = Convert-ToId $_.Groups["slug"].Value
                name = [System.Net.WebUtility]::HtmlDecode(
                    ($_.Groups["name"].Value -replace "<[^>]+>", "")).Trim()
                url = $_.Groups["url"].Value
            }
        } |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_["id"]) -and
            -not [string]::IsNullOrWhiteSpace($_["name"])
        } |
        Sort-Object @{
            Expression = { [string]$_["id"] }
        } -Unique)
if ($simaCompanies.Count -lt 300) {
    throw (
        "El directorio SIMA devolvió solo $($simaCompanies.Count) empresas; " +
        "se esperaban al menos 300.")
}

$byId = @{}
foreach ($actor in $actors) {
    $byId[[string]$actor["id"]] = $actor
}

foreach ($company in $simaCompanies) {
    if ($byId.ContainsKey([string]$company["id"])) {
        $actor = $byId[[string]$company["id"]]
        $actor["sourceIds"] = [object[]]@(
            @($actor["sourceIds"]) + "sima-companies" |
                Sort-Object -Unique)
        $actor["evidenceUrls"] = [object[]]@(
            @($actor["evidenceUrls"]) + [string]$company["url"] |
                Sort-Object -Unique)
        continue
    }

    $id = [string]$company["id"]
    if (-not $usedIds.Add($id)) {
        throw "No se pudo generar un id único para '$($company["name"])'."
    }

    $actor = [ordered]@{
        id = $id
        name = [string]$company["name"]
        officialWebsite = $null
        reviewStatus = "candidate"
        sourceIds = [object[]]@("sima-companies")
        evidenceUrls = [object[]]@([string]$company["url"])
        monitoredSourceIds = [object[]]@()
    }
    $actors.Add($actor)
    $byId[$id] = $actor
}

$liveCatalog = Get-Content -Raw -LiteralPath $discoveryPath | ConvertFrom-Json
$commercialSources = @($liveCatalog.sources |
        Where-Object {
            $_.enabled -and
            $_.sourceKind -eq "officialCommercialWebsite"
        })
$byHost = @{}
foreach ($actor in $actors) {
    $hostName = Get-WebsiteHost $actor.officialWebsite
    if (-not [string]::IsNullOrWhiteSpace($hostName)) {
        if (-not $byHost.ContainsKey($hostName)) {
            $byHost[$hostName] =
                [System.Collections.Generic.List[object]]::new()
        }

        $byHost[$hostName].Add($actor)
    }
}

foreach ($source in $commercialSources | Sort-Object id) {
    $hostName = @($source.allowedHosts |
            ForEach-Object { ([string]$_).ToLowerInvariant() -replace "^www\.", "" } |
            Where-Object { $_ -match "\." } |
            Select-Object -First 1)[0]
    if ([string]::IsNullOrWhiteSpace($hostName)) {
        continue
    }

    $matchingActors = [object[]]@()
    if ($byHost.ContainsKey($hostName)) {
        $matchingActors = [object[]]@($byHost[$hostName].ToArray())
    }
    if ($matchingActors.Count -eq 0) {
        $name = ([string]$source.name -split "\s+[—-]\s+", 2)[0].Trim()
        $id = Convert-ToId $name
        if ([string]::IsNullOrWhiteSpace($id) -or -not $usedIds.Add($id)) {
            $id = "$id-source-$($source.id)"
            if (-not $usedIds.Add($id)) {
                throw "No se pudo generar un id único para la fuente '$($source.id)'."
            }
        }

        $actor = [ordered]@{
            id = $id
            name = $name
            officialWebsite = "https://$hostName/"
            reviewStatus = "monitored"
            sourceIds = [object[]]@("sierranueva-live-catalog")
            evidenceUrls = [object[]]@("https://$hostName/")
            monitoredSourceIds = [object[]]@([string]$source.id)
        }
        $actors.Add($actor)
        $byHost[$hostName] =
            [System.Collections.Generic.List[object]]::new()
        $byHost[$hostName].Add($actor)
        continue
    }

    foreach ($actor in $matchingActors) {
        $actor["reviewStatus"] = "monitored"
        $actor["sourceIds"] = [object[]]@(
            @($actor["sourceIds"]) + "sierranueva-live-catalog" |
                Sort-Object -Unique)
        $actor["monitoredSourceIds"] = [object[]]@(
            @($actor["monitoredSourceIds"]) + [string]$source.id |
                Sort-Object -Unique)
    }
}

$checkedAt = [datetimeoffset]::new(
    $CheckedAtUtc.ToUniversalTime(),
    [timespan]::Zero)
$catalog = [ordered]@{
    schemaVersion = "1.0"
    checkedAtUtc = $checkedAt.ToString("O")
    sources = [object[]]@(
        [ordered]@{
            id = "asprima-members"
            name = "ASPRIMA — listado de socios"
            url = "https://asprima.es/listado-de-socios/"
        },
        [ordered]@{
            id = "sierranueva-live-catalog"
            name = "SierraNueva — fuentes comerciales monitorizadas"
            url = "https://github.com/Javiig13/SierraNueva/blob/main/config/discovery-sources.live.json"
        },
        [ordered]@{
            id = "sima-companies"
            name = "SIMA — directorio de empresas"
            url = "https://simaexpo.com/empresas/"
        })
    actors = [object[]]@($actors |
            Sort-Object @{
                Expression = { ([string]$_.name).ToLowerInvariant() }
            }, id)
}

Write-Utf8WithoutBom $target (
    ($catalog | ConvertTo-Json -Depth 10) + [Environment]::NewLine)

$monitored = @($actors |
        Where-Object { $_.reviewStatus -eq "monitored" }).Count
$withWebsite = @($actors |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.officialWebsite) }).Count
Write-Output (
    "Catálogo creado: $($actors.Count) actores; $monitored monitorizados; " +
    "$withWebsite con web declarada; $($simaCompanies.Count) en SIMA. " +
    "No se han persistido datos de contacto.")
