# Bulk rename SaaS domain in frontend
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..'
$frontend = Join-Path $root 'frontend'

$files = Get-ChildItem $frontend -Recurse -Include '*.ts','*.tsx','*.json' | Where-Object {
    $_.FullName -notmatch 'node_modules|dist|\.vite'
}

$replacements = @(
    @('TenantSaasSubscription', 'SubscriberSubscription'),
    @('SaasPlanFeature', 'CommercialPlanFeature'),
    @('SaasPlanAdmin', 'CommercialPlanAdmin'),
    @('SaasPlan', 'CommercialPlan'),
    @('SaasFeature', 'PlatformFeature'),
    @('TenantCustomMenu', 'SubscriberCustomMenu'),
    @('TenantEntitlements', 'SubscriberEntitlements'),
    @('tenantEntitlements', 'subscriberEntitlements'),
    @('listSaasPlansAdmin', 'listCommercialPlansAdmin'),
    @('createSaasPlan', 'createCommercialPlan'),
    @('updateSaasPlan', 'updateCommercialPlan'),
    @('deleteSaasPlan', 'deleteCommercialPlan'),
    @('saas-plans', 'commercial-plans'),
    @('api/saas/entitlements', 'api/subscribers/entitlements'),
    @('superadmin/tenants', 'superadmin/subscribers'),
    @('api/tenants', 'api/subscribers'),
    @('select-tenant', 'select-subscriber'),
    @('switch-tenant', 'switch-subscriber'),
    @('TenantSelect', 'SubscriberSelect'),
    @('TenantAccess', 'SubscriberAccess'),
    @('tenantAccess', 'subscriberAccess'),
    @('tenantSlug', 'subscriberSlug'),
    @('TenantSlug', 'SubscriberSlug'),
    @('tenantName', 'subscriberName'),
    @('TenantName', 'SubscriberName'),
    @('tenantId', 'subscriberId'),
    @('TenantId', 'SubscriberId'),
    @('tenantIds', 'subscriberIds'),
    @('TenantIds', 'SubscriberIds'),
    @('tenant_id', 'subscriber_id'),
    @('tenant_ids', 'subscriber_ids'),
    @('tenant-demo', 'subscriber-demo'),
    @('GLOBAL_TENANT_ID', 'GLOBAL_SUBSCRIBER_ID'),
    @('tenantIds.ts', 'subscriberIds.ts'),
    @('impersonation-tenant', 'impersonation-subscriber'),
    @('ZHAppTenantHeader', 'ZHAppSubscriberHeader'),
    @('companiesTenantDetailNav', 'companiesSubscriberDetailNav'),
    @('Membership', 'CompanyUserMembership'),
    @('memberships', 'company_user_memberships'),
    @('ICurrentTenant', 'ICurrentSubscriber'),
    @('CurrentTenant', 'CurrentSubscriber'),
    @('SwitchTenant', 'SwitchSubscriber'),
    @('SuperAdminTenants', 'SuperAdminSubscribers'),
    @('SuperAdminTenant', 'SuperAdminSubscriber'),
    @('Tenant ', 'Subscriber '),
    @('Tenant)', 'Subscriber)'),
    @('Tenant,', 'Subscriber,'),
    @('Tenant.', 'Subscriber.'),
    @('<Tenant>', '<Subscriber>'),
    @('tenants', 'subscribers')
)

$count = 0
foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $original = $content
    foreach ($r in $replacements) {
        $content = $content.Replace($r[0], $r[1])
    }
    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($file.FullName, $content)
        $count++
    }
}
Write-Host "Updated $count frontend files"
