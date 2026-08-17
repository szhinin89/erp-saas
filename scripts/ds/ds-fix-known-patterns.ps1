<#
.SYNOPSIS
  Corrige automaticamente patrones DS 100% seguros (ERP-DS-GUARD-SCRIPTS-01).

.DESCRIPTION
  Solo aplica los 5 fixers narrow/seguros documentados abajo. Cualquier caso
  que no matchee exactamente el patron esperado se deja intacto y se reporta
  como NEEDS_DECISION - este script nunca "adivina".

  Fixers implementados:
    1. Quita la prop booleana `italic` de <ZHDataValue ...> en modules/**/*.tsx.
    2. Quita el token `zh-data-value--italic` de className en modules/**/*.tsx.
    3. Borra reglas de una sola linea "SELECTOR { font-style: italic; }" en
       modules/**/*.css (unica declaracion del bloque - si el bloque tiene
       mas propiedades o esta en varias lineas, NO se toca).
    4. Garantiza `white-space: nowrap;` dentro de `.zh-money-value { ... }`
       en frontend/src/styles/zh-ui.css (invariante global, corre siempre,
       fuera del filtro -Scope, porque protege una regla que TODOS los
       modulos consumen).
    5. Borra reglas de una sola linea "SELECTOR { font-family: var(--font-family-mono); }"
       en modules/**/*.css cuando el nombre del selector matchea un patron de
       codigo tecnico conocido (sku|code|clave|secuencial|auxcode|access) -
       solo borra la declaracion CSS local; NUNCA edita el .tsx. Reporta el
       selector para que el className `zh-code-value` se agregue a mano.

.PARAMETER Scope
  modules (default) | all - components/styles no tienen "fixes conocidos"
  aplicables hoy (son ellos la fuente de verdad), se aceptan por
  compatibilidad con los demas scripts pero no producen cambios.

.PARAMETER ModuleName
  Opcional. Restringe el scope "modules" a frontend/src/modules/<ModuleName>.

.PARAMETER Apply
  Sin esta flag, el script es de solo lectura (dry-run): imprime que
  cambiaria, pero no escribe nada. Con -Apply, escribe los cambios.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/ds/ds-fix-known-patterns.ps1 -Scope modules
  # dry-run - no escribe nada

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/ds/ds-fix-known-patterns.ps1 -Scope modules -Apply
#>

param(
    [ValidateSet("modules", "all")]
    [string]$Scope = "modules",
    [string]$ModuleName,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "_ds-lib.ps1")

$root = Get-DsRepoRoot
$DryRun = -not $Apply

Write-Host "ds-fix-known-patterns: scope='$Scope'$(if ($ModuleName) { " moduleName='$ModuleName'" }) mode=$(if ($DryRun) { 'DRY-RUN (sin -Apply, no se escribe nada)' } else { 'APPLY' })" -ForegroundColor Cyan
Write-Host ""

$changes = [System.Collections.Generic.List[object]]::new()   # { File, Description }
$needsDecisionNotes = [System.Collections.Generic.List[string]]::new()

# ── Fixer 1 + 2: ZHDataValue italic prop / zh-data-value--italic className, en modules/**/*.tsx ──
$tsxPaths = Resolve-DsScopePaths -Scope $Scope -ModuleName $ModuleName | Where-Object { $_ -match "[\\/]modules([\\/]|$)" }
$tsxFiles = if ($tsxPaths) { Get-ChildItem -Path $tsxPaths -Recurse -File -Include "*.tsx" -ErrorAction SilentlyContinue } else { @() }

