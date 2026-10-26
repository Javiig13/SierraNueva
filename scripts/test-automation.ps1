[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Join-Path ([IO.Path]::GetTempPath()) "sierranueva-automation-$([Guid]::NewGuid().ToString('N'))"
$remote = Join-Path $root 'remote.git'
$repository = Join-Path $root 'repository'
$public = Join-Path $root 'public'
$restored = Join-Path $root 'restored'
$state = Join-Path $root 'state'
$sync = Join-Path $PSScriptRoot 'sync-public-data.ps1'
$monitor = Join-Path $PSScriptRoot 'update-monitoring-issue.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)

function Invoke-TestGit([string]$Directory, [string[]]$Arguments) {
    $output = & git --no-pager -C $Directory @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git de prueba falló: $($Arguments[0])." }
    return $output
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Write-TestJson([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), $utf8)
}

New-Item -ItemType Directory -Path $root, $repository, $public, $state -Force | Out-Null
try {
    Invoke-TestGit $root @('init', '--bare', $remote) | Out-Null
    Invoke-TestGit $repository @('init', '--initial-branch=main') | Out-Null
    Invoke-TestGit $repository @('config', 'core.autocrlf', 'false') | Out-Null
    [IO.File]::WriteAllText((Join-Path $repository '.gitignore'), ".runtime/`n", $utf8)
    Invoke-TestGit $repository @('add', '.gitignore') | Out-Null
    Invoke-TestGit $repository @(
        '-c', 'user.name=Test', '-c', 'user.email=test@example.org',
        'commit', '-m', 'Fixture inicial',
        '-m', 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
    ) | Out-Null
    Invoke-TestGit $repository @('remote', 'add', 'origin', $remote) | Out-Null
    $mainHead = Invoke-TestGit $repository @('rev-parse', 'HEAD')
    $promotion = @{
        id = 'sn-checkpoint'; canonicalUrl = 'https://example.org/promocion'
        lastSeenUtc = '2026-09-27T05:00:00Z'
    }
    $dataset = @{
        schemaVersion = '1.0'; runId = 'previous-run'
        generatedAtUtc = '2026-09-27T05:00:00Z'; promotions = @($promotion)
    }
    $report = @{
        schemaVersion = '1.0'; runId = 'previous-run'; status = 'partialSuccess'
        successfulSources = 44; totalSources = 46; failedSources = 2
        sourceResults = @(@{ sourceId = 'reviewed-source'; status = 'failed' })
    }
    Write-TestJson (Join-Path $public 'promotions.json') $dataset
    Write-TestJson (Join-Path $public 'run.json') $report
    Write-TestJson (Join-Path $public 'changes.json') @{ schemaVersion = '1.0'; runId = 'previous-run'; changes = @() }
    Write-TestJson (Join-Path $public 'promotions.geojson') @{
        type = 'FeatureCollection'; features = @(@{ id = 'sn-checkpoint'; type = 'Feature' })
    }
    [IO.File]::WriteAllText((Join-Path $public 'promotions.csv'), "id,name`nsn-checkpoint,Fixture`n", $utf8)
    [IO.File]::WriteAllText((Join-Path $public 'opportunity-candidates.json'), 'PRIVATE_SENTINEL', $utf8)

    & $sync -Mode Save -PublicDirectory $public -RepositoryDirectory $repository
    $tree = @(Invoke-TestGit $repository @('ls-tree', '--name-only', 'public-data'))
    Assert-True ($tree.Count -eq 5) 'El checkpoint debe contener exactamente cinco archivos públicos.'
    Assert-True ('opportunity-candidates.json' -notin $tree) 'Se ha filtrado estado privado al checkpoint.'
    Assert-True ((Invoke-TestGit $repository @('rev-parse', 'HEAD')) -eq $mainHead) 'La publicación modificó main.'
    Assert-True (@(Invoke-TestGit $repository @('status', '--porcelain')).Count -eq 0) 'La publicación dejó cambios en main.'

    & $sync -Mode Restore -PublicDirectory $restored -RepositoryDirectory $repository
    $recovered = Get-Content -Raw -LiteralPath (Join-Path $restored 'promotions.json') | ConvertFrom-Json
    Assert-True ($recovered.runId -eq 'previous-run') 'La recuperación perdió el identificador del dataset.'
    Assert-True (([DateTimeOffset]$recovered.promotions[0].lastSeenUtc).UtcDateTime -eq
        ([DateTimeOffset]$promotion.lastSeenUtc).UtcDateTime) 'La recuperación rejuveneció una comprobación antigua.'

    $before = Invoke-TestGit $repository @('rev-parse', 'public-data')
    & $sync -Mode Save -PublicDirectory $public -RepositoryDirectory $repository
    Assert-True ((Invoke-TestGit $repository @('rev-parse', 'public-data')) -eq $before) 'La escritura idéntica creó actividad ficticia.'
    $dataset.promotions[0].canonicalUrl = 'https://fixture.test/promocion'
    Write-TestJson (Join-Path $public 'promotions.json') $dataset
    $rejected = $false
    try { & $sync -Mode Save -PublicDirectory $public -RepositoryDirectory $repository } catch {
        $rejected = $_.Exception.Message -like '*fixture*'
    }
    Assert-True $rejected 'No se rechazó una fixture de producción.'
    Assert-True ((Invoke-TestGit $repository @('rev-parse', 'public-data')) -eq $before) 'Un fallo sustituyó el checkpoint válido.'

    $dataset.promotions[0].canonicalUrl = 'https://example.org/promocion'
    Write-TestJson (Join-Path $public 'promotions.json') $dataset
    $report.runId = 'mismatched-run'
    Write-TestJson (Join-Path $public 'run.json') $report
    $rejected = $false
    try { & $sync -Mode Save -PublicDirectory $public -RepositoryDirectory $repository } catch {
        $rejected = $_.Exception.Message -like '*coherente*'
    }
    Assert-True $rejected 'Se aceptaron archivos de ejecuciones distintas.'
    Assert-True ((Invoke-TestGit $repository @('rev-parse', 'public-data')) -eq $before) 'Un checkpoint incoherente sustituyó el válido.'

    Write-TestJson (Join-Path $state 'opportunity-triage.json') @{
        pendingCandidates = 2; highPriority = 1
        items = @(
            @{ status = 'new'; priority = 'high'; title = 'PRIVATE_TITLE'; officialUrl = 'https://private.example.org/SECRET' },
            @{ status = 'monitoring'; priority = 'medium'; title = 'OTHER_PRIVATE_TITLE' }
        )
    }
    $env:BUILD_OUTCOME = 'success'
    $env:RADAR_OUTCOME = 'success'
    $env:SEARCH_OUTCOME = 'success'
    $env:TRIAGE_OUTCOME = 'success'
    $env:PRIVATE_STATE_MISSING = 'true'
    $env:CRAWL_EXIT = '1'
    $env:GITHUB_REPOSITORY = 'example/repository'
    $env:GITHUB_RUN_ID = '123'
    $body = @(& $monitor -PublicDirectory $public -StateDirectory $state -DryRun) -join "`n"
    Assert-True ($body -like '*Atención necesaria*' -and $body -like '*1 altas nuevas sin revisar*') 'Faltan alertas de fuentes o candidatos.'
    Assert-True ($body -notmatch 'PRIVATE_TITLE|SECRET|private\.example|OTHER_PRIVATE_TITLE') 'La incidencia expone candidatos privados.'
    Assert-True ($body -like '*no la cola privada anterior*') 'Se está ocultando la pérdida de la caché privada.'
    $signaturePattern = '<!-- monitoring-signature: ([a-f0-9]{64}) -->'
    $signature = [regex]::Match($body, $signaturePattern).Groups[1].Value
    $env:GITHUB_RUN_ID = '124'
    $sameHealth = @(& $monitor -PublicDirectory $public -StateDirectory $state -DryRun) -join "`n"
    Assert-True ($signature.Length -eq 64 -and
        [regex]::Match($sameHealth, $signaturePattern).Groups[1].Value -eq $signature) 'El cambio de ejecución provoca notificaciones diarias sin novedades.'
    $report.runId = 'previous-run'
    $report.successfulSources = 46
    $report.failedSources = 0
    $report.sourceResults = @()
    Write-TestJson (Join-Path $public 'run.json') $report
    Write-TestJson (Join-Path $state 'opportunity-triage.json') @{
        pendingCandidates = 0; highPriority = 0; items = @()
    }
    $env:PRIVATE_STATE_MISSING = 'false'
    $env:CRAWL_EXIT = '0'
    $body = @(& $monitor -PublicDirectory $public -StateDirectory $state -DryRun) -join "`n"
    Assert-True ($body -like '*Comprobación operativa correcta*') 'Una recuperación sana no resuelve la alerta.'
    Assert-True ([regex]::Match($body, $signaturePattern).Groups[1].Value -ne $signature) 'La recuperación no cambia la señal operativa.'
    Write-Host 'Automatización offline correcta: checkpoint, recuperación, aislamiento, rechazo de fixtures y alertas agregadas.'
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
