-- Backfill script for MultiCompanyOnboarding migration.
-- Run AFTER applying the migration, BEFORE allowing user traffic.
-- Safe to run multiple times (idempotent WHERE clause).

-- 1. Assign company_id in sri_settings to the first active company per subscriber.
--    Existing rows got the empty-GUID default from the migration.
UPDATE sri_settings s
SET    company_id = c.id
FROM   companies c
WHERE  c.subscriber_id = s.subscriber_id
  AND  s.company_id    = '00000000-0000-0000-0000-000000000000'
  AND  c.is_active     = true;

-- 2. (Optional) After confirming no rows remain with empty GUID, remove the DB default:
-- ALTER TABLE sri_settings ALTER COLUMN company_id DROP DEFAULT;

-- 3. branches.company_id is intentionally left nullable; backfill per-company as needed.