foreach ($file in $tsxFiles) {
    $lines = @(Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue)
    if (-not $lines) { continue }

    $newLines = [System.Collections.Generic.List[string]]::new()
    $fileChanged = $false
    $insideZHDataValueTag = $false

    foreach ($line in $lines) {
        $out = $line

        # Fixer 1a: <ZHDataValue ... italic ...> en la misma linea del open tag.
        if ($out -match "<ZHDataValue\b" -and $out -match "\sitalic\b(?=\s|>)") {
            $out = $out -replace "\sitalic\b(?=\s|>)", ""
            $fileChanged = $true
            $changes.Add([PSCustomObject]@{ File = $file.FullName; Description = "Quitada prop 'italic' de ZHDataValue (linea inline)" })
        }

        # Fixer 1b: prop 'italic' sola en su propia linea (formato multi-linea/Prettier),
        # dentro de un tag <ZHDataValue ...> abierto sin cerrar todavia.
        if ($out -match "<ZHDataValue\b" -and $out -notmatch ">") { $insideZHDataValueTag = $true }
        if ($insideZHDataValueTag -and $out.Trim() -eq "italic") {
            $fileChanged = $true
            $changes.Add([PSCustomObject]@{ File = $file.FullName; Description = "Quitada prop 'italic' de ZHDataValue (linea propia)" })
            continue  # no agregar esta linea a newLines: se borra completa
        }
        if ($out -match ">") { $insideZHDataValueTag = $false }

        # Fixer 2: className="... zh-data-value--italic ..."
        if ($out -match "zh-data-value--italic") {
            $before = $out
            $out = $out -replace "\s*zh-data-value--italic\s*", " "
            $out = $out -replace 'className="\s+', 'className="'
            $out = $out -replace '\s+"', '"'
            if ($out -ne $before) {
                $fileChanged = $true
                $changes.Add([PSCustomObject]@{ File = $file.FullName; Description = "Quitada clase 'zh-data-value--italic' de className" })
            }
        }

        $newLines.Add($out)
    }

    if ($fileChanged -and -not $DryRun) {
        Set-Content -LiteralPath $file.FullName -Value $newLines -Encoding UTF8
    }
}

# ── Fixer 3: reglas de una sola linea "SELECTOR { font-style: italic; }" en modules/**/*.css ──
$cssPaths = $tsxPaths
$cssFiles = if ($cssPaths) { Get-ChildItem -Path $cssPaths -Recurse -File -Include "*.css" -ErrorAction SilentlyContinue } else { @() }
$singleLineItalicRulePattern = "^\s*[^\{]+\{\s*font-style:\s*italic;\s*\}\s*$"

foreach ($file in $cssFiles) {
    $lines = @(Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue)
    if (-not $lines) { continue }

    $newLines = [System.Collections.Generic.List[string]]::new()
    $fileChanged = $false

    foreach ($line in $lines) {
        if ($line -match $singleLineItalicRulePattern) {
            $fileChanged = $true
            $changes.Add([PSCustomObject]@{ File = $file.FullName; Description = "Borrada regla de una linea 'font-style: italic' unica declaracion: $($line.Trim())" })
            continue
        }
        $newLines.Add($line)
    }

    if ($fileChanged -and -not $DryRun) {
        Set-Content -LiteralPath $file.FullName -Value $newLines -Encoding UTF8
    }
}

# ── Fixer 5: reglas de una sola linea "SELECTOR { font-family: var(--font-family-mono); }" con selector de codigo conocido ──
$codeSelectorPattern = "(?i)sku|code|clave|secuencial|auxcode|access"
$singleLineMonoRulePattern = "^\s*(?<sel>[^\{]+)\{\s*font-family:\s*var\(--font-family-mono\);\s*\}\s*$"

foreach ($file in $cssFiles) {
    $lines = @(Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue)
    if (-not $lines) { continue }

    $newLines = [System.Collections.Generic.List[string]]::new()
    $fileChanged = $false

    foreach ($line in $lines) {
        $m = [regex]::Match($line, $singleLineMonoRulePattern)
        if ($m.Success -and $m.Groups["sel"].Value -match $codeSelectorPattern) {
            $fileChanged = $true
            $sel = $m.Groups["sel"].Value.Trim()
            $changes.Add([PSCustomObject]@{ File = $file.FullName; Description = "Borrada regla local 'font-family-mono' de selector de codigo tecnico '$sel'. AGREGAR MANUALMENTE className='zh-code-value' (o Badge code / ZHDataValue variant=code) en el .tsx que usa '$sel'." })
            continue
        }
        $newLines.Add($line)
    }

    if ($fileChanged -and -not $DryRun) {
        Set-Content -LiteralPath $file.FullName -Value $newLines -Encoding UTF8
    }
}

