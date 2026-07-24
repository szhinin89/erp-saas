#Requires -Version 5.1
<#
.SYNOPSIS
  Falla si un método Handle de MediatR supera el límite (regla C-03, PR handler size).
.PARAMETER MaxHandleLines
  Límite de líneas del cuerpo de Handle (default 150).
.PARAMETER GrandfatherPath
  JSON con rutas legacy permitidas (tools/architecture/architecture-grandfather.json).
#>
param(
    [int]$MaxHandleLines = 150,
    [string]$GrandfatherPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'architecture/architecture-grandfather.json')
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

function Get-AllHandleMethodLineCounts {
    param([string[]]$Lines)
    $results = @()
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -notmatch '^\s*(public|protected)\s+(async\s+)?[\w<>,\[\]\.?]+\s+Handle\s*\(') {
            continue
        }
        $brace = 0
        $started = $false
        $count = 0
        for ($j = $i; $j -lt $Lines.Count; $j++) {
            foreach ($ch in $Lines[$j].ToCharArray()) {
                if ($ch -eq '{') { $brace++; $started = $true }
                elseif ($ch -eq '}') { $brace-- }
            }
            if ($started) { $count++ }
            if ($started -and $brace -eq 0) {
                $results += $count
                break
            }
        }
    }
    return $results
}

function Normalize-RelativePath {
    param([string]$Path, [string]$Root)
    return ($Path.Replace($Root, '').TrimStart('\', '/')).Replace('\', '/')
}

$root = Resolve-RepoRoot -Start $PSScriptRoot
$applicationRoot = Join-Path $root 'backend/src/ERP.Application'
if (-not (Test-Path $applicationRoot)) {
    Write-Error "No se encontró $applicationRoot"
}

$grandfather = @()
if (Test-Path $GrandfatherPath) {
    $json = Get-Content -Raw -Path $GrandfatherPath | ConvertFrom-Json
    $grandfather = @($json.handlerHandleMaxLines150 | ForEach-Object { $_.Replace('\', '/') })
}

$violations = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $applicationRoot -Recurse -Filter '*Handler*.cs' -File | ForEach-Object {
    $relative = Normalize-RelativePath -Path $_.FullName -Root $root
    $lines = Get-Content -Path $_.FullName
    $handleCounts = Get-AllHandleMethodLineCounts -Lines $lines
    $maxHandle = ($handleCounts | Measure-Object -Maximum).Maximum
    if ($null -eq $maxHandle -or $maxHandle -le $MaxHandleLines) { return }

    if ($grandfather -contains $relative) {
        Write-Host "WARN (grandfather): $relative Handle=$maxHandle > $MaxHandleLines" -ForegroundColor Yellow
        return
    }

    $violations.Add("$relative - Handle=$maxHandle lines (max $MaxHandleLines)")
}

if ($violations.Count -gt 0) {
    Write-Host "Handler size guardrail FAILED:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Handler size guardrail OK (max Handle lines: $MaxHandleLines)." -ForegroundColor Green
