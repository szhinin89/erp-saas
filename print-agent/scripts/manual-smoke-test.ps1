param(
    [string]$BaseUrl = "http://127.0.0.1:9817",
    [string]$ApiKey = "local-dev-key-change-me"
)

$ErrorActionPreference = "Stop"
$headers = @{ "X-ZH-PrintAgent-Key" = $ApiKey }
$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$successJobId = "manual-pos-$suffix"
$failureJobId = "manual-pos-fail-$suffix"

Invoke-RestMethod -Method Get -Uri "$BaseUrl/health" -Headers $headers
Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/ready" -Headers $headers

$job = @{
    jobId = $successJobId
    printerName = "POS-80"
    copies = 1
    receipt = @{
        merchantName = "ZH Technologies"
        headerLines = @("Caja 1", "Factura FAKE-001")
        items = @(
            @{ name = "Producto demo"; quantity = 1; unitPrice = 12.50; total = 12.50 }
        )
        totals = @(
            @{ label = "TOTAL"; amount = 12.50 }
        )
        footerLines = @("Gracias por su compra")
    }
} | ConvertTo-Json -Depth 8

Invoke-RestMethod -Method Post -Uri "$BaseUrl/print-jobs" -Headers $headers -ContentType "application/json" -Body $job
Invoke-RestMethod -Method Post -Uri "$BaseUrl/print-jobs" -Headers $headers -ContentType "application/json" -Body $job
Start-Sleep -Seconds 1
Invoke-RestMethod -Method Get -Uri "$BaseUrl/print-jobs/$successJobId" -Headers $headers

$failingJob = @{
    jobId = $failureJobId
    printerName = "FAIL-POS-80"
    copies = 1
    receipt = @{
        merchantName = "ZH Technologies"
        rawLines = @("This job should fail in simulated mode")
    }
} | ConvertTo-Json -Depth 8

Invoke-RestMethod -Method Post -Uri "$BaseUrl/print-jobs" -Headers $headers -ContentType "application/json" -Body $failingJob
Start-Sleep -Seconds 1
Invoke-RestMethod -Method Get -Uri "$BaseUrl/print-jobs/$failureJobId" -Headers $headers
Invoke-RestMethod -Method Post -Uri "$BaseUrl/print-jobs/$failureJobId/retry" -Headers $headers
