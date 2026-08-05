# Carpeta: C:\ProyectCursor\erp-saas\frontend
# Uso: instalar, limpiar lint, compilar y revisar frontend

$ErrorActionPreference = "Stop"

Set-Location "C:\ProyectCursor\erp-saas\frontend"

function Invoke-Step {
    param([string]$Command)
    Invoke-Expression $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Comando fallo (exit $LASTEXITCODE): $Command" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host "1. Instalar/restaurar paquetes frontend"
Invoke-Step "npm install"

Write-Host "2. Revisar vulnerabilidades npm"
npm audit

Write-Host "3. Ejecutar ESLint"
npm run lint

Write-Host "4. Intentar corrección automática ESLint"
Invoke-Step "npx eslint src --fix"

Write-Host "5. Revisar ESLint después del fix"
Invoke-Step "npm run lint"

Write-Host "6. Compilar frontend"
Invoke-Step "npm run build"

Write-Host "7. Listar pruebas Playwright"
Invoke-Step "npx playwright test --list"

Write-Host "8. Ejecutar pruebas Playwright"
npx playwright test

Write-Host "9. Ver cambios finales"
Set-Location "C:\ProyectCursor\erp-saas"
git status
git diff --stat