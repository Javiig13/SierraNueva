[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Restore', 'Save')]
    [string]$Mode,

    [Parameter(Mandatory)]
    [string]$PublicDirectory,

    [string]$RepositoryDirectory = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = (Resolve-Path -LiteralPath $RepositoryDirectory).Path
$branch = 'public-data'
$files = @('promotions.json', 'promotions.csv', 'promotions.geojson', 'changes.json', 'run.json')
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Invoke-CheckedGit([string]$Directory, [string[]]$Arguments) {
    $output = & git --no-pager -C $Directory @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git falló ($LASTEXITCODE): $($Arguments[0])."
    }
    return $output
}

function Assert-PublicDataset([string]$Directory) {
    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $file) -PathType Leaf)) {
            throw "Falta el archivo público obligatorio: $file."
        }
    }
    $json = Get-Content -Raw -LiteralPath (Join-Path $Directory 'promotions.json')
    $dataset = $json | ConvertFrom-Json
    $run = Get-Content -Raw -LiteralPath (Join-Path $Directory 'run.json') | ConvertFrom-Json
    $changes = Get-Content -Raw -LiteralPath (Join-Path $Directory 'changes.json') | ConvertFrom-Json
    $geo = Get-Content -Raw -LiteralPath (Join-Path $Directory 'promotions.geojson') | ConvertFrom-Json
    if ($dataset.schemaVersion -ne '1.0' -or $run.schemaVersion -ne '1.0' -or
        $changes.schemaVersion -ne '1.0' -or [string]::IsNullOrWhiteSpace($dataset.runId) -or
        $dataset.runId -ne $run.runId -or $dataset.runId -ne $changes.runId -or
        @($dataset.promotions).Count -eq 0 -or $run.status -notin @('success', 'partialSuccess') -or
        $geo.type -ne 'FeatureCollection' -or
        @($geo.features).Count -ne @($dataset.promotions).Count) {
        throw 'El checkpoint no contiene un dataset público completo, coherente y publicable.'
    }
    if ($json.IndexOf('.test', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw 'No se puede conservar una fixture como checkpoint de producción.'
    }
    $ids = @($dataset.promotions | ForEach-Object { $_.id })
    $featureIds = @($geo.features | ForEach-Object { $_.id })
    if (@($ids | Select-Object -Unique).Count -ne $ids.Count -or
        @($featureIds | Select-Object -Unique).Count -ne $featureIds.Count -or
        @(Compare-Object $ids $featureIds).Count -gt 0) {
        throw 'Los identificadores del checkpoint y del mapa no coinciden.'
    }
    return $dataset.runId
}

$remoteBranch = @(Invoke-CheckedGit $repository @('ls-remote', '--heads', 'origin', "refs/heads/$branch"))
if ($Mode -eq 'Restore') {
    $staging = Join-Path (Join-Path $repository '.runtime') 'public-data-restore'
    if (Test-Path -LiteralPath $staging) {
        throw "Ya existe el directorio temporal de recuperación: $staging."
    }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    try {
        if ($remoteBranch.Count -gt 0) {
            Invoke-CheckedGit $repository @('fetch', '--depth=1', 'origin', "refs/heads/$branch") | Out-Null
            foreach ($file in $files) {
                $content = @(Invoke-CheckedGit $repository @('show', "FETCH_HEAD:$file")) -join "`n"
                [System.IO.File]::WriteAllText((Join-Path $staging $file), "$content`n", $utf8)
            }
        } else {
            Write-Warning 'Aún no existe public-data; se recupera el último despliegue válido de Pages.'
            $version = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
            foreach ($file in $files) {
                Invoke-WebRequest `
                    -Uri "https://javiig13.github.io/SierraNueva/data/$($file)?v=$version" `
                    -OutFile (Join-Path $staging $file) `
                    -TimeoutSec 60
            }
        }
        $runId = Assert-PublicDataset $staging
        New-Item -ItemType Directory -Path $PublicDirectory -Force | Out-Null
        foreach ($file in $files) {
            Copy-Item -LiteralPath (Join-Path $staging $file) -Destination (Join-Path $PublicDirectory $file) -Force
        }
        Write-Host "Checkpoint público recuperado: $runId. No contiene estado privado."
    } finally {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    return
}

$public = (Resolve-Path -LiteralPath $PublicDirectory).Path
$runId = Assert-PublicDataset $public
$worktree = Join-Path (Join-Path $repository '.runtime') 'public-data-worktree'
if (Test-Path -LiteralPath $worktree) {
    throw "Ya existe el worktree de publicación: $worktree."
}
if ($remoteBranch.Count -gt 0) {
    Invoke-CheckedGit $repository @('fetch', '--depth=1', 'origin', "refs/heads/$branch") | Out-Null
    Invoke-CheckedGit $repository @('worktree', 'add', '--detach', $worktree, 'FETCH_HEAD') | Out-Null
} else {
    Invoke-CheckedGit $repository @('worktree', 'add', '--detach', '--no-checkout', $worktree, 'HEAD') | Out-Null
    Invoke-CheckedGit $worktree @('switch', '--orphan', $branch) | Out-Null
}
$unexpected = @(Get-ChildItem -LiteralPath $worktree -Force |
    Where-Object { $_.Name -ne '.git' -and $_.Name -notin $files })
if ($unexpected.Count -gt 0) {
    throw 'La rama public-data contiene archivos ajenos a los cinco contratos públicos.'
}
foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $public $file) -Destination (Join-Path $worktree $file) -Force
}
Invoke-CheckedGit $worktree (@('add', '--') + $files) | Out-Null
& git --no-pager -C $worktree diff --cached --quiet
$diffExit = $LASTEXITCODE
if ($diffExit -eq 1) {
    Invoke-CheckedGit $worktree @(
        '-c', 'user.name=github-actions[bot]',
        '-c', 'user.email=41898282+github-actions[bot]@users.noreply.github.com',
        'commit', '-m', "Preserve public dataset $runId",
        '-m', 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
    ) | Out-Null
} elseif ($diffExit -ne 0) {
    throw "No se pudieron comprobar los cambios del checkpoint ($diffExit)."
}
Invoke-CheckedGit $worktree @('push', 'origin', "HEAD:refs/heads/$branch") | Out-Null
Invoke-CheckedGit $repository @('worktree', 'remove', $worktree) | Out-Null
Write-Host "Checkpoint público conservado en ${branch}: $runId. La rama principal no se modifica."
