param(
    [string]$RepoRoot = ".",
    [string]$AllowlistPath = "scripts/stack-allowlist.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Add-Violation {
    param(
        [System.Collections.Generic.List[object]]$List,
        [string]$File,
        [int]$Line,
        [string]$Detected,
        [string]$Reason,
        [string]$Suggestion
    )

    $List.Add([PSCustomObject]@{
        File       = $File
        Line       = $Line
        Detected   = $Detected
        Reason     = $Reason
        Suggestion = $Suggestion
    })
}

$root = (Resolve-Path $RepoRoot).Path
$allowlistFullPath = Join-Path $root $AllowlistPath
$allowlist = Get-Content -Raw $allowlistFullPath | ConvertFrom-Json

$violations = New-Object 'System.Collections.Generic.List[object]'

# 1) NuGet direct references in all csproj
$csprojFiles = Get-ChildItem -Path (Join-Path $root "backend/src") -Recurse -Filter "*.csproj"
foreach ($csproj in $csprojFiles) {
    [xml]$xml = Get-Content -Raw $csproj.FullName
    foreach ($itemGroup in @($xml.Project.ItemGroup)) {
        if ($null -eq $itemGroup) { continue }
        if ($itemGroup.PSObject.Properties.Name -notcontains "PackageReference") { continue }
        foreach ($r in @($itemGroup.PackageReference)) {
            if ($null -eq $r -or $null -eq $r.Include) { continue }
            $pkg = [string]$r.Include
            if ($allowlist.nugetAllowed -notcontains $pkg) {
                $hit = Select-String -Path $csproj.FullName -Pattern ("PackageReference Include=""{0}""" -f [regex]::Escape($pkg)) | Select-Object -First 1
                $lineNumber = if ($hit) { $hit.LineNumber } else { 1 }
                Add-Violation -List $violations -File $csproj.FullName.Replace($root + [IO.Path]::DirectorySeparatorChar, "") -Line $lineNumber -Detected $pkg -Reason "Paquete NuGet directo fuera de allowlist." -Suggestion "Agregarlo al inventario oficial o retirarlo del proyecto."
            }
        }
    }
}

# 2) npm dependencies/devDependencies in frontend/package.json
$packageJson = Join-Path $root "frontend/package.json"
$pkg = Get-Content -Raw $packageJson | ConvertFrom-Json
$npmDeps = @{}
if ($pkg.dependencies) {
    $pkg.dependencies.PSObject.Properties | ForEach-Object { $npmDeps[$_.Name] = $_.Value }
}
if ($pkg.devDependencies) {
    $pkg.devDependencies.PSObject.Properties | ForEach-Object { $npmDeps[$_.Name] = $_.Value }
}
foreach ($name in $npmDeps.Keys) {
    if ($allowlist.npmAllowed -notcontains $name) {
        $hit = Select-String -Path $packageJson -Pattern ('"{0}"\s*:' -f [regex]::Escape($name)) | Select-Object -First 1
        $lineNumber = if ($hit) { $hit.LineNumber } else { 1 }
        Add-Violation -List $violations -File "frontend/package.json" -Line $lineNumber -Detected $name -Reason "Dependencia npm fuera de allowlist." -Suggestion "Agregarla al inventario oficial o eliminarla."
    }
}

# 3) docker-compose services (solo dentro del bloque services:)
$composePath = Join-Path $root "docker-compose.yml"
$composeLines = Get-Content $composePath
$inServices = $false
for ($i = 0; $i -lt $composeLines.Count; $i++) {
    $line = $composeLines[$i]

    if ($line -match '^\s*services:\s*$') {
        $inServices = $true
        continue
    }

    if ($inServices -and $line -match '^\S') {
        $inServices = $false
    }

    if (-not $inServices) { continue }
    if ($line -notmatch '^\s{2}([a-zA-Z0-9_-]+):\s*$') { continue }

    $svc = $matches[1]
    if ($allowlist.dockerComposeServicesAllowed -notcontains $svc) {
        Add-Violation -List $violations -File "docker-compose.yml" -Line ($i + 1) -Detected $svc -Reason "Servicio docker-compose fuera de allowlist." -Suggestion "Retirar el servicio o autorizarlo explícitamente."
    }
}

# 4) GitHub Actions usage in workflows
$workflowFiles = Get-ChildItem -Path (Join-Path $root ".github/workflows") -Filter "*.yml"
foreach ($wf in $workflowFiles) {
    # Soporta ambos formatos YAML:
    # - "- uses: actions/checkout@v6"
    # - "- name: X" + "  uses: actions/checkout@v6"
    $usesHits = Select-String -Path $wf.FullName -Pattern '^\s*uses:\s*([^\s]+)\s*$|^\s*-\s*uses:\s*([^\s]+)\s*$'
    foreach ($u in $usesHits) {
        $actionRef = $u.Matches[0].Groups[1].Value
        if ([string]::IsNullOrWhiteSpace($actionRef)) {
            $actionRef = $u.Matches[0].Groups[2].Value
        }
        $isAllowedExact = $allowlist.githubActionsAllowed -contains $actionRef
        $isAllowedPrefix = $false
        foreach ($p in $allowlist.githubActionsAllowedPrefixes) {
            if ($actionRef.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)) {
                $isAllowedPrefix = $true
                break
            }
        }

        if (-not ($isAllowedExact -or $isAllowedPrefix)) {
            $rel = $wf.FullName.Replace($root + [IO.Path]::DirectorySeparatorChar, "")
            Add-Violation -List $violations -File $rel -Line $u.LineNumber -Detected $actionRef -Reason "GitHub Action fuera de allowlist." -Suggestion "Reemplazar por acción permitida o autorizarla en la allowlist."
        }
    }
}

# 5) Disallowed patterns in source/docs/config
$scanTargets = @(
    (Join-Path $root "backend"),
    (Join-Path $root "frontend"),
    (Join-Path $root ".github/workflows")
)
foreach ($pattern in $allowlist.disallowedPatterns) {
    foreach ($target in $scanTargets) {
        if (-not (Test-Path $target)) { continue }
        $files = Get-ChildItem -Path $target -Recurse -File -Include *.cs,*.csproj,*.json,*.md,*.yml,*.yaml,*.ps1,*.ts,*.tsx,*.js,*.jsx,*.sql,*.config -ErrorAction SilentlyContinue |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj|node_modules|\.git|dist|build)[\\/]'
            }
        if ($files.Count -eq 0) { continue }
        $hits = $files | Select-String -Pattern $pattern -ErrorAction SilentlyContinue
        foreach ($h in $hits) {
            $rel = $h.Path.Replace($root + [IO.Path]::DirectorySeparatorChar, "")
            Add-Violation -List $violations -File $rel -Line $h.LineNumber -Detected $pattern -Reason "Patrón no permitido detectado." -Suggestion "Eliminar uso o agregar excepción explícita documentada."
        }
    }
}

if ($violations.Count -eq 0) {
    Write-Host "Stack audit passed. No violations found." -ForegroundColor Green
    exit 0
}

Write-Host "Stack audit failed. Violations: $($violations.Count)" -ForegroundColor Red
$violations |
    Sort-Object File, Line |
    Format-Table -AutoSize File, Line, Detected, Reason, Suggestion

exit 1
