#Requires -Version 5.1
<#
.SYNOPSIS
  Guardrails de arquitectura (backend + frontend). Ver docs/ARCHITECTURE-RULES.md § AUTOMATED GUARDRAILS.
.PARAMETER DiffBase
  Ref git base (p. ej. origin/main) para validar solo archivos nuevos/modificados en límites de líneas.
.PARAMETER SkipFrontendChunk
.PARAMETER FrontendChunkOnly
  Solo verifica tamaño del chunk index (post vite build).
#>
param(
    [string]$DiffBase = '',
    [switch]$SkipFrontendChunk,
    [switch]$FrontendChunkOnly,
    [string]$GrandfatherPath = (Join-Path $PSScriptRoot 'architecture-grandfather.json'),
    [string]$DistAssetsPath = ''
)

$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
    param([string]$Start)
    $root = Split-Path -Parent (Split-Path -Parent $Start)
    if (-not (Test-Path (Join-Path $root 'backend'))) {
        $root = Split-Path -Parent $Start
    }
    return $root
}

function Normalize-RelativePath {
    param([string]$Path, [string]$Root)
    return ($Path.Replace($Root, '').TrimStart('\', '/')).Replace('\', '/')
}

function Get-ChangedRelativePaths {
    param([string]$Root, [string]$BaseRef)
    if ([string]::IsNullOrWhiteSpace($BaseRef)) { return @() }
    Push-Location $Root
    try {
        git rev-parse --verify "${BaseRef}^{commit}" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WARN: DiffBase '$BaseRef' no resuelve; usando escaneo completo." -ForegroundColor Yellow
            return @()
        }
        $raw = git diff --name-only --diff-filter=ACMR ('{0}...HEAD' -f $BaseRef) 2>$null
        if ($LASTEXITCODE -ne 0) {
            $raw = git diff --name-only --diff-filter=ACMR "$BaseRef" HEAD 2>$null
        }
        return @($raw | Where-Object { $_ } | ForEach-Object { $_.Replace('\', '/') })
    }
    finally {
        Pop-Location
    }
}

function Add-Violation {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Message
    )
    $List.Add($Message) | Out-Null
}

function Test-FrontendIndexChunkSize {
    param(
        [string]$Root,
        [int]$IndexMaxKb,
        [string]$DistAssetsPath,
        [System.Collections.Generic.List[string]]$Violations
    )
    $distDir = if ($DistAssetsPath) { $DistAssetsPath } else { Join-Path $Root 'frontend/dist/assets' }
    if (-not (Test-Path $distDir)) {
        Write-Error "Missing dist at $distDir - run npm run build first."
    }
    $indexChunks = Get-ChildItem -Path $distDir -Filter 'index-*.js' -File -ErrorAction SilentlyContinue
    if (-not $indexChunks -or $indexChunks.Count -eq 0) {
        Write-Error "index-*.js not found in $distDir"
    }
    foreach ($chunk in $indexChunks) {
        $sizeKb = [math]::Round($chunk.Length / 1024, 1)
        if ($sizeKb -gt $IndexMaxKb) {
            Add-Violation $Violations "P-01: chunk $($chunk.Name) = ${sizeKb}KB (max ${IndexMaxKb}KB). Use lazy routes."
        }
        else {
            Write-Host "Index chunk OK: $($chunk.Name) = ${sizeKb}KB (max ${IndexMaxKb}KB)" -ForegroundColor Green
        }
    }
}

$Root = Resolve-RepoRoot -Start $PSScriptRoot
$script:Root = $Root
$violations = New-Object System.Collections.Generic.List[string]

if (-not (Test-Path $GrandfatherPath)) {
    Write-Error "Falta $GrandfatherPath"
}
$grandfather = Get-Content -Raw -Path $GrandfatherPath | ConvertFrom-Json
$indexMaxKb = [int]$grandfather.frontendIndexChunkMaxKb

if ($FrontendChunkOnly) {
    Test-FrontendIndexChunkSize -Root $Root -IndexMaxKb $indexMaxKb -DistAssetsPath $DistAssetsPath -Violations $violations
    if ($violations.Count -gt 0) { exit 1 }
    Write-Host 'Frontend chunk guardrail OK.' -ForegroundColor Green
    exit 0
}

$gfTsx500 = @($grandfather.tsxMaxLines500 | ForEach-Object { $_.Replace('\', '/') })
$gfPages15 = @($grandfather.tsxPageWrapperMaxLines15 | ForEach-Object { $_.Replace('\', '/') })
$gfServiceImports = @($grandfather.modulesLegacyRootServiceImports | ForEach-Object { $_.Replace('\', '/') })

$changed = Get-ChangedRelativePaths -Root $Root -BaseRef $DiffBase
$changedMode = $changed.Count -gt 0
if ($changedMode) {
    Write-Host "Modo diff: $($changed.Count) archivos vs '$DiffBase'" -ForegroundColor Cyan
}

# --- Backend: ErpDbContext en Controllers (B-05, PR-1) ---
$controllersRoot = Join-Path $Root 'backend/src/ERP.API/Controllers'
if (Test-Path $controllersRoot) {
    Get-ChildItem -Path $controllersRoot -Recurse -Filter '*.cs' -File | ForEach-Object {
        $hits = Select-String -Path $_.FullName -Pattern 'ErpDbContext' -SimpleMatch
        if ($hits) {
            Add-Violation $violations "PR-1: ErpDbContext en controller $(Normalize-RelativePath -Path $_.FullName -Root $Root)"
        }
    }
}

# --- Handler size (delegado) ---
$handlerScript = Join-Path $PSScriptRoot 'check-handler-size.ps1'
& $handlerScript -GrandfatherPath $GrandfatherPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# --- Frontend: patrones prohibidos ---
$frontendSrc = Join-Path $Root 'frontend/src'
$excludeFrontend = @('*\node_modules\*', '*\dist\*', '*\.lscache', '*\playwright-report\*')

# fetch() directo en modules (PR frontend)
$modulesOnly = Join-Path $frontendSrc 'modules'
if (Test-Path $modulesOnly) {
    Get-ChildItem -Path $modulesOnly -Recurse -File -Include '*.ts', '*.tsx' | ForEach-Object {
        $rel = Normalize-RelativePath -Path $_.FullName -Root $Root
        if ($changedMode -and ($changed -notcontains $rel)) { return }
        Select-String -Path $_.FullName -Pattern '[^a-zA-Z.]fetch\s*\(' | ForEach-Object {
            Add-Violation $violations ('PR frontend: fetch() in {0} (use modules/lib/api.ts)' -f $rel)
        }
    }
}

# window.confirm / tenantId en URL
foreach ($pattern in @('window\.(confirm|prompt)\s*\(', 'tenantId=\$\{')) {
    Get-ChildItem -Path $frontendSrc -Recurse -File -Include '*.ts', '*.tsx' | ForEach-Object {
        $rel = Normalize-RelativePath -Path $_.FullName -Root $Root
        if ($changedMode -and ($changed -notcontains $rel)) { return }
        if (Select-String -Path $_.FullName -Pattern $pattern -Quiet) {
            Add-Violation $violations ('PR frontend: banned pattern {0} in {1}' -f $pattern, $rel)
        }
    }
}

# imports directos a services/ desde modules/ (PR-5 parcial)
$modulesRoot = Join-Path $frontendSrc 'modules'
if (Test-Path $modulesRoot) {
    Get-ChildItem -Path $modulesRoot -Recurse -File -Include '*.ts', '*.tsx' | ForEach-Object {
        $rel = Normalize-RelativePath -Path $_.FullName -Root $Root
        if ($changedMode -and ($changed -notcontains $rel)) { return }
        Select-String -Path $_.FullName -Pattern "from\s+['""][^'""]*\/services\/" | ForEach-Object {
            if ($gfServiceImports -contains $rel) {
                Write-Host "WARN (grandfather): import services/ en $rel" -ForegroundColor Yellow
                return
            }
            Add-Violation $violations ('PR-5: services/ import in module {0}: {1}' -f $rel, $_.Line.Trim())
        }
    }
}

# --- TSX > 500 líneas (PR-7) ---
$frontendTsx = Join-Path $frontendSrc ''
Get-ChildItem -Path $frontendSrc -Recurse -File -Filter '*.tsx' | ForEach-Object {
    $rel = Normalize-RelativePath -Path $_.FullName -Root $Root
    if ($changedMode -and ($changed -notcontains $rel)) { return }
    $lineCount = (Get-Content -Path $_.FullName | Measure-Object -Line).Lines
    if ($lineCount -le 500) { return }
    if ($gfTsx500 -contains $rel) {
        Write-Host "WARN (grandfather): $rel has $lineCount lines" -ForegroundColor Yellow
        return
    }
    Add-Violation $violations "PR-7: $rel has $lineCount lines (max 500)"
}

# --- pages/*.tsx > 15 líneas (PR-6) ---
$pagesRoot = Join-Path $frontendSrc 'pages'
if (Test-Path $pagesRoot) {
    Get-ChildItem -Path $pagesRoot -Filter '*.tsx' -File | ForEach-Object {
        $rel = Normalize-RelativePath -Path $_.FullName -Root $Root
        if ($changedMode -and ($changed -notcontains $rel)) { return }
        $lineCount = (Get-Content -Path $_.FullName | Measure-Object -Line).Lines
        if ($lineCount -le 15) { return }
        if ($gfPages15 -contains $rel) {
            Write-Host "WARN (grandfather): page wrapper $rel ($lineCount lines)" -ForegroundColor Yellow
            return
        }
        Add-Violation $violations "PR-6: page wrapper $rel has $lineCount lines (max 15; move to modules/*/pages/)"
    }
}

# --- Nuevos services/ raíz (> 20 líneas, PR-5) ---
$servicesRoot = Join-Path $frontendSrc 'services'
if (Test-Path $servicesRoot) {
    Push-Location $Root
    try {
        $newServiceFiles = @()
        if ($changedMode) {
            $newServiceFiles = git diff --name-only --diff-filter=A ('{0}...HEAD' -f $DiffBase) 2>$null
            if ($LASTEXITCODE -ne 0) { $newServiceFiles = git diff --name-only --diff-filter=A "$DiffBase" HEAD }
            $newServiceFiles = @($newServiceFiles | Where-Object { $_ -like 'frontend/src/services/*.ts' })
        }
        else {
            $newServiceFiles = @()
        }
        foreach ($svcRel in $newServiceFiles) {
            $svcRel = $svcRel.Replace('\', '/')
            $full = Join-Path $Root $svcRel
            if (-not (Test-Path $full)) { continue }
            $lineCount = (Get-Content -Path $full | Measure-Object -Line).Lines
            if ($lineCount -gt 20) {
                Add-Violation $violations "PR-5: new root service $svcRel ($lineCount lines). Use modules/{domain}/api/."
            }
        }
    }
    finally {
        Pop-Location
    }
}

# --- Vite index chunk size (P-01) ---
if (-not $SkipFrontendChunk) {
    Test-FrontendIndexChunkSize -Root $Root -IndexMaxKb $indexMaxKb -DistAssetsPath $DistAssetsPath -Violations $violations
}

if ($violations.Count -gt 0) {
    Write-Host "`nArchitecture guardrails FAILED ($($violations.Count)):" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'Architecture guardrails OK.' -ForegroundColor Green
