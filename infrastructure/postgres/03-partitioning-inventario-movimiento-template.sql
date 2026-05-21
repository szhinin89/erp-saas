-- Paso 4.4 (PLANTILLA): particionamiento por rango de fecha en PostgreSQL.
-- NO ejecutar en producción sin ventana de mantenimiento, prueba en copia y plan de migración.
--
-- Esquema real del ERP: tabla "inventario_movimientos" con columna de tiempo (p. ej. created_at / fecha).
-- Ajustar nombres de tabla y columna de partición al modelo desplegado.

/*
-- 1) Crear tabla hija particionada (ejemplo conceptual)
CREATE TABLE inventario_movimientos_partitioned (
    LIKE inventario_movimientos INCLUDING DEFAULTS INCLUDING CONSTRAINTS
) PARTITION BY RANGE (created_at);

-- 2) Crear particiones mensuales
CREATE TABLE inventario_movimientos_y2026m01 PARTITION OF inventario_movimientos_partitioned
    FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
-- ... repetir por mes ...

-- 3) Migración: INSERT ... SELECT en lotes, validar conteos, swap de nombres, recrear índices y FKs.
*/
