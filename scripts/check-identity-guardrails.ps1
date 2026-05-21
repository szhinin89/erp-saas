#Requires -Version 5.1
<#
.SYNOPSIS
  Falla si el repositorio reintroduce referencias al auth legacy (tabla users).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root 'backend'))) {
  $root = Split-Path -Parent $PSScriptRoot
}

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
  '*erp_auth.ps1*'
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