if ($cssFiles.Count -eq 0 -and $tsxFiles.Count -eq 0) {
    Write-Host "Scope '$Scope'$(if ($ModuleName) { "/$ModuleName" }) no tiene archivos .tsx/.css de modulos que auditar con estos fixers." -ForegroundColor Yellow
}

# ── Fixer 4: invariante global .zh-money-value { white-space: nowrap; } ──
$zhUiPath = Join-Path $root "frontend/src/styles/zh-ui.css"
if (Test-Path $zhUiPath) {
    $zhUiLines = @(Get-Content -LiteralPath $zhUiPath)
    $blockStart = -1
    $blockEnd = -1
    for ($i = 0; $i -lt $zhUiLines.Count; $i++) {
        if ($zhUiLines[$i] -match "^\.zh-money-value\s*\{") { $blockStart = $i; break }
    }
    if ($blockStart -ge 0) {
        for ($i = $blockStart; $i -lt $zhUiLines.Count; $i++) {
            if ($zhUiLines[$i] -match "\}") { $blockEnd = $i; break }
        }
    }
    if ($blockStart -ge 0 -and $blockEnd -ge $blockStart) {
        $blockText = ($zhUiLines[$blockStart..$blockEnd]) -join "`n"
        if ($blockText -match "white-space:\s*nowrap") {
            Write-Host "Fixer 4: .zh-money-value ya tiene white-space: nowrap - OK, sin cambios." -ForegroundColor Green
        } else {
            $changes.Add([PSCustomObject]@{ File = $zhUiPath; Description = "Insertada 'white-space: nowrap;' dentro de .zh-money-value (invariante global de money indivisible)" })
            if (-not $DryRun) {
                $newZhUi = [System.Collections.Generic.List[string]]::new()
                $newZhUi.AddRange($zhUiLines[0..($blockEnd - 1)])
                $newZhUi.Add("  white-space: nowrap;")
                $newZhUi.AddRange($zhUiLines[$blockEnd..($zhUiLines.Count - 1)])
                Set-Content -LiteralPath $zhUiPath -Value $newZhUi -Encoding UTF8
            }
        }
    } else {
        Write-Host "Fixer 4: no se encontro el bloque .zh-money-value en zh-ui.css - revisar manualmente (NEEDS_DECISION)." -ForegroundColor Yellow
    }
}

# ── Resumen ────────────────────────────────────────────────────────────
Write-Host ""
if ($changes.Count -eq 0) {
    Write-Host "ds-fix-known-patterns: sin cambios aplicables en scope '$Scope'$(if ($ModuleName) { "/$ModuleName" })." -ForegroundColor Green
    exit 0
}

$affectedFiles = $changes.File | Sort-Object -Unique
Write-Host "Archivos afectados ($($affectedFiles.Count)):" -ForegroundColor White
foreach ($f in $affectedFiles) {
    $rel = $f.Replace($root, "").TrimStart("\", "/").Replace("\", "/")
    Write-Host "  - $rel" -ForegroundColor White
}
Write-Host ""
Write-Host "Cambios ($($changes.Count)):" -ForegroundColor White
foreach ($c in $changes) {
    $rel = $c.File.Replace($root, "").TrimStart("\", "/").Replace("\", "/")
    Write-Host "  [$rel] $($c.Description)" -ForegroundColor $(if ($DryRun) { "Yellow" } else { "Green" })
}

Write-Host ""
if ($DryRun) {
    Write-Host "DRY-RUN: no se escribio ningun archivo. Volver a correr con -Apply para aplicar estos $($changes.Count) cambio(s)." -ForegroundColor Yellow
} else {
    Write-Host "APLICADO: $($changes.Count) cambio(s) escritos en $($affectedFiles.Count) archivo(s)." -ForegroundColor Green
}
