<#
.SYNOPSIS
  Auditoria de solo lectura del Design System (ERP-DS-GUARD-SCRIPTS-01).

.DESCRIPTION
  Escanea frontend/src/{modules,components,styles} (o un subconjunto via
  -Scope/-ModuleName) buscando los patrones prohibidos/auditables definidos
  en DS_RULES.md: font-family local, italic en datos normales, style={{,
  colores hardcodeados, y tipografia local (font-size/font-weight/
  line-height/letter-spacing/text-transform). No modifica ningun archivo.

  Genera un reporte Markdown (por defecto docs/design-system/DS_AUDIT_REPORT.md)
  con fecha, scope, conteos por patron/clasificacion y la tabla completa de
  hallazgos (archivo, linea, patron, contenido, clasificacion, accion sugerida).

.PARAMETER Scope
  modules (default) | components | styles | all

.PARAMETER ModuleName
  Opcional. Restringe el scope "modules" a frontend/src/modules/<ModuleName>.

.PARAMETER Output
  Ruta del reporte Markdown a generar. Default: docs/design-system/DS_AUDIT_REPORT.md

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/ds/ds-audit.ps1 -Scope modules

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/ds/ds-audit.ps1 -Scope modules -ModuleName sales
#>

param(
    [ValidateSet("modules", "components", "styles", "all")]
    [string]$Scope = "modules",
    [string]$ModuleName,
    [string]$Output
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "_ds-lib.ps1")

$root = Get-DsRepoRoot
if (-not $Output) {
    $Output = Join-Path $root "docs/design-system/DS_AUDIT_REPORT.md"
}

Write-Host "ds-audit: escaneando scope='$Scope'$(if ($ModuleName) { " moduleName='$ModuleName'" })..." -ForegroundColor Cyan
$findings = Get-DsFindings -Scope $Scope -ModuleName $ModuleName

$total = $findings.Count
$byPattern = $findings | Group-Object Pattern | Sort-Object Count -Descending
$byClass = $findings | Group-Object Classification | Sort-Object Count -Descending
$notOk = @($findings | Where-Object { $_.Classification -eq "NOT_OK_VISUAL_LOCAL" })
$needsDecision = @($findings | Where-Object { $_.Classification -eq "NEEDS_DECISION" })

# ── Consola ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Total hallazgos: $total" -ForegroundColor White
foreach ($g in $byClass) {
    $color = switch ($g.Name) {
        "NOT_OK_VISUAL_LOCAL" { "Red" }
        "NEEDS_DECISION" { "Yellow" }
        default { "Green" }
    }
    Write-Host ("  {0,-22} {1}" -f $g.Name, $g.Count) -ForegroundColor $color
}
Write-Host ""
Write-Host "NOT_OK_VISUAL_LOCAL: $($notOk.Count)  |  NEEDS_DECISION: $($needsDecision.Count)" -ForegroundColor White

# ── Reporte Markdown ───────────────────────────────────────────────────
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# DS Audit Report")
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("- Scope: ``$Scope``$(if ($ModuleName) { " (moduleName: ``$ModuleName``)" })")
[void]$sb.AppendLine("- Generado por: ``scripts/ds/ds-audit.ps1``")
[void]$sb.AppendLine("- Total hallazgos: **$total**")
[void]$sb.AppendLine()
[void]$sb.AppendLine("> Auditoria por lineas (regex), no un parser real de CSS/TSX. `` NEEDS_DECISION `` siempre requiere revision humana - ver `` docs/design-system/DS_RULES.md ``.")
[void]$sb.AppendLine()

[void]$sb.AppendLine("## Conteo por patron")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Patron | Hallazgos |")
[void]$sb.AppendLine("|---|---|")
foreach ($g in $byPattern) {
    [void]$sb.AppendLine("| ``$($g.Name)`` | $($g.Count) |")
}
[void]$sb.AppendLine()

[void]$sb.AppendLine("## Conteo por clasificacion")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Clasificacion | Hallazgos |")
[void]$sb.AppendLine("|---|---|")
foreach ($g in $byClass) {
    [void]$sb.AppendLine("| ``$($g.Name)`` | $($g.Count) |")
}
[void]$sb.AppendLine()

if ($total -gt 0) {
    [void]$sb.AppendLine("## Hallazgos")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |")
    [void]$sb.AppendLine("|---|---|---|---|---|---|")
    foreach ($f in ($findings | Sort-Object File, Line)) {
        $content = $f.Content -replace "\|", "\|"
        if ($content.Length -gt 100) { $content = $content.Substring(0, 100) + "..." }
        [void]$sb.AppendLine("| ``$($f.File)`` | $($f.Line) | ``$($f.Pattern)`` | ``$content`` | $($f.Classification) | $($f.Action) |")
    }
} else {
    [void]$sb.AppendLine("## Hallazgos")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("Sin hallazgos en este scope.")
}
[void]$sb.AppendLine()

$outDir = Split-Path $Output -Parent
if ($outDir -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
Set-Content -LiteralPath $Output -Value $sb.ToString() -Encoding UTF8

Write-Host ""
Write-Host "Reporte escrito en: $Output" -ForegroundColor Cyan
