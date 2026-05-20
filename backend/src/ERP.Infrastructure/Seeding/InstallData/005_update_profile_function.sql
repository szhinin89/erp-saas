-- ============================================================================
-- 005_update_profile_function.sql
-- Redefine erp_seed_tenant_default_profiles() with current English permission
-- keys, aligned with Permissions.cs (refactored in commit that renamed all
-- keys from ventas/inventario/compras/accounting → sales/inventory/purchases/finance).
--
-- NOTE: C# DefaultProfileSeeder (EF Core) is the primary path for tenant
-- onboarding. This function is kept as an emergency / psql manual tool only.
-- ============================================================================

CREATE OR REPLACE FUNCTION erp_seed_tenant_default_profiles(
    p_tenant_id UUID,
    p_actor_id  UUID
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_facturador_id UUID;
    v_bodeguero_id  UUID;
    v_contador_id   UUID;
BEGIN

    -- ── 1. Create profiles (roles) — idempotent ────────────────────────────
    INSERT INTO access_profiles
        (id, tenant_id, name, description, is_active, created_at, created_by)
    VALUES
        (gen_random_uuid(), p_tenant_id, 'Facturador', 'Billing operator — can create and void invoices.',  TRUE, NOW(), p_actor_id),
        (gen_random_uuid(), p_tenant_id, 'Bodeguero',  'Warehouse operator — manages stock and transfers.', TRUE, NOW(), p_actor_id),
        (gen_random_uuid(), p_tenant_id, 'Contador',   'Accountant — read-only access to accounting data.', TRUE, NOW(), p_actor_id)
    ON CONFLICT (tenant_id, name) DO NOTHING;

    SELECT id INTO v_facturador_id FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Facturador';
    SELECT id INTO v_bodeguero_id  FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Bodeguero';
    SELECT id INTO v_contador_id   FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Contador';

    -- ── 2. Facturador permissions ──────────────────────────────────────────
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_facturador_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'sales.invoices.view',
        'sales.invoices.create',
        'sales.invoices.update',
        'sales.invoices.void',
        'sales.credit-notes.view',
        'sales.credit-notes.create',
        'sales.customers.view',
        'sales.customers.create',
        'sales.customers.update',
        'inventory.products.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

    -- ── 3. Bodeguero permissions ───────────────────────────────────────────
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_bodeguero_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'inventory.products.view',
        'inventory.warehouses.view',
        'inventory.transfers.view',
        'inventory.transfers.create',
        'inventory.adjustments.view',
        'inventory.adjustments.create',
        'purchases.orders.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

    -- ── 4. Contador permissions ────────────────────────────────────────────
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_contador_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'finance.config.view',
        'finance.accounts.view',
        'finance.accounts.create',
        'finance.accounts.edit',
        'finance.journal.view',
        'sales.invoices.view',
        'purchases.orders.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

END;
$$;

COMMENT ON FUNCTION erp_seed_tenant_default_profiles(UUID, UUID) IS
    'Seeds Facturador / Bodeguero / Contador access profiles for a given tenant. Idempotent. Emergency/psql tool — C# path: DefaultProfileSeeder.';
