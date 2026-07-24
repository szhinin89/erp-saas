# =============================================================================
# ZH Technologies
# Generate Project Model
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DashboardRoot = Join-Path $ProjectRoot "docs\ProgressDashboard"
$DataFolder = Join-Path $DashboardRoot "data"

Write-Host ""
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " Building Project Model"
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""

$model = @{

    Project = "ZH ERP SaaS"

    Version = "4.0"

    Layers = @(

        @{
            Id="web"
            Name="Web ERP"

            Domains=@(

                @{
                    Id="configuration"
                    Name="Configuration"
                },

                @{
                    Id="security"
                    Name="Security"
                },

                @{
                    Id="business-partners"
                    Name="Business Partners"
                },

                @{
                    Id="crm"
                    Name="CRM"
                },

                @{
                    Id="purchases"
                    Name="Purchases"
                },

                @{
                    Id="inventory"
                    Name="Inventory"
                },

                @{
                    Id="sales"
                    Name="Sales"
                },

                @{
                    Id="cash"
                    Name="Cash"
                },

                @{
                    Id="accounting"
                    Name="Accounting"
                },

                @{
                    Id="reports"
                    Name="Reports"
                }
            )
        },

        @{
            Id="core"
            Name="Core Services"

            Domains=@(

                @{
                    Id="electronic-documents"
                    Name="Electronic Documents"
                }
            )
        }
    )

}

$model |
ConvertTo-Json -Depth 50 |
Set-Content (Join-Path $DataFolder "project-model.json") -Encoding UTF8

Write-Host ""
Write-Host "Project Model Created Successfully." -ForegroundColor Green