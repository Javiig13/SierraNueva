$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) "sierranueva-actors-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $root | Out-Null
$catalogPath = Join-Path $root 'actors.json'
$sourcesPath = Join-Path $root 'sources.json'
$discoveryPath = Join-Path $root 'discovery.json'
$sync = Join-Path $PSScriptRoot 'sync-market-actors.ps1'
try {
    $catalog = @{
        schemaVersion = '1.0'; checkedAtUtc = '2026-01-01T00:00:00Z'; sources = @()
        actors = @(@{
            id = 'known'; name = 'Known'; officialWebsite = 'https://www.example.test/'
            reviewStatus = 'candidate'; sourceIds = @(); evidenceUrls = @('https://example.test/')
            monitoredSourceIds = @()
        })
    }
    $catalog | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $catalogPath
    @{ sources = @(@{ id = 'extract'; name = 'Known'; baseUrl = 'https://example.test/'; enabled = $true }) } |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $sourcesPath
    @{ sources = @() } | ConvertTo-Json | Set-Content -LiteralPath $discoveryPath
    & $sync -CatalogPath $catalogPath -SourcesPath $sourcesPath -DiscoverySourcesPath $discoveryPath
    $hash = (Get-FileHash -LiteralPath $catalogPath).Hash
    & $sync -CatalogPath $catalogPath -SourcesPath $sourcesPath -DiscoverySourcesPath $discoveryPath
    if ((Get-FileHash -LiteralPath $catalogPath).Hash -ne $hash) { throw 'Sincronización no idempotente.' }
    $result = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
    if ($result.actors.Count -ne 1 -or $result.actors[0].reviewStatus -ne 'monitored' -or
        $result.actors[0].monitoredSourceIds[0] -ne 'extract' -or
        $result.checkedAtUtc.ToString('yyyy-MM-dd') -ne '2026-01-01') {
        throw 'La sincronización no preservó evidencia o no enlazó la extracción.'
    }
    $result.actors[0].reviewStatus = 'rejected'
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $catalogPath
    $before = (Get-FileHash -LiteralPath $catalogPath).Hash
    $refused = $false
    try { & $sync -CatalogPath $catalogPath -SourcesPath $sourcesPath -DiscoverySourcesPath $discoveryPath } catch { $refused = $true }
    if (-not $refused -or (Get-FileHash -LiteralPath $catalogPath).Hash -ne $before) {
        throw 'Se sobrescribió una decisión humana o se escribió ante conflicto.'
    }
    Write-Output 'Actores: idempotencia, enlace por host y preservación de decisiones correctos, sin red.'
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if ($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
