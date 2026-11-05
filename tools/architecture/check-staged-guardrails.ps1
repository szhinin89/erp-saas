#Requires -Version 5.1
<#
.SYNOPSIS
  Ejecuta los controles oficiales de CI Stack Audit (scripts/ci/verify-stack-allowlist.ps1) y
  Architecture guardrails (check-architecture-guardrails.ps1 -SkipFrontendChunk) sobre el
  contenido PREPARADO EN STAGING, es decir, exactamente lo que incluiría el próximo commit.
.DESCRIPTION
  No define reglas propias: materializa el índice de git en un directorio temporal
  (git checkout-index) y ejecuta allí las versiones preparadas de ambos controles, los mismos
  pasos que corre CI en push. El snapshot contiene solo archivos versionados, como el checkout
  de CI, de modo que los resultados no dependen de cambios no preparados ni de archivos sin
  seguimiento del árbol de trabajo. Ambos controles se ejecutan siempre; PASS exige los dos.
  Fuera de alcance: el chunk Vite (-FrontendChunkOnly, requiere build) y los demás jobs de CI.

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

    # Mismos controles y argumentos que los jobs de CI en push (architecture.yml), en su orden.
    $controls = @(
        @{ Name = 'Stack Audit (allowlist)'; Script = 'scripts/ci/verify-stack-allowlist.ps1'; Arguments = @('-RepoRoot', $snapshot) },
        @{ Name = 'Architecture guardrails'; Script = 'tools/architecture/check-architecture-guardrails.ps1'; Arguments = @('-SkipFrontendChunk') }
    )
    foreach ($control in $controls) {
        $path = Join-Path $snapshot $control.Script
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Control oficial ausente del indice: $($control.Script)" }
    }

    # Procesos hijos con el mismo motor de PowerShell. GIT_DIR apunta al repositorio real para las
    # lecturas git de los controles (commits base PR-7, ls-files de /.local/); el snapshot no es repo.
    $env:GIT_DIR = $gitDir
    $shell = (Get-Process -Id $PID).Path
    $results = @()
    foreach ($control in $controls) {
        Write-Host "`n=== $($control.Name) sobre staging ===" -ForegroundColor Cyan
        $global:LASTEXITCODE = $null
        $controlArgs = $control.Arguments
        & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $snapshot $control.Script) @controlArgs
        $childExit = $LASTEXITCODE
        if ($null -eq $childExit) { throw "$($control.Name) no devolvio codigo de salida." }
        $results += [pscustomobject]@{ Control = $control.Name; Exit = [int]$childExit }
    }

    Write-Host "`nResumen de controles sobre staging:"
    $results | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Control, $(if ($_.Exit -eq 0) { 'PASS' } else { "FAIL (exit $($_.Exit))" })) }
    $exitCode = if (@($results | Where-Object { $_.Exit -ne 0 }).Count -eq 0) { 0 } else { 1 }
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
