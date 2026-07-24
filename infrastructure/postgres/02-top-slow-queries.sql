-- Paso 4.1: consultas con mayor tiempo medio (ajustar LIMIT / orden según necesidad).
-- Útil para localizar kardex, reportes y listados pesados.

SELECT
    calls,
    round(mean_exec_time::numeric, 2) AS mean_time_ms,
    round(total_exec_time::numeric, 2) AS total_time_ms,
    left(query, 200) AS query_preview
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 20;
