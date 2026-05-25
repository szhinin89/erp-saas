-- 009: remove standalone /finance/config nav item — config is now a tab within /finance/accounts
-- Safe to run multiple times (DELETE is idempotent).

-- § 1: Remove /finance/config from ui_nav_items (all groups)
DELETE FROM ui_nav_items
WHERE route_path = '/finance/config';

-- § 2: Fix any plan menu_config JSON that has /finance/config as a standalone item
--      Replace the route_path value in the JSONB array of items.
UPDATE commercial_plans
SET menu_config = replace(menu_config::text, '"/finance/config"', '"/finance/accounts?tab=config"')::jsonb
WHERE menu_config::text LIKE '%/finance/config%';
