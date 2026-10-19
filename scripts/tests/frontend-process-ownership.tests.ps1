# Standalone assertions; all process/socket effects are simulated. Not a Pester suite.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput(
    [IO.File]::ReadAllText((Join-Path $root 'scripts/dev-launcher.ps1')), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'FAIL launcher syntax' }
foreach ($name in 'Start-Frontend','Stop-FrontendProcess','Test-FrontendListenerOwnership','Stop-DevelopmentServices','Start-ErpDev','Start-BackendOnly','Invoke-FullReset') {
    $definition = $ast.Find({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    if ($null -eq $definition) { throw "Missing function $name" }
    Invoke-Expression $definition.Extent.Text
}
function Assert { param([bool]$Condition, [string]$Message) if (-not $Condition) { throw $Message } }
function Check { param($Name, [scriptblock]$Test) & $Test; Write-Output "PASS $Name" }
function Write-Step { param($Message) }
function Write-Title { param($Message) }
function Write-Warn { param($Message) }
function Write-Ok { param($Message) }
# Defensive guard: a regression must never reach the real OS termination cmdlet.
function Stop-Process { throw 'Unsafe PID-based termination attempted' }
function Start-Sleep { param($Seconds) }
function Wait-FrontendReady { param($Launch) }
function Open-Chrome { param($Url) $script:Browsers++ }
function Stop-ApiProcess { $script:ApiStops++ }
function Invoke-Clean { }
function Invoke-DotnetRestore { }
function Ensure-FrontendDependencies { }
function Invoke-DatabaseUpdate { $script:DatabaseCalls++ }
function Invoke-DatabaseDrop { $script:Drops++ }
function Invoke-BackendBuild { }
function Start-Backend { $script:BackendStarts++; $script:ApiLaunch = @{} }
function Wait-ApiReady { param($Launch) return $true }
function Confirm-SystemInitialized { $script:StatusCalls++; return $false }
function Read-Host { param($Prompt) return 'RESETEAR' }
function Get-Command {
    [CmdletBinding()]param($Name)
    if ($Name -ne 'node.exe') { throw 'Unexpected command inspection' }
    return [pscustomobject]@{ Source='C:\Program Files\nodejs\node.exe' }
}
function Start-Process {
    [CmdletBinding()]param($FilePath,$ArgumentList,[switch]$PassThru)
    Assert $PassThru 'Frontend identity not retained'
    $script:Starts++; $script:Arguments=$ArgumentList
    if ($script:Mode -eq 'start_error') { throw 'Synthetic launch failure' }
    return $script:Shell
}
function Get-NetTCPConnection {
    [CmdletBinding()]param($State)
    Assert ($State -eq 'Listen') 'Non-listener sockets queried'
    $script:SocketReads++
    if ($script:Mode -eq 'socket_error') { throw 'Synthetic socket inspection failure' }
    if ($script:Mode -eq 'free' -or $script:Mode -eq 'start_error') { return }
    if ($script:Mode -eq 'changed_listener' -and $script:SocketReads -gt 1) {
        return [pscustomobject]@{ LocalPort=5173; State='Listen'; OwningProcess=99 }
    }
    if ($script:Mode -eq 'foreign') {
        return [pscustomobject]@{ LocalPort=5173; State='Listen'; OwningProcess=99 }
    }
    if ($script:Mode -eq 'mixed') {
        [pscustomobject]@{ LocalPort=5173; State='Listen'; OwningProcess=99 }
    }
    [pscustomobject]@{ LocalPort=5173; State='Listen'; OwningProcess=12 }
}
function Get-CimInstance {
    [CmdletBinding()]param($ClassName,$Filter)
    $script:CimReads++
    if ($script:Mode -eq 'inspection_error') { throw 'Synthetic CIM error' }
    if ($Filter -notmatch '^ProcessId = (\d+)$') { throw 'Unexpected CIM inspection' }
    $node = $script:Nodes[[int]$Matches[1]]
    if ($script:Mode -eq 'changed_identity' -and $script:CimReads -gt 3 -and $node.ProcessId -eq 12) {
        $node.CreationDate=$node.CreationDate.AddMinutes(1)
    }
    return $node
}
function Get-Process {
    [CmdletBinding()]param($Id)
    if ($script:Mode -eq 'missing_process') { throw 'Synthetic exited process' }
    return $script:Processes[$Id]
}
function Fake-Process {
    param($Id,$Time)
    $fake=[pscustomobject]@{ Id=$Id; Handle=$Id; StartTime=$Time; HasExited=$false; Disposed=$false }
    $fake | Add-Member ScriptMethod Refresh { }
    $fake | Add-Member ScriptMethod Dispose { $this.Disposed=$true }
    $fake | Add-Member ScriptMethod Kill {
        # The retained object must remain the target even if its numeric PID is recycled.
        Assert ($this.Id -eq 12) 'Foreign process terminated'
        if ($script:Mode -eq 'stop_error') { throw 'Synthetic termination failure' }
        $script:Kills++; $this.HasExited=$true; $script:Shell.HasExited=$true
    }
    return $fake
}
function Reset {
    param($Mode='owned')
    $script:Mode=$Mode
    $script:Starts=0; $script:Kills=0; $script:SocketReads=0; $script:CimReads=0
    $script:BackendStarts=0; $script:ApiStops=0; $script:Browsers=0; $script:StatusCalls=0
    $script:DatabaseCalls=0; $script:Drops=0; $script:Arguments=@()
    $script:Config=@{ FrontendPort=5173 }
    $script:Urls=@{ Frontend='http://localhost:5173'; Swagger='http://localhost:5003/swagger' }
    $script:FrontendPath=Join-Path $root 'frontend'
    $time=[datetime]::SpecifyKind([datetime]'2026-10-19T12:00:00',[DateTimeKind]::Utc)
    $script:Shell=Fake-Process 10 $time
    $script:Node=Fake-Process 12 ($time.AddSeconds(2))
    $script:Processes=@{ 12=$Node; 99=(Fake-Process 99 $time) }
    $script:FrontendLaunch=[pscustomobject]@{ Process=$Shell; StartTimeUtc=$time; NodePath='C:\Program Files\nodejs\node.exe' }
    $script:Nodes=@{
        10=[pscustomobject]@{ ProcessId=10; ParentProcessId=1; CreationDate=$time; Name='powershell.exe'; ExecutablePath='C:\Windows\powershell.exe' }
        11=[pscustomobject]@{ ProcessId=11; ParentProcessId=10; CreationDate=$time.AddSeconds(1); Name='node.exe'; ExecutablePath=$FrontendLaunch.NodePath }
        12=[pscustomobject]@{ ProcessId=12; ParentProcessId=11; CreationDate=$time.AddSeconds(2); Name='node.exe'; ExecutablePath=$FrontendLaunch.NodePath }
        99=[pscustomobject]@{ ProcessId=99; ParentProcessId=1; CreationDate=$time; Name='node.exe'; ExecutablePath=$FrontendLaunch.NodePath }
    }
}
function Rejected {
    $failed=$false
    try { Stop-FrontendProcess } catch { $failed=$true }
    Assert ($failed -and $Kills -eq 0) 'Unverified ownership accepted or a process killed'
}
Check 'owned frontend stopped through retained handle' {
    Reset; Stop-FrontendProcess
    Assert ($Kills -eq 1 -and $Node.Disposed) 'Owned frontend not stopped/disposed'
}
Check 'foreign node at same executable path rejected' { Reset 'foreign'; Rejected }
Check 'mixed own and foreign listeners cause no termination' { Reset 'mixed'; Rejected }
Check 'listener without retained launch rejected' { Reset; $script:FrontendLaunch=$null; Rejected }
Check 'unrelated node ancestry rejected' { Reset; $Nodes[11].ParentProcessId=99; Rejected }
Check 'different executable path rejected' { Reset; $Nodes[12].ExecutablePath='C:\other\node.exe'; Rejected }
Check 'different executable name rejected' { Reset; $Nodes[12].Name='foreign.exe'; Rejected }
Check 'missing executable path rejected' { Reset; $Nodes[12].ExecutablePath=$null; Rejected }
Check 'reused listener PID rejected' { Reset; $Nodes[12].CreationDate=$Nodes[12].CreationDate.AddSeconds(3); Rejected }
Check 'reused root PID rejected' { Reset; $Nodes[10].CreationDate=$Nodes[10].CreationDate.AddMilliseconds(10); Rejected }
Check 'reused intermediate PID rejected' { Reset; $Nodes[11].CreationDate=$Nodes[11].CreationDate.AddSeconds(20); Rejected }
Check 'listener changed before stop rejected' { Reset 'changed_listener'; Rejected }
Check 'identity changed during inspection rejected' { Reset 'changed_identity'; Rejected }
Check 'exited listener rejected' { Reset; $Node.HasExited=$true; Rejected }
Check 'exited root rejected' { Reset; $Shell.HasExited=$true; Rejected }
Check 'process disappears before handle acquisition rejected' { Reset 'missing_process'; Rejected }
Check 'CIM failure rejected and handle disposed' { Reset 'inspection_error'; Rejected; Assert $Node.Disposed 'Leaked handle' }
Check 'socket inspection error rejected' { Reset 'socket_error'; Rejected }
Check 'stop error propagated and handle disposed' { Reset 'stop_error'; Rejected; Assert $Node.Disposed 'Leaked handle' }
Check 'empty listener set does not inspect or stop any process' {
    Reset 'free'; Stop-FrontendProcess; Assert ($Kills -eq 0 -and $CimReads -eq 0) 'Unsafe empty-port behavior'
}
Check 'start retains root identity and expected node path' {
    Reset 'free'; $script:FrontendLaunch=$null; Start-Frontend
    Assert ($Starts -eq 1 -and $FrontendLaunch.Process -eq $Shell -and $FrontendLaunch.NodePath -eq $Nodes[12].ExecutablePath) 'Lost frontend identity'
    Assert (($Arguments -contains '-NoProfile') -and -not ($Arguments -contains '-NoExit')) 'Unsafe child shell lifecycle'
    Assert (($Arguments -join ' ').Contains('--strictPort') -and ($Arguments -join ' ').Contains('exit $LASTEXITCODE')) 'Port fallback or exit propagation missing'
    Assert (($Arguments -join ' ').Contains('& npm.cmd run dev')) 'npm.ps1 policy regression'
}
Check 'occupied frontend port prevents new launch' {
    Reset 'foreign'; $failed=$false
    try { Start-Frontend } catch { $failed=$true }
    Assert ($failed -and $Starts -eq 0) 'Started into occupied port'
}
Check 'launch failure leaves no fabricated identity' {
    Reset 'start_error'; $script:FrontendLaunch=$null
    $failed=$false
    try { Start-Frontend } catch { $failed=$true }
    Assert ($failed -and $Starts -eq 1 -and $null -eq $FrontendLaunch) 'Launch failure retained stale identity'
}
Check 'exited previous root can be replaced' {
    Reset 'free'; $Shell.HasExited=$true; Start-Frontend
    Assert ($Starts -eq 1) 'Could not replace exited previous launch'
}
Check 'live retained root prevents duplicate start even without listener' {
    Reset 'free'; $failed=$false
    try { Start-Frontend } catch { $failed=$true }
    Assert ($failed -and $Starts -eq 0) 'Lost live previous frontend ownership'
}
Check 'full start preserves pending first-run path' {
    Reset 'free'; $script:FrontendLaunch=$null; Start-ErpDev
    Assert ($Starts -eq 1 -and $BackendStarts -eq 1 -and $StatusCalls -eq 1 -and $Browsers -eq 2) 'Full option changed'
}
Check 'full start foreign frontend aborts before cleanup migrations and start' {
    Reset 'foreign'; $failed=$false
    try { Start-ErpDev } catch { $failed=$true }
    Assert ($failed -and $Kills -eq 0 -and $DatabaseCalls -eq 0 -and $BackendStarts -eq 0) 'Full option ignored foreign frontend'
}
Check 'backend-only does not inspect or stop frontend' {
    Reset 'foreign'; Start-BackendOnly
    Assert ($Kills -eq 0 -and $SocketReads -eq 0 -and $Starts -eq 0 -and $BackendStarts -eq 1) 'Backend-only changed frontend'
}
Check 'reset callsite retains frontend identity without real reset' {
    Reset 'free'; $script:FrontendLaunch=$null; Invoke-FullReset
    Assert ($Drops -eq 1 -and $Starts -eq 1 -and $StatusCalls -eq 1 -and $Browsers -eq 2) 'Reset callsite changed'
}
