[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublicDirectory,

    [Parameter(Mandatory)]
    [string]$StateDirectory,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$reasons = [System.Collections.Generic.List[string]]::new()
$details = [System.Collections.Generic.List[string]]::new()
$title = '[SierraNueva] Revisar captación y fuentes'

if ($env:BUILD_OUTCOME -ne 'success') {
    $reasons.Add('La preparación del despliegue falló. El último despliegue válido debe conservarse.')
}
if ($env:RADAR_OUTCOME -ne 'success' -or $env:SEARCH_OUTCOME -ne 'success') {
    $reasons.Add('El radar o la matriz web no terminaron correctamente; puede haber puntos ciegos.')
}
if ($env:TRIAGE_OUTCOME -ne 'success') {
    $reasons.Add('No se pudo clasificar la cola privada de oportunidades.')
}
if ($env:PRIVATE_STATE_MISSING -eq 'true') {
    $reasons.Add('Caducó la caché privada. El checkpoint recupera el histórico comercial, no la cola privada anterior.')
}
if ($env:CRAWL_EXIT -notin @('0', '1')) {
    $reasons.Add('El crawler comercial no produjo una ejecución publicable.')
}

$runPath = Join-Path $PublicDirectory 'run.json'
if (Test-Path -LiteralPath $runPath -PathType Leaf) {
    $run = Get-Content -Raw -LiteralPath $runPath | ConvertFrom-Json
    $details.Add("Informe comercial disponible: $($run.successfulSources)/$($run.totalSources) fuentes correctas; $($run.failedSources) fallidas.")
    if ($env:CRAWL_EXIT -eq '1' -or [int]$run.failedSources -gt 0) {
        $reasons.Add('Hay fuentes comerciales sin verificar. Sus datos protegidos no confirman disponibilidad actual.')
        $failedIds = @($run.sourceResults | Where-Object status -eq 'failed' |
            ForEach-Object { $_.sourceId })
        $details.Add("Fuentes a revisar: $($failedIds -join ', ').")
    }
} else {
    $reasons.Add('No se dispone de un informe comercial para esta ejecución.')
}

$triagePath = Join-Path $StateDirectory 'opportunity-triage.json'
if (Test-Path -LiteralPath $triagePath -PathType Leaf) {
    $triage = Get-Content -Raw -LiteralPath $triagePath | ConvertFrom-Json
    $newHigh = @($triage.items | Where-Object {
        $_.status -eq 'new' -and $_.priority -eq 'high'
    }).Count
    $details.Add("Cola privada: $($triage.pendingCandidates) pendientes; $($triage.highPriority) de prioridad alta; $newHigh altas nuevas sin revisar.")
    if ($newHigh -gt 0) {
        $reasons.Add('Hay candidatos nuevos de prioridad alta pendientes de evaluación jurídica, técnica y comercial.')
    }
} else {
    $reasons.Add('No se dispone de un triaje actualizado de la cola privada.')
}

$healthy = $reasons.Count -eq 0
$heading = if ($healthy) { 'Comprobación operativa correcta' } else { 'Atención necesaria' }
$reasonText = if ($healthy) { '- Sin incidencias detectadas en este pase.' } else {
    ($reasons | ForEach-Object { "- $_" }) -join "`n"
}
$detailText = ($details | ForEach-Object { "- $_" }) -join "`n"
$hasher = [Security.Cryptography.SHA256]::Create()
try {
    $digest = $hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes("$reasonText`n$detailText"))
    $signature = [BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant()
} finally {
    $hasher.Dispose()
}
$runUrl = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
$body = @"
<!-- sierranueva-monitoring -->
<!-- monitoring-signature: $signature -->
## $heading

$reasonText

### Agregados, sin candidatos privados
$detailText

[Consultar ejecución de Actions]($runUrl).

La búsqueda descubre candidatos privados, no incorpora promociones automáticamente.
Revisar la cola mediante la exportación cifrada antes de habilitar una fuente nueva.
Un dato conservado no implica que la vivienda siga disponible.
"@
if ($DryRun) {
    Write-Output $body
    return
}
if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN) -or
    [string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) {
    throw 'Faltan GH_TOKEN o GITHUB_REPOSITORY para actualizar la incidencia operativa.'
}
$headers = @{
    Authorization = "Bearer $env:GH_TOKEN"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'SierraNueva-monitoring'
}
$query = [Uri]::EscapeDataString("repo:$env:GITHUB_REPOSITORY is:issue in:title `"$title`"")
$search = Invoke-RestMethod -Uri "https://api.github.com/search/issues?q=$query&per_page=10" -Headers $headers
$existing = @($search.items | Where-Object { $_.title -eq $title } | Select-Object -First 1)
$issueApi = "https://api.github.com/repos/$env:GITHUB_REPOSITORY/issues"
if ($existing.Count -eq 0) {
    if ($healthy) {
        Write-Host 'Vigilancia correcta; no se necesita abrir una incidencia.'
        return
    }
    $payload = @{
        title = $title
        body = $body
        assignees = @(($env:GITHUB_REPOSITORY -split '/')[0])
    } | ConvertTo-Json
    $issue = Invoke-RestMethod -Method Post -Uri $issueApi -Headers $headers `
        -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
} else {
    $previousBody = [string]$existing[0].body
    $changed = $previousBody.IndexOf("<!-- monitoring-signature: $signature -->", [StringComparison]::Ordinal) -lt 0
    $payload = @{
        body = $body
        state = $(if ($healthy) { 'closed' } else { 'open' })
    } | ConvertTo-Json
    $issue = Invoke-RestMethod -Method Patch -Uri "$issueApi/$($existing[0].number)" -Headers $headers `
        -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
    if (-not $healthy -and $changed) {
        $comment = @{ body = $body } | ConvertTo-Json
        $null = Invoke-RestMethod -Method Post -Uri "$issueApi/$($issue.number)/comments" -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($comment))
    }
}
Write-Host "Vigilancia operativa actualizada: $($issue.html_url)"
