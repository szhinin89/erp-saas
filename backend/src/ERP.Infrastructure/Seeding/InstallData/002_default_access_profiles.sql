-- ============================================================================
-- Install default access profile seeder function.
-- Executed ONCE at system startup by InstallDataBootstrapService (checksum-based).
--
-- The function erp_seed_tenant_default_profiles(tenant_id, actor_id) is called
-- from C# (DefaultProfileSeeder) every time a new tenant is created.
-- It is also available for manual/emergency use from a psql session.
--
-- Profiles seeded for each tenant:
--   · Facturador  — billing operator (invoice create/view/void)
--   · Bodeguero   — warehouse operator (stock, transfers, adjustments)
--   · Contador    — accountant (accounting read, invoice read)
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
        (gen_random_uuid(), p_tenant_id, 'Facturador', 'Billing operator — can create and void invoices.',   TRUE, NOW(), p_actor_id),
        (gen_random_uuid(), p_tenant_id, 'Bodeguero',  'Warehouse operator — manages stock and transfers.',  TRUE, NOW(), p_actor_id),
        (gen_random_uuid(), p_tenant_id, 'Contador',   'Accountant — read-only access to accounting data.',  TRUE, NOW(), p_actor_id)
    ON CONFLICT (tenant_id, name) DO NOTHING;

    -- Resolve IDs (profiles may have existed before this call)
    SELECT id INTO v_facturador_id FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Facturador';
    SELECT id INTO v_bodeguero_id  FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Bodeguero';
    SELECT id INTO v_contador_id   FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Contador';

    -- ── 2. Facturador permissions ──────────────────────────────────────────
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_facturador_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'ventas.invoice.view',
        'ventas.invoice.create',
        'ventas.invoice.update',
        'ventas.invoice.void',
        'ventas.credit-note.view',
        'ventas.credit-note.create',
        'ventas.customers.view',
        'ventas.customers.create',
        'ventas.customers.update',
        'inventario.products.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

    -- ── 3. Bodeguero permissions ───────────────────────────────────────────
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_bodeguero_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'inventario.products.view',
        'inventario.warehouses.view',
        'inventario.transfers.view',
        'inventario.transfers.create',
        'inventario.adjustments.view',
        'inventario.adjustments.create',
        'compras.orders.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

    -- ── 4. Contador permissions ────────────────────────────────────────────
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_contador_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'accounting.config.view',
        'accounting.accounts.view',
        'accounting.accounts.create',
        'accounting.accounts.edit',
        'accounting.journal.view',
        'ventas.invoice.view',
        'compras.orders.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

END;
$$;

COMMENT ON FUNCTION erp_seed_tenant_default_profiles(UUID, UUID) IS
    'Seeds Facturador / Bodeguero / Contador access profiles for a given tenant. Idempotent.';
