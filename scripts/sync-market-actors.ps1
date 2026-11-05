param(
    [string]$CatalogPath = 'config/market-actors.json',
    [string]$SourcesPath = 'config/sources.live.json',
    [string]$DiscoverySourcesPath = 'config/discovery-sources.live.json'
)
$ErrorActionPreference = 'Stop'

function Get-HostName([string]$Url) {
    if ([string]::IsNullOrWhiteSpace($Url)) { return '' }
    $uri = [uri]$Url
    if ($uri.Scheme -ne 'https') { throw "Se requiere HTTPS: $Url" }
    return $uri.IdnHost.ToLowerInvariant() -replace '^www\.', ''
}

$originalJson = Get-Content -Raw -LiteralPath $CatalogPath
$catalog = $originalJson | ConvertFrom-Json
$catalog.checkedAtUtc = [regex]::Match($originalJson, '"checkedAtUtc"\s*:\s*"([^"]+)"').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($catalog.checkedAtUtc)) { throw 'Falta fecha de evidencia del catálogo.' }
$sources = (Get-Content -Raw -LiteralPath $SourcesPath | ConvertFrom-Json).sources
$discovery = (Get-Content -Raw -LiteralPath $DiscoverySourcesPath | ConvertFrom-Json).sources
$channels = @($sources | Where-Object enabled | ForEach-Object {
    [pscustomobject]@{ id = $_.id; hostName = Get-HostName $_.baseUrl; name = $_.name }
}) + @($discovery | Where-Object {
    $_.enabled -and $_.sourceKind -eq 'officialCommercialWebsite'
} | ForEach-Object {
    [pscustomobject]@{ id = $_.id; hostName = Get-HostName $_.urlTemplate; name = $_.name }
})
$provenance = 'sierranueva-extraction-catalog'
if ($catalog.sources.id -notcontains $provenance) {
    $catalog.sources = @($catalog.sources) + [pscustomobject]@{
        id = $provenance; name = 'SierraNueva — extracción comercial revisada'
        url = 'https://github.com/Javiig13/SierraNueva/blob/main/config/sources.live.json'
    }
}
$actors = [Collections.Generic.List[object]]::new()
foreach ($actor in $catalog.actors) { $actors.Add($actor) }
foreach ($group in ($channels | Group-Object hostName | Sort-Object Name)) {
    $matching = @($actors | Where-Object { (Get-HostName $_.officialWebsite) -eq $group.Name })
    if ($matching.reviewStatus -contains 'rejected') {
        throw "Conflicto con decisión humana para $($group.Name); revisar antes de sincronizar."
    }
    if ($matching.Count -eq 0) {
        $first = $group.Group | Sort-Object id | Select-Object -First 1
        $id = "$($first.id)-actor"
        if ($actors.id -contains $id) { throw "Id de actor duplicado: $id" }
        $newActor = [pscustomobject]@{
            id = $id; name = ([string]$first.name -split ' — ', 2)[0]
            officialWebsite = "https://$($group.Name)/"; reviewStatus = 'candidate'
            sourceIds = @($provenance); evidenceUrls = @("https://$($group.Name)/")
            monitoredSourceIds = @()
        }
        $actors.Add($newActor)
        $matching = @($newActor)
    }
    foreach ($actor in $matching) {
        $actor.reviewStatus = 'monitored'
        $actor.sourceIds = @(@($actor.sourceIds) + $provenance | Sort-Object -Unique)
        $actor.monitoredSourceIds = @(@($group.Group.id) | Sort-Object -Unique)
    }
}
# Keep directory evidence dates and rejected actors; this performs no network review.
$catalog.actors = @($actors | Sort-Object @{Expression = { $_.name.ToLowerInvariant() }}, id)
$destination = [IO.Path]::GetFullPath($CatalogPath)
$temporary = "$destination.$([guid]::NewGuid().ToString('N')).tmp"
try {
    [IO.File]::WriteAllText($temporary, ($catalog | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $destination -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Output "Inventario sincronizado sin red: $($actors.Count) actores. No acredita oferta ni salud live."
