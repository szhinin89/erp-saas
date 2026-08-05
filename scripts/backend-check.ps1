# Carpeta: C:\ProyectCursor\erp-saas
# Uso: revisar, limpiar, formatear, compilar y probar backend
# Solucion: backend/src/ERP.slnx

$ErrorActionPreference = "Stop"

Set-Location "C:\ProyectCursor\erp-saas"

function Invoke-Step {
    param([string]$Command)
    Invoke-Expression $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Comando fallo (exit $LASTEXITCODE): $Command" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

$sln = "backend/src/ERP.slnx"

Write-Host "1. Ver estado Git"
git status

Write-Host "2. Limpiar backend"
Invoke-Step "dotnet clean `"$sln`""

Write-Host "3. Eliminar bin/obj"
Get-ChildItem -Path "backend" -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force

Write-Host "4. Restaurar paquetes backend"
Invoke-Step "dotnet restore `"$sln`""

Write-Host "5. Formatear C# con CSharpier"
Invoke-Step "csharpier format backend/src"
Invoke-Step "csharpier format backend/tests"

Write-Host "6. Formatear solución con dotnet format"
Invoke-Step "dotnet format `"$sln`""

Write-Host "7. Compilar backend"
Invoke-Step "dotnet build `"$sln`""

Write-Host "8. Ejecutar pruebas backend"
Invoke-Step "dotnet test `"$sln`""

Write-Host "9. Ver cambios finales"
git status
git diff --stat