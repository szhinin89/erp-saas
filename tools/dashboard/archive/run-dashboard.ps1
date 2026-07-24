powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-frontend.ps1

powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-tests.ps1

powershell -ExecutionPolicy Bypass -File .\tools\dashboard\analyze-docs.ps1

powershell -ExecutionPolicy Bypass -File .\tools\dashboard\build-dashboard.ps1

powershell -ExecutionPolicy Bypass -File .\tools\dashboard\render-dashboard.ps1