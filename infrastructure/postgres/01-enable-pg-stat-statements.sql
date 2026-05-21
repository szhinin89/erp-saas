-- Paso 4.1 (PostgreSQL): habilitar extensión y carga de estadísticas de consultas.
-- Requiere privilegios de superusuario o rol con permiso CREATE en la base.

CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

-- Parámetros típicos (revisar postgresql.conf o ALTER SYSTEM en entorno administrado):
-- shared_preload_libraries = 'pg_stat_statements'
-- pg_stat_statements.track = all
