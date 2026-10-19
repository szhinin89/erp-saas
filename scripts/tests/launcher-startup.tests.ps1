# Standalone assertion script, not a Pester suite. Run with pwsh -NoProfile -File <this file>.
# An unhandled assertion throws out of -File (exit 1); successful completion returns exit 0.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$path = Join-Path $root 'scripts/dev-launcher.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput([IO.File]::ReadAllText($path), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'FAIL launcher syntax' }

# Evaluate ONLY selected function definitions, never the launcher entry point or prerequisites.
$names = @('Test-ApiExecutable', 'Test-ApiListenerOwnership', 'Wait-ApiReady', 'Start-Backend',
    'Stop-ApiProcess', 'Stop-DevelopmentServices', 'Start-ErpDev', 'Start-BackendOnly')
foreach ($name in $names) {
    $definition = $ast.Find({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    if ($null -eq $definition) { throw "Missing function: $name" }
    Invoke-Expression $definition.Extent.Text
}

# All OS/network/process/build/database/browser effects are replaced; nothing real is launched or stopped.
function Write-Title { param($Message) }
function Write-Step { param($Message) }
function Write-Ok { param($Message) $script:Messages.Add("OK $Message") }
function Write-Warn { param($Message) $script:Messages.Add("WARN $Message") }
function Stop-FrontendProcess { }
function Invoke-Clean { }
function Invoke-DotnetRestore { }
function Ensure-FrontendDependencies { }
function Invoke-DatabaseUpdate { $script:DatabaseCalls++ }
function Invoke-BackendBuild { }
function Start-Frontend { $script:FrontendCalls++; $script:FrontendLaunch = @{} }
function Wait-FrontendReady { param($Launch) }
function Open-Chrome { param($Url) $script:BrowserCalls++ }
function Confirm-SystemInitialized { $script:StatusCalls++; return $false }
function Start-Sleep { param($Seconds) }
function Stop-Process {
    param($Id, [switch]$Force, $ErrorAction)
    if ($script:Mode -eq 'stop_error') { throw 'Simulated stop failure' }
    $script:Stops++
}

function Get-NetTCPConnection {
    [CmdletBinding()]param($LocalPort, $State)
    if ($script:Mode -eq 'occupied' -or $script:Replaced) {
        return [pscustomobject]@{ OwningProcess = 99 }
    }
    if ($script:Spawned -and $script:Mode -ne 'no_listener') {
        return [pscustomobject]@{ OwningProcess = 12 }
    }
}
function Get-CimInstance {
    [CmdletBinding()]param($ClassName, $Filter)
    if ($script:Mode -eq 'inspection_error' -or ($script:Mode -eq 'http_inspection_error' -and $script:HttpCalls -gt 0)) {
        throw 'Simulated process inspection failure'
    }
    if ($Filter -notmatch '^ProcessId = (\d+)$') { throw 'Unexpected CIM query' }
    return $script:Nodes[[int]$Matches[1]]
}
function Invoke-RestMethod {
    [CmdletBinding()]param($Uri, $TimeoutSec)
    $script:HttpCalls++
    if ($script:Mode -eq 'http_fail') { throw 'Simulated HTTP unavailable' }
    if ($script:Mode -eq 'http_replaced') { $script:Replaced = $true }
    if ($script:Mode -eq 'http_exit') { $script:FakeProcess.HasExited = $true }
    return [pscustomobject]@{ data = [pscustomobject]@{ isInitialized = $false } }
}
function Start-Process {
    [CmdletBinding()]param($FilePath, $ArgumentList, [switch]$PassThru)
    $script:Starts++
    $script:Arguments = $ArgumentList
    if (-not $PassThru) { throw 'Process identity not retained' }
    if ($script:Mode -eq 'start_error') { throw 'Simulated process start failure' }
    $script:Spawned = $true
    return $script:FakeProcess
}

function Assert-True { param([bool]$Value, [string]$Reason) if (-not $Value) { throw $Reason } }
function Reset-Scenario {
    param([string]$Mode = 'ready', [bool]$Spawned = $true)
    $script:Mode = $Mode
    $script:Spawned = $Spawned
    $script:Replaced = $false
    $script:HttpCalls = 0; $script:Starts = 0; $script:Stops = 0
    $script:FrontendCalls = 0; $script:BrowserCalls = 0; $script:StatusCalls = 0; $script:DatabaseCalls = 0
    $script:Arguments = @()
    $script:Messages = New-Object 'System.Collections.Generic.List[string]'
    $script:Config = @{ ApiPort = 5003; FrontendPort = 5173; ApiTimeoutSeconds = 2 }
    $script:Urls = @{ Api = 'http://localhost:5003'; Frontend = 'http://localhost:5173'; Swagger = 'http://localhost:5003/swagger' }
    $script:BackendPath = Join-Path $root 'backend'
    $script:ApiProject = Join-Path $BackendPath 'src/ERP.API/ERP.API.csproj'
    $time = [datetime]::SpecifyKind([datetime]'2026-10-19T12:00:00', [DateTimeKind]::Utc)
    $script:FakeProcess = [pscustomobject]@{ Id = 10; Handle = 1; StartTime = $time; HasExited = $false; ExitCode = 1 }
    $script:FakeProcess | Add-Member -MemberType ScriptMethod -Name Refresh -Value { }
    $script:ApiLaunch = [pscustomobject]@{ Process = $FakeProcess; StartTimeUtc = $time }
    $script:Nodes = @{
        10 = [pscustomobject]@{ ProcessId = 10; ParentProcessId = 1; CreationDate = $time; Name = 'powershell.exe'; ExecutablePath = 'C:\Windows\powershell.exe' }
        11 = [pscustomobject]@{ ProcessId = 11; ParentProcessId = 10; CreationDate = $time.AddSeconds(1); Name = 'dotnet.exe'; ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe' }
        12 = [pscustomobject]@{ ProcessId = 12; ParentProcessId = 11; CreationDate = $time.AddSeconds(2); Name = 'ERP.API.exe'; ExecutablePath = (Join-Path (Split-Path $ApiProject -Parent) 'bin/Debug/net10.0/ERP.API.exe') }
        99 = [pscustomobject]@{ ProcessId = 99; ParentProcessId = 1; CreationDate = $time; Name = 'foreign.exe'; ExecutablePath = 'C:\foreign.exe' }
    }
}
function Check {
    param([string]$Name, [scriptblock]$Test)
    & $Test
    Write-Output "PASS $Name"
}
function Assert-NoSuccess {
    Assert-True (-not ($script:Messages -match 'iniciado correctamente')) 'False success message'
    Assert-True ($script:FrontendCalls -eq 0 -and $script:BrowserCalls -eq 0) 'Continued after failed startup'
}

Check 'owned listener and successful HTTP' {
    Reset-Scenario
    Assert-True (Wait-ApiReady -Launch $ApiLaunch) 'Owned API not accepted'
    Assert-True ($HttpCalls -eq 1) 'Unexpected HTTP calls'
}
Check 'HTTP unavailable' {
    Reset-Scenario 'http_fail'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Unavailable API accepted'
}
Check 'no listener' {
    Reset-Scenario 'no_listener'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Missing listener accepted'
    Assert-True ($HttpCalls -eq 0) 'Contacted unknown listener'
}
Check 'premature process exit' {
    Reset-Scenario
    $FakeProcess.HasExited = $true
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Dead process accepted'
    Assert-True ($HttpCalls -eq 0) 'Contacted HTTP after exit'
}
Check 'process inspection unavailable' {
    Reset-Scenario 'inspection_error'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Unverified process accepted'
    Assert-True ($HttpCalls -eq 0) 'HTTP attempted without identity'
}
Check 'inspection failure after HTTP' {
    Reset-Scenario 'http_inspection_error'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Accepted unverifiable HTTP responder'
    Assert-True ($HttpCalls -eq 1 -and [bool]($Messages -match 'despues del HTTP')) 'Inspection failure not explicit'
}
Check 'foreign listener' {
    Reset-Scenario 'occupied'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Foreign listener accepted'
    Assert-True ($HttpCalls -eq 0) 'Contacted foreign process'
}
Check 'same project but unrelated ancestry' {
    Reset-Scenario
    $Nodes[11].ParentProcessId = 99
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Unrelated API accepted'
}
Check 'reused parent PID' {
    Reset-Scenario
    $Nodes[11].CreationDate = $FakeProcess.StartTime.AddSeconds(30)
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Reused parent accepted'
}
Check 'listener replacement during HTTP' {
    Reset-Scenario 'http_replaced'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Replacement listener accepted'
}
Check 'exit during HTTP' {
    Reset-Scenario 'http_exit'
    Assert-True (-not (Wait-ApiReady -Launch $ApiLaunch)) 'Exit during HTTP accepted'
}
Check 'start retains process and propagates native exit' {
    Reset-Scenario 'ready' $false
    Start-Backend
    Assert-True ($ApiLaunch.Process -eq $FakeProcess -and $Starts -eq 1) 'Lost process identity'
    Assert-True (-not ($Arguments -contains '-NoExit')) 'Shell would hide API exit'
    Assert-True (($Arguments -join ' ').Contains('exit $LASTEXITCODE')) 'Missing exit propagation'
}
Check 'start failure clears stale identity' {
    Reset-Scenario 'start_error' $false
    $failed = $false
    try { Start-Backend } catch { $failed = $true }
    Assert-True ($failed -and $null -eq $ApiLaunch) 'Start error ignored or stale identity retained'
}
Check 'port conflict prevents launch' {
    Reset-Scenario 'occupied' $false
    $failed = $false
    try { Start-Backend } catch { $failed = $true }
    Assert-True ($failed -and $Starts -eq 0) 'Launched into occupied port'
}
Check 'foreign port owner is not terminated' {
    Reset-Scenario 'occupied' $false
    $failed = $false
    try { Stop-ApiProcess } catch { $failed = $true }
    Assert-True ($failed -and $Stops -eq 0) 'Stopped unrelated process'
}
Check 'recognized existing API can be stopped' {
    Reset-Scenario
    Stop-ApiProcess
    Assert-True ($Stops -eq 1) 'Existing project API not stopped'
}
Check 'stop failure aborts before replacement' {
    Reset-Scenario 'stop_error'
    $failed = $false
    try { Start-ErpDev } catch { $failed = $true }
    Assert-True ($failed -and $Starts -eq 0 -and $DatabaseCalls -eq 0) 'Continued after stop failure'
    Assert-NoSuccess
}
Check 'full startup preserves pending first-run path' {
    Reset-Scenario 'ready' $false
    Start-ErpDev
    Assert-True ([bool]($Messages -match 'OK ERP iniciado correctamente')) 'Missing full success'
    Assert-True ($StatusCalls -eq 1 -and $FrontendCalls -eq 1 -and $BrowserCalls -eq 2) 'First-run flow changed'
}
Check 'full startup rejects unavailable API' {
    Reset-Scenario 'http_fail' $false
    Start-ErpDev
    Assert-NoSuccess
}
Check 'backend-only success' {
    Reset-Scenario 'ready' $false
    Start-BackendOnly
    Assert-True ([bool]($Messages -match 'OK Backend API iniciado correctamente')) 'Missing backend success'
    Assert-True ($FrontendCalls -eq 0 -and $BrowserCalls -eq 1) 'Backend-only flow changed'
}
Check 'backend-only rejects unavailable API' {
    Reset-Scenario 'http_fail' $false
    Start-BackendOnly
    Assert-NoSuccess
}
Check 'backend-only premature exit' {
    Reset-Scenario 'ready' $false
    $FakeProcess.HasExited = $true
    Start-BackendOnly
    Assert-NoSuccess
}
Check 'full startup aborts on start error' {
    Reset-Scenario 'start_error' $false
    $failed = $false
    try { Start-ErpDev } catch { $failed = $true }
    Assert-True $failed 'Start error swallowed'
    Assert-NoSuccess
}
Check 'full startup refuses foreign port before build/migrations' {
    Reset-Scenario 'occupied' $false
    $failed = $false
    try { Start-ErpDev } catch { $failed = $true }
    Assert-True ($failed -and $Stops -eq 0 -and $Starts -eq 0 -and $DatabaseCalls -eq 0) 'Unsafe conflict handling'
    Assert-NoSuccess
}
