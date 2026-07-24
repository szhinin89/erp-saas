<#
.SYNOPSIS
  Reset greenfield de PostgreSQL local: schema EF + InstallData (001-002).

.DESCRIPTION
  1. dotnet ef database drop --force
  2. dotnet ef database update
  3. (Opcional) recordatorio para arrancar API y ejecutar first-run platform operator

  Requiere PostgreSQL accesible con ConnectionStrings:DefaultConnection del API.
#>
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$infraDir = Join-Path $repoRoot 'backend\src\ERP.Infrastructure'
$apiProj  = Join-Path $repoRoot 'backend\src\ERP.API\ERP.API.csproj'

Push-Location $infraDir
try {
    if (-not $SkipBuild) {
        Write-Host 'Building ERP.API...'
        dotnet build $apiProj -v q
    }

    Write-Host 'Dropping database (force)...'
    dotnet ef database drop --force --startup-project $apiProj

    Write-Host 'Applying migrations...'
    dotnet ef database update --startup-project $apiProj

    Write-Host ''
    Write-Host 'Greenfield DB ready.'
    Write-Host 'Next: start API (InstallData 001-002 runs on startup) then:'
    Write-Host '  .\scripts\setup\Crear-PlatformOperator.ps1'
}
finally {
    Pop-Location
}
