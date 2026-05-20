# Fix double-replacements and leftover Tenant namespaces
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..'
$targets = @(
    (Join-Path $root 'backend'),
    (Join-Path $root 'frontend')
)

$excludePatterns = @('*Migrations\202*', '*ErpDbContextModelSnapshot.cs', '*.Designer.cs', '*node_modules*', '*dist*')

$replacements = @(
    @('CompanyUserCompanyUserMemberships', 'CompanyUserMemberships'),
    @('CompanyUserCompanyUserMembership', 'CompanyUserMembership'),
    @('company_user_company_user_memberships', 'company_user_memberships'),
    @('ERP.Domain.Tenants', 'ERP.Domain.Subscribers'),
    @('ERP.Application.Modules.Tenants', 'ERP.Application.Modules.Subscribers'),
    @('IMustHaveTenant', 'IMustHaveSubscriber'),
    @('private Tenant()', 'private Subscriber()'),
    @('GetCompanyUserMembershipsByTenantAsync', 'GetCompanyUserMembershipsBySubscriberAsync'),
    @('CountActiveCompanyUserMembershipsByTenantAsync', 'CountActiveCompanyUserMembershipsBySubscriberAsync'),
    @('GetTenantCompanyUserMemberships', 'GetSubscriberCompanyUserMemberships'),
    @('TenantCompanyUserMembershipItemDto', 'SubscriberCompanyUserMembershipItemDto'),
    @('TenantMembership', 'SubscriberMembership'),
    @('TenantAccess', 'SubscriberAccess'),
    @('UseCases\TenantAccess', 'UseCases\SubscriberAccess'),
    @('Modules\Tenants', 'Modules\Subscribers'),
    @('TenantsController', 'SubscribersController'),
    @('TenantRepository', 'SubscriberRepository'),
    @('ITenantRepository', 'ISubscriberRepository'),
    @('TenantConfiguration', 'SubscriberConfiguration'),
    @('TenantDto', 'SubscriberDto'),
    @('TenantPublicSettingsDto', 'SubscriberPublicSettingsDto'),
    @('TenantEntitlementsService', 'SubscriberEntitlementsService'),
    @('TenantSubscriptionOverridesService', 'SubscriptionFeatureOverridesService'),
    @('TenantOnboardingService', 'SubscriberOnboardingService'),
    @('TenantMenuService', 'SubscriberMenuService'),
    @('TenantIamMenuMerger', 'SubscriberIamMenuMerger'),
    @('CurrentTenantService', 'CurrentSubscriberService'),
    @('ManualCurrentTenant', 'ManualCurrentSubscriber'),
    @('JobTenantContext', 'JobSubscriberContext'),
    @('TenantCustomMenu', 'SubscriberCustomMenu'),
    @('TenantSubscriptionTests', 'SubscriberSubscriptionTests'),
    @('UpdateTenantSubscription', 'UpdateSubscriberSubscription'),
    @('UpdateTenantOperationalSettings', 'UpdateSubscriberOperationalSettings'),
    @('UpdateTenantGlobalParameters', 'UpdateSubscriberGlobalParameters'),
    @('UpdateTenantCompany', 'UpdateSubscriberCompany'),
    @('UpdateTenantPasswordResetMode', 'UpdateSubscriberPasswordResetMode'),
    @('CreateTenant', 'CreateSubscriber'),
    @('RegisterTenantWithAdmin', 'RegisterSubscriberWithAdmin'),
    @('SuperAdminTenant', 'SuperAdminSubscriber'),
    @('SwitchTenant', 'SwitchSubscriber'),
    @('SaasEntitlementsController', 'SubscriberEntitlementsController')
)

$total = 0
foreach ($base in $targets) {
    $files = Get-ChildItem $base -Recurse -Include '*.cs','*.ts','*.tsx','*.json' -ErrorAction SilentlyContinue | Where-Object {
        $p = $_.FullName
        -not ($excludePatterns | Where-Object { $p -like $_ })
    }
    foreach ($file in $files) {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        $original = $content
        foreach ($r in $replacements) {
            $content = $content.Replace($r[0], $r[1])
        }
        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($file.FullName, $content)
            $total++
        }
    }
}
Write-Host "Cleanup updated $total files"
