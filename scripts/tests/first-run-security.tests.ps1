$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$path = Join-Path $root 'scripts/dev-launcher.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseInput([IO.File]::ReadAllText($path), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Launcher: error de sintaxis' }
$source = [IO.File]::ReadAllText($path)
foreach ($forbidden in @('Invoke-AutoSetup', 'Get-SetupTokenFromLog', 'Invoke-FirstRunWizard', 'Read-WizardValue', 'setupToken', '/setup/admin', '/auth/login', 'defaultPassword', 'Tee-Object', '.api-startup.log')) {
    if ($source.Contains($forbidden)) { throw "Launcher contiene superficie prohibida: $forbidden" }
}
$drop = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-DatabaseDrop' }, $true).Extent.Text
if (-not $drop.Contains('-- --launcher-reset=true')) { throw 'Drop sin guard efectivo de EF' }
foreach ($variable in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT')) {
    if (-not $drop.Contains('$env:' + $variable + ' = "Development"')) { throw 'Drop sin entorno fijo' }
}
$factory = [IO.File]::ReadAllText((Join-Path $root 'backend/src/ERP.API/Hosting/ErpDesignTimeDbContextFactory.cs'))
if ($factory.IndexOf('LauncherResetGuard.Confirm') -gt $factory.IndexOf('new DbContextOptionsBuilder')) { throw 'Guard posterior a construir contexto' }
if (-not $factory.Contains('LauncherResetGuard.Confirm(connectionString, environment')) { throw 'Guard no usa conexiÃ³n efectiva' }
Write-Output 'PASS: sintaxis, launcher sin alta/credenciales/captura, drop protegido y guard sobre conexiÃ³n efectiva.'
