#Requires -Version 5.1
<#
.SYNOPSIS
  Ejecuta el guardrail oficial de CI (check-architecture-guardrails.ps1 -SkipFrontendChunk) sobre el
  contenido PREPARADO EN STAGING, es decir, exactamente lo que incluiría el próximo commit.
.DESCRIPTION
  No define reglas propias: materializa el índice de git en un directorio temporal
  (git checkout-index) y ejecuta allí la versión preparada del guardrail oficial, el mismo
  comando que corre el job "Architecture guardrails" de CI en push. Así detecta PR-7 y el resto
  de violaciones con independencia de cambios no preparados del árbol de trabajo.

  Solo lectura respecto del repositorio: no modifica el árbol de trabajo, el índice ni la
  configuración de git, y no prepara cambios. El directorio temporal se elimina al terminar.
  Falla cerrado: cualquier error propio o del guardrail devuelve código distinto de cero y nunca
  imprime PASS.
.EXAMPLE
  pwsh -NoProfile -File tools/architecture/check-staged-guardrails.ps1
#>
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git {
    param([string[]]$Arguments)
    # stderr de git (avisos CRLF, etc.) se muestra sin convertirse en error terminante en PS 5.1;
    # el resultado se decide solo por el codigo de salida.
    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') fallo con codigo $LASTEXITCODE." }
    return $output
}

$exitCode = 1
$snapshot = $null
$previousGitDir = $env:GIT_DIR
try {
    $repoRoot = (Invoke-Git @('-C', $PSScriptRoot, 'rev-parse', '--show-toplevel') | Select-Object -First 1).Trim()
    $gitDir = (Invoke-Git @('-C', $repoRoot, 'rev-parse', '--absolute-git-dir') | Select-Object -First 1).Trim()

    $unmerged = @(Invoke-Git @('-C', $repoRoot, 'ls-files', '--unmerged'))
    if ($unmerged.Count -gt 0 -and "$($unmerged[0])".Trim()) { throw 'El indice tiene conflictos sin resolver.' }

    $staged = @(Invoke-Git @('-C', $repoRoot, 'diff', '--cached', '--name-only') | Where-Object { "$_".Trim() })
    Write-Host "Archivos preparados en staging: $($staged.Count)"
    $staged | ForEach-Object { Write-Host "  $_" }

    # Nombre corto: las rutas del repo llegan a ~160 caracteres y Windows limita a 260 (MAX_PATH).
    $snapshot = Join-Path ([IO.Path]::GetTempPath()) ("erp-sg-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $snapshot | Out-Null
    # checkout-index lee el indice y escribe solo bajo --prefix; el repositorio no cambia.
    # core.longpaths solo para esta invocacion; no altera la configuracion de git.
    $prefix = $snapshot.Replace('\', '/').TrimEnd('/') + '/'
    Invoke-Git @('-C', $repoRoot, '-c', 'core.longpaths=true', 'checkout-index', '--all', '--force', "--prefix=$prefix") | Out-Null

    $guardrail = Join-Path $snapshot 'tools/architecture/check-architecture-guardrails.ps1'
    if (-not (Test-Path -LiteralPath $guardrail -PathType Leaf)) { throw 'El guardrail oficial no esta en el indice.' }

    # Proceso hijo con el mismo motor de PowerShell; GIT_DIR permite verificar commits base (PR-7).
    $env:GIT_DIR = $gitDir
    $shell = (Get-Process -Id $PID).Path
    & $shell -NoProfile -ExecutionPolicy Bypass -File $guardrail -SkipFrontendChunk
    $childExit = $LASTEXITCODE
    if ($null -eq $childExit) { throw 'El guardrail no devolvio codigo de salida.' }
    $exitCode = [int]$childExit
}
catch {
    Write-Host "Staged guardrails ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 2
}
finally {
    $env:GIT_DIR = $previousGitDir
    if ($snapshot -and (Test-Path -LiteralPath $snapshot)) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        $full = [IO.Path]::GetFullPath($snapshot)
        if ($full.StartsWith($tempRoot) -and (Split-Path $full -Leaf) -match '^erp-sg-[0-9a-f]{8}$') {
            Remove-Item -LiteralPath $full -Recurse -Force
        }
    }
}

if ($exitCode -eq 0) {
    Write-Host 'Staged guardrails PASS (contenido preparado para el commit).' -ForegroundColor Green
}
else {
    Write-Host "Staged guardrails FAILED (exit $exitCode). No hacer commit hasta corregir." -ForegroundColor Red
}
exit $exitCode
