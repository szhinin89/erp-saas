<#
.SYNOPSIS
  Valida que frontend/src/pages/**/*.tsx sean wrappers puros (F-01 / PR-6).

.RULES
  1. Cada .tsx en pages/ debe tener <= 15 líneas.
  2. No puede contener hooks React (useState, useEffect, useCallback, useMemo, useRef, useReducer, useContext).
  3. No puede importar CSS directamente (las implementaciones llevan su CSS al módulo).

.EXIT
  0 = todo OK
  1 = al menos una violación encontrada
#>

param(
  [string]$PagesDir = "frontend/src/pages",
  [switch]$WarnOnly
)

$root    = Join-Path $PSScriptRoot "../.."
$pagesAbs = Join-Path $root $PagesDir
$errors  = [System.Collections.Generic.List[string]]::new()

$hookPattern = 'use(State|Effect|Callback|Memo|Ref|Reducer|Context)\s*[<(]'
$cssPattern  = "import\s+['""]\./"

$files = Get-ChildItem -Path $pagesAbs -Recurse -Filter "*.tsx" -ErrorAction SilentlyContinue

if (-not $files) {
  Write-Host "pages-wrapper-rule: no .tsx files found in $PagesDir — OK" -ForegroundColor Green
  exit 0
}

foreach ($file in $files) {
  $rel     = $file.FullName.Replace($root + "\", "").Replace("\", "/")
  $lines   = Get-Content $file.FullName -ErrorAction SilentlyContinue
  $count   = if ($lines) { $lines.Count } else { 0 }
  $content = $lines -join "`n"

  # Rule 1 — max 15 lines
  if ($count -gt 15) {
    $errors.Add("[$rel] ${count} lines (max 15). Move implementation to modules/{domain}/pages/.")
  }

  # Rule 2 — no React hooks
  if ($content -match $hookPattern) {
    $errors.Add("[$rel] Contains React hook(s). Hooks belong in modules/{domain}/pages/ or hooks/.")
  }

  # Rule 3 — no CSS imports
  if ($content -match $cssPattern) {
    $errors.Add("[$rel] Contains CSS import. CSS belongs in the module implementation, not the wrapper.")
  }
}

if ($errors.Count -eq 0) {
  Write-Host "pages-wrapper-rule: all $($files.Count) pages/ wrappers are clean." -ForegroundColor Green
  exit 0
}

$label = if ($WarnOnly) { "WARNING" } else { "ERROR" }
Write-Host "`npages-wrapper-rule: $($errors.Count) violation(s) found:`n" -ForegroundColor $(if ($WarnOnly) { "Yellow" } else { "Red" })
foreach ($e in $errors) {
  Write-Host "  $label  $e" -ForegroundColor $(if ($WarnOnly) { "Yellow" } else { "Red" })
}
Write-Host ""

if ($WarnOnly) {
  exit 0
} else {
  exit 1
}
