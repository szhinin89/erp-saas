#Requires -Version 5.1
<#
.SYNOPSIS
  Falla si el repositorio reintroduce referencias al auth legacy (tabla users).
#>
$ErrorActionPreference = 'Stop'
function Resolve-ErpRepoRoot {
  $dir = $PSScriptRoot
  while ($dir) {
    if ((Test-Path (Join-Path $dir 'backend')) -and (Test-Path (Join-Path $dir 'frontend'))) {
      return $dir
    }
    $parent = Split-Path -Parent $dir
    if ([string]::IsNullOrEmpty($parent) -or $parent -eq $dir) { break }
    $dir = $parent
  }
  throw 'No se encontró la raíz del repo ERP SaaS.'
}
$root = Resolve-ErpRepoRoot

$banned = @(
  'ERP\.Domain\.Auth\.Entities\.User',
  'IUserRepository',
  'ToTable\("users"\)',
  '_context\.Users',
  'DbSet<User>'
)

$searchRoots = @(
  (Join-Path $root 'backend\src'),
  (Join-Path $root 'frontend\src')
)

$exclude = @(
  '*\Migrations\*',
  '*\docs\*',
  '*\scripts\*',
  '*\.lscache',
  '*\.cursor\*'
)

$failed = $false
foreach ($pattern in $banned) {
  foreach ($searchRoot in $searchRoots) {
    if (-not (Test-Path $searchRoot)) { continue }
    $hits = Get-ChildItem -Path $searchRoot -Recurse -File -Include *.cs,*.ts,*.tsx |
      Where-Object {
        $rel = $_.FullName
        -not ($exclude | Where-Object { $rel -like $_ })
      } |
      Select-String -Pattern $pattern -SimpleMatch:$false -CaseSensitive
    if ($hits) {
      Write-Host "BANNED pattern '$pattern' found:" -ForegroundColor Red
      $hits | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
      $failed = $true
    }
  }
}

if ($failed) {
  Write-Error 'Identity guardrails failed — remove legacy users auth references.'
}
Write-Host 'Identity guardrails OK.' -ForegroundColor Green
