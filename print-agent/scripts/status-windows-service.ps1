param(
    [string]$ServiceName = "ZHPrintAgent",
    [switch]$CheckHealth,
    [string]$BaseUrl = "http://127.0.0.1:9817",
    [string]$ApiKey
)

$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Host "Service '$ServiceName' is not installed."
    exit 1
}

$service | Format-List Name, DisplayName, Status, StartType

if ($CheckHealth) {
    if (-not $ApiKey) {
        Write-Host "`nNote: /health and /health/ready require the API key (-ApiKey) once setup is completed."
    }

    $headers = @{}
    if ($ApiKey) {
        $headers["X-ZH-PrintAgent-Key"] = $ApiKey
    }

    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Headers $headers -TimeoutSec 5
        Write-Host "`n/health -> $($health.status)"
    } catch {
        Write-Host "`n/health -> unreachable ($($_.Exception.Message))"
    }

    try {
        $ready = Invoke-RestMethod -Uri "$BaseUrl/health/ready" -Headers $headers -TimeoutSec 5
        Write-Host "/health/ready -> $($ready.status)"
    } catch {
        Write-Host "/health/ready -> not ready or unreachable ($($_.Exception.Message))"
    }
}
