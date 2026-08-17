<#
.SYNOPSIS
  Guard de CI/local para el Design System (ERP-DS-GUARD-SCRIPTS-01).

.DESCRIPTION
  Falla (exit 1) si encuentra reglas DS prohibidas en el scope indicado:
    - font-family local (incl. font-family-mono) en frontend/src/modules
    - font-style: italic en frontend/src/modules
    - zh-data-value--italic en frontend/src/modules
    - style={{ en frontend/src/modules
    - colores hardcodeados (#hex / rgb( / rgba() en frontend/src/modules

  Advierte (no falla) por font-size/font-weight/line-height/letter-spacing/
  text-transform sin clasificar como OK_* (NEEDS_DECISION) - esas 5
  propiedades requieren revision visual, no se pueden validar solo por regex.

.PARAMETER Scope
  modules (default) | components | styles | all

.PARAMETER ModuleName
  Opcional. Restringe el scope "modules" a frontend/src/modules/<ModuleName>.

.PARAMETER WhitelistPath
  Opcional. Archivo de texto plano, una entrada "ruta/relativa.ext:linea" por
  renglon, para excluir hallazgos puntuales ya revisados y aceptados
  explicitamente (ver seccion "Whitelist" en DS_RULES.md). No existe por
  defecto - el guard no ignora nada salvo que se liste aqui.

.EXIT
  0 = sin violaciones bloqueantes
  1 = al menos una violacion NOT_OK_VISUAL_LOCAL encontrada

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/ds/ds-guard.ps1 -Scope modules
#>

param(
    [ValidateSet("modules", "components", "styles", "all")]
    [string]$Scope = "modules",
    [string]$ModuleName,
    [string]$WhitelistPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "_ds-lib.ps1")

$root = Get-DsRepoRoot

# Whitelist opcional: lineas "archivo:linea" exentas de bloquear el guard.
# Vacia por defecto - agregar entradas aqui (o en el archivo pasado por
# -WhitelistPath) solo tras revisión humana explícita, nunca para silenciar
# un hallazgo sin corregirlo primero si es corregible.
$whitelist = New-Object System.Collections.Generic.HashSet[string]
if ($WhitelistPath -and (Test-Path $WhitelistPath)) {
    Get-Content -LiteralPath $WhitelistPath | Where-Object { $_.Trim() -and -not $_.Trim().StartsWith("#") } | ForEach-Object {
        [void]$whitelist.Add($_.Trim())
    }
}

Write-Host "ds-guard: escaneando scope='$Scope'$(if ($ModuleName) { " moduleName='$ModuleName'" })..." -ForegroundColor Cyan
$findings = Get-DsFindings -Scope $Scope -ModuleName $ModuleName

$blocking = @($findings | Where-Object {
    $_.Classification -eq "NOT_OK_VISUAL_LOCAL" -and -not $whitelist.Contains("$($_.File):$($_.Line)")
})
$warnings = @($findings | Where-Object {
    $_.Classification -eq "NEEDS_DECISION" -and
    $_.Pattern -in @("font-size", "font-weight", "line-height", "letter-spacing", "text-transform") -and
    -not $whitelist.Contains("$($_.File):$($_.Line)")
})

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "ADVERTENCIA - $($warnings.Count) hallazgo(s) de tipografia local sin clasificar (NEEDS_DECISION), requieren revision visual:" -ForegroundColor Yellow
    foreach ($w in $warnings) {
        Write-Host ("  [$($w.Pattern)] $($w.File):$($w.Line)  $($w.Content)") -ForegroundColor Yellow
    }
}

if ($blocking.Count -eq 0) {
    Write-Host ""
    Write-Host "ds-guard: OK - sin violaciones bloqueantes en scope '$Scope'." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "ds-guard: $($blocking.Count) violacion(es) bloqueante(s):" -ForegroundColor Red
foreach ($b in ($blocking | Sort-Object File, Line)) {
    Write-Host ("  [$($b.Pattern)] $($b.File):$($b.Line)") -ForegroundColor Red
    Write-Host ("      $($b.Content)") -ForegroundColor DarkGray
    Write-Host ("      -> $($b.Action)") -ForegroundColor DarkGray
}
Write-Host ""
exit 1
