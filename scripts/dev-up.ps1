# Levanta PostgreSQL local vía docker-compose (raíz del repo).
# Uso:  pwsh ./scripts/dev-up.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

Write-Host "==> docker compose up -d (desde $repoRoot)" -ForegroundColor Cyan
docker compose up -d
docker compose ps

Write-Host "`nSiguiente: copiar ERP.API/appsettings.Development.json.example -> appsettings.Development.json y ejecutar dotnet ef database update (ver docs/DESARROLLO.md)." -ForegroundColor Green
