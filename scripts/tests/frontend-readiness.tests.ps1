# Reuse simulated ownership fixtures; no real process, HTTP or database effects.
. "$PSScriptRoot/frontend-process-ownership.tests.ps1"
foreach ($name in 'Assert-FrontendListeners','Wait-FrontendReady') {
    $definition = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true)
    Invoke-Expression $definition.Extent.Text
}
function Invoke-WebRequest {
    [CmdletBinding()]param($Uri,[switch]$UseBasicParsing,$TimeoutSec)
    $script:HttpCalls++
    Assert ($Uri -eq 'http://localhost:5173/@vite/client') 'Wrong Vite probe'
    if ($Mode -eq 'http_fail') { throw 'HTTP unavailable' }
    if ($Mode -eq 'http_exit') { $Shell.HasExited=$true }
    if ($Mode -eq 'http_foreign') { $script:Mode='foreign' }
    $content='vite client'
    if ($Mode -eq 'not_vite') { $content='unrelated application' }
    return [pscustomobject]@{ StatusCode=200; Content=$content }
}
function Ready-Reset { param($Scenario='owned') Reset $Scenario; $Config.FrontendTimeoutSeconds=2; $script:HttpCalls=0 }
Check 'Vite readiness validates ownership before and after HTTP' {
    Ready-Reset; Wait-FrontendReady $FrontendLaunch
    Assert ($HttpCalls -eq 1 -and $SocketReads -eq 2) 'Missing HTTP or ownership recheck'
}
foreach ($scenario in 'free','foreign','mixed','socket_error','inspection_error','http_fail','http_exit','http_foreign','not_vite') {
    Check "Vite readiness rejects $scenario" {
        Ready-Reset $scenario; $failed=$false
        try { Wait-FrontendReady $FrontendLaunch } catch { $failed=$true }
        Assert $failed 'False frontend success'
    }
}
Check 'Vite readiness rejects exited launch before HTTP' {
    Ready-Reset; $Shell.HasExited=$true; $failed=$false
    try { Wait-FrontendReady $FrontendLaunch } catch { $failed=$true }
    Assert ($failed -and $HttpCalls -eq 0) 'Exited frontend accepted'
}
# Exercise both menu paths with readiness outcome and initialization state controlled.
function Wait-FrontendReady { param($Launch) if ($script:FailReady) { throw 'FAIL synthetic frontend' } }
function Confirm-SystemInitialized { return $script:Initialized }
function Open-Chrome { param($Url) $script:Opened.Add($Url) }
function Write-Ok { param($Message) $script:Success.Add($Message) }
foreach ($flow in 'Start-ErpDev','Invoke-FullReset') {
    foreach ($initialized in $true,$false) {
        Check "$flow routes initialized=$initialized" {
            Reset 'free'; $script:FrontendLaunch=$null; $script:Initialized=$initialized; $script:FailReady=$false
            $script:Opened=New-Object 'System.Collections.Generic.List[string]'
            $script:Success=New-Object 'System.Collections.Generic.List[string]'
            & $flow
            $expected=$Urls.Frontend
            if (-not $initialized) { $expected+='/setup' }
            Assert ($Opened[0] -eq $expected -and $Opened.Count -eq 2) 'Wrong startup route'
        }
    }
    Check "$flow frontend failure prevents browsers and final success" {
        Reset 'free'; $script:FrontendLaunch=$null; $script:Initialized=$false; $script:FailReady=$true
        $script:Opened=New-Object 'System.Collections.Generic.List[string]'
        $script:Success=New-Object 'System.Collections.Generic.List[string]'
        $failed=$false
        try { & $flow } catch { $failed=$true }
        Assert ($failed -and $Opened.Count -eq 0 -and -not ($Success -match 'ERP iniciado correctamente|Reset completo finalizado')) 'False flow success'
    }
}
