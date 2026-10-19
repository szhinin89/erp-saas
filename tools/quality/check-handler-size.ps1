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
    [ValidateSet(150)][int]$MaxHandleLines = 150,
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
    param([string[]]$Lines, [switch]$RequireComplete)
    # Recognition only: mask comments/literals while preserving offsets and newlines.
    # Body sizes below still use the original physical lines and brace counter.
    $text = $Lines -join "`n"
    $literalPattern = @'
//[^\r\n]*|/\*[\s\S]*?\*/|"{3,}[\s\S]*?"{3,}|(?:\$?@|@\$)"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'
'@
    $masked = [regex]::Replace($text, $literalPattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) [regex]::Replace($m.Value, '[^\r\n]', ' ') })
    $handleName = '\bHandle\s*(?:<[^(){};]+>\s*)?\('
    $declaration = '(?m)^[ \t]*(?:(?:public|protected|internal|private|static|virtual|override|sealed|new|async|unsafe|extern|partial|readonly)\s+)+[\w<>,\[\].?: \t\r\n]+?\s+Handle\s*(?:<[^(){};]+>\s*)?\('
    $methods = @([regex]::Matches($masked, $declaration))
    if ($RequireComplete) {
        # Calls or unsupported signatures are ambiguous: never silently grant a waiver.
        $candidates = @([regex]::Matches($masked, $handleName) | Where-Object {
            # Unambiguous expression forwarding, including the existing HandleAsync wrapper.
            $prefix = $masked.Substring(0, $_.Index)
            $prefix -notmatch '(?:=>|\breturn|\bawait)\s*$'
        })
        if ($candidates.Count -ne $methods.Count) { throw 'Handle ambiguo: firma o invocacion no reconocida con seguridad.' }
        if ($methods.Count -ne 1) { throw 'Excepcion exige exactamente un Handle detectable.' }
    }
    $results = @()
    foreach ($method in $methods) {
        $i = ([regex]::Matches($masked.Substring(0, $method.Index), "`n")).Count
        $brace = 0
        $started = $false
        $count = 0
        $completed = $false
        for ($j = $i; $j -lt $Lines.Count; $j++) {
            foreach ($ch in $Lines[$j].ToCharArray()) {
                if ($ch -eq '{') { $brace++; $started = $true }
                elseif ($ch -eq '}') { $brace-- }
            }
            if ($started) { $count++ }
            if ($started -and $brace -eq 0) {
                $results += $count
                $completed = $true
                break
            }
        }
        if ($RequireComplete -and -not $completed) { throw 'Handle no contable: cuerpo incompleto.' }
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

function Assert-ExactFile {
    param([string]$Relative)
    if ($Relative -cnotmatch '^[A-Za-z0-9_-]+(?:[./][A-Za-z0-9_-]+)*$' -or $Relative.Contains('..')) {
        throw "Ruta invalida: $Relative"
    }
    $current = $root
    foreach ($part in $Relative.Split('/')) {
        $entry = @(Get-ChildItem -LiteralPath $current -Force | Where-Object { $_.Name -ceq $part })
        if ($entry.Count -ne 1 -or ($entry[0].Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Ruta inexistente, caso incorrecto o enlace: $Relative" }
        $current = $entry[0].FullName
    }
    if (-not (Test-Path -LiteralPath $current -PathType Leaf)) { throw "No es archivo: $Relative" }
}

$rawJson = [string](Get-Content -Raw -LiteralPath $GrandfatherPath)
$json = $rawJson | ConvertFrom-Json
# pwsh 7 desenvuelve una raiz [ {...} ] de un elemento y [pscustomobject] acepta cualquier PSObject:
# se exige texto que abra con '{' y el tipo exacto, igual en Windows PowerShell 5.1 y en pwsh 7.
if (-not $rawJson.TrimStart().StartsWith('{') -or $json -isnot [System.Management.Automation.PSCustomObject]) { throw 'La raiz de architecture-grandfather.json debe ser un objeto JSON.' }
if ($json.handlerHandleMaxLines150 -isnot [Array]) { throw 'handlerHandleMaxLines150 debe ser una lista dentro de un objeto.' }
$grandfather = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($entry in $json.handlerHandleMaxLines150) {
    if ($null -eq $entry -or $entry -isnot [pscustomobject]) { throw 'Excepcion C-03 debe ser objeto, no ruta legacy.' }
    $fields = @($entry.PSObject.Properties.Name | Sort-Object)
    if (($fields -join ',') -cne 'adr,baselineCommit,file,maxHandleLines') { throw 'Metadatos C-03 incompletos o desconocidos.' }
    if ($entry.file -isnot [string] -or $entry.file -cnotmatch '^backend/src/ERP\.Application/(?:[^/]+/)*[^/]*Handler[^/]*\.cs$') { throw 'Ruta C-03 fuera de alcance.' }
    Assert-ExactFile $entry.file
    if ($grandfather.ContainsKey($entry.file)) { throw "Excepcion duplicada: $($entry.file)" }
    if (($entry.maxHandleLines -isnot [int] -and $entry.maxHandleLines -isnot [long]) -or $entry.maxHandleLines -le 150 -or $entry.maxHandleLines -gt [int]::MaxValue) { throw 'Techo C-03 debe ser entero mayor que 150.' }
    if ($entry.baselineCommit -isnot [string] -or $entry.baselineCommit -cnotmatch '^[0-9a-f]{40}$') { throw 'Commit base invalido.' }
    $commitCheck = git -C $root cat-file -t $entry.baselineCommit 2>&1
    if ($LASTEXITCODE -ne 0 -or "$commitCheck" -ne 'commit') { throw 'Commit base inexistente o no es commit.' }
    if ($entry.adr -isnot [string] -or $entry.adr -cnotmatch '^docs/decisions/ADR-[0-9]+-[a-z0-9-]+\.md$') { throw 'Referencia ADR invalida.' }
    Assert-ExactFile $entry.adr
    $grandfather.Add($entry.file, $entry)
}

$violations = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $applicationRoot -Recurse -Filter '*Handler*.cs' -File | ForEach-Object {
    $relative = Normalize-RelativePath -Path $_.FullName -Root $root
    $lines = Get-Content -Path $_.FullName
    $handleCounts = @(Get-AllHandleMethodLineCounts -Lines $lines -RequireComplete:($grandfather.ContainsKey($relative)))
    $maxHandle = ($handleCounts | Measure-Object -Maximum).Maximum
    if ($grandfather.ContainsKey($relative)) {
        $entry = $grandfather[$relative]
        if ($handleCounts.Count -ne 1) {
            $violations.Add("$relative - excepcion exige exactamente un Handle contable")
        } elseif ($maxHandle -le 150) {
            $violations.Add("$relative - excepcion obsoleta: Handle=$maxHandle; retirar entrada")
        } elseif ($maxHandle -gt $entry.maxHandleLines) {
            $violations.Add("$relative - Handle=$maxHandle exceeds ceiling=$($entry.maxHandleLines); ADR=$($entry.adr)")
        } else {
            Write-Host "WARN (grandfather): $relative Handle=$maxHandle ceiling=$($entry.maxHandleLines) ADR=$($entry.adr)" -ForegroundColor Yellow
        }
        return
    }
    if ($null -eq $maxHandle -or $maxHandle -le $MaxHandleLines) { return }

    $violations.Add("$relative - Handle=$maxHandle lines (max $MaxHandleLines)")
}

if ($violations.Count -gt 0) {
    Write-Host "Handler size guardrail FAILED:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Handler size guardrail OK (max Handle lines: $MaxHandleLines)." -ForegroundColor Green
