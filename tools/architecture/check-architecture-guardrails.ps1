#Requires -Version 5.1
<#
.SYNOPSIS
  Guardrails de arquitectura (backend + frontend). Ver docs/architecture/pr-rules-catalog.md § AUTOMATED GUARDRAILS; entrada docs/ARCHITECTURE-RULES.md.
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
    $dir = $Start
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'backend')) -and (Test-Path (Join-Path $dir 'frontend'))) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $dir) { break }
        $dir = $parent
    }
    throw "No se encontró la raíz del monorepo ERP SaaS desde: $Start"
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
$grandfatherRaw = [string](Get-Content -Raw -Path $GrandfatherPath)
$grandfather = $grandfatherRaw | ConvertFrom-Json
# Misma guarda que check-handler-size.ps1: pwsh 7 desenvuelve una raiz [ {...} ] de un elemento.
if (-not $grandfatherRaw.TrimStart().StartsWith('{') -or $grandfather -isnot [System.Management.Automation.PSCustomObject]) {
    throw "La raiz de $GrandfatherPath debe ser un objeto JSON."
}
$indexMaxKb = [int]$grandfather.frontendIndexChunkMaxKb

if ($FrontendChunkOnly) {
    Test-FrontendIndexChunkSize -Root $Root -IndexMaxKb $indexMaxKb -DistAssetsPath $DistAssetsPath -Violations $violations
    if ($violations.Count -gt 0) { exit 1 }
    Write-Host 'Frontend chunk guardrail OK.' -ForegroundColor Green
    exit 0
}

function Assert-ExactRepoFile {
    param([string]$Relative)
    if ($Relative -cnotmatch '^[A-Za-z0-9_-]+(?:[./][A-Za-z0-9_-]+)*$' -or $Relative.Contains('..')) {
        throw "Ruta invalida: $Relative"
    }
    $current = $Root
    foreach ($part in $Relative.Split('/')) {
        $entry = @(Get-ChildItem -LiteralPath $current -Force | Where-Object { $_.Name -ceq $part })
        if ($entry.Count -ne 1 -or ($entry[0].Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Ruta inexistente, caso incorrecto o enlace: $Relative" }
        $current = $entry[0].FullName
    }
    if (-not (Test-Path -LiteralPath $current -PathType Leaf)) { throw "No es archivo: $Relative" }
}

# PR-7 (ADR-047): excepciones con techo individual; se valida toda la configuracion antes de contar, en cualquier modo.
if ($grandfather.tsxMaxLines500 -isnot [Array]) { throw 'tsxMaxLines500 debe ser una lista dentro de un objeto.' }
$gfTsx500 = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($entry in $grandfather.tsxMaxLines500) {
    if ($null -eq $entry -or $entry -isnot [pscustomobject]) { throw 'Excepcion PR-7 debe ser objeto, no ruta legacy.' }
    $fields = @($entry.PSObject.Properties.Name | Sort-Object)
    if (($fields -join ',') -cne 'adr,baselineCommit,file,maxLines') { throw 'Metadatos PR-7 incompletos o desconocidos.' }
    if ($entry.file -isnot [string] -or $entry.file -cnotmatch '^frontend/src/(?:[^/]+/)*[^/]+\.tsx$') { throw 'Ruta PR-7 fuera de alcance.' }
    Assert-ExactRepoFile $entry.file
    if ($gfTsx500.ContainsKey($entry.file)) { throw "Excepcion PR-7 duplicada: $($entry.file)" }
    if (($entry.maxLines -isnot [int] -and $entry.maxLines -isnot [long]) -or $entry.maxLines -le 500 -or $entry.maxLines -gt [int]::MaxValue) { throw 'Techo PR-7 debe ser entero mayor que 500.' }
    if ($entry.baselineCommit -isnot [string] -or $entry.baselineCommit -cnotmatch '^[0-9a-f]{40}$') { throw 'Commit base PR-7 invalido.' }
    $commitCheck = git -C $Root cat-file -t $entry.baselineCommit 2>&1
    if ($LASTEXITCODE -ne 0 -or "$commitCheck" -ne 'commit') { throw 'Commit base PR-7 inexistente o no es commit.' }
    if ($entry.adr -isnot [string] -or $entry.adr -cnotmatch '^docs/decisions/ADR-[0-9]+-[a-z0-9-]+\.md$') { throw 'Referencia ADR PR-7 invalida.' }
    Assert-ExactRepoFile $entry.adr
    $gfTsx500.Add($entry.file, $entry)
}
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
$handlerScript = Join-Path (Split-Path $PSScriptRoot -Parent) 'quality/check-handler-size.ps1'
# Sin un comando nativo previo $LASTEXITCODE es $null y "exit $null" saldria 0 omitiendo el resto (fail-open).
$global:LASTEXITCODE = 0
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
    if ($gfTsx500.ContainsKey($rel)) {
        $entry = $gfTsx500[$rel]
        if ($lineCount -le 500) {
            Add-Violation $violations "PR-7: $rel excepcion obsoleta: $lineCount lines (max 500); retirar entrada; ADR=$($entry.adr)"
        }
        elseif ($lineCount -gt $entry.maxLines) {
            Add-Violation $violations "PR-7: $rel has $lineCount lines, exceeds ceiling=$($entry.maxLines); ADR=$($entry.adr)"
        }
        else {
            Write-Host "WARN (grandfather): $rel lines=$lineCount ceiling=$($entry.maxLines) ADR=$($entry.adr)" -ForegroundColor Yellow
        }
        return
    }
    if ($lineCount -le 500) { return }
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
