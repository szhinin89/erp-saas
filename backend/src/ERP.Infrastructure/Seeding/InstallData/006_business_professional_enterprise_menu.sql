-- ============================================================================
-- 006_business_professional_enterprise_menu.sql
-- Copia el menu_config del plan Starter a los planes Business, Professional y
-- Enterprise cuando éstos aún no tienen configuración de menú (menu_config IS NULL).
--
-- Contexto: SaasPlansBootstrap.cs inserta Business/Professional/Enterprise sin
-- menu_config (null). La plataforma cae al fallback de ui_nav_* globales, lo
-- cual es correcto para producción, pero deja el Menu Builder sin punto de
-- partida para personalizar por plan.
--
-- Idempotente: solo actualiza filas donde menu_config IS NULL.
-- ============================================================================

UPDATE commercial_plans AS target
SET    menu_config = source.menu_config
FROM   commercial_plans AS source
WHERE  source.code = 'starter'
  AND  source.menu_config IS NOT NULL
  AND  target.code IN ('business', 'professional', 'enterprise')
  AND  target.menu_config IS NULL;
