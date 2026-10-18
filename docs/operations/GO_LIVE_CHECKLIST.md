# Checklist de puesta en marcha — Empresa en producción

> Nivel 3 (documentación operativa). Checklist a completar **antes del primer día de operación real**
> de una empresa. No modifica código ni datos: es una guía de verificación y de decisiones del cliente.
> Complementa [`docs/deployment/README.md`](../deployment/README.md) y
> [`BACKUP_RESTORE.md`](BACKUP_RESTORE.md). Operación diaria: [`DAILY_RUNBOOK.md`](DAILY_RUNBOOK.md).
> Precedente histórico (piloto Sumak, 2026-08-08):
> [`docs/archive/operations/SUMAK_PILOT_CHECKLIST-2026-08-08.md`](../archive/operations/SUMAK_PILOT_CHECKLIST-2026-08-08.md).

Copiar este checklist por empresa y registrar fecha, responsable y evidencia de cada punto.
Base del entorno: `erp_zh_prod`.

---

## 1. Infraestructura

- [ ] Stack app `healthy`: `.\scripts\app-stack.ps1 -Action Health` (API, Worker, Frontend; PostgreSQL y Redis existentes).
- [ ] `/health/live` y `/health/ready` → `Healthy` (database, redis, sri-external).
- [ ] Worker: cuatro servidores Hangfire activos y siete recurring jobs (ver [deployment § Verificación](../deployment/README.md#verificación-y-supervisión)).
- [ ] Backup inicial ejecutado: `.\scripts\backup-restore.ps1 -Action Backup -Database erp_zh_prod`.
- [ ] Restore drill aislado probado: `.\scripts\backup-restore.ps1 -Action RestoreCheck -Database erp_zh_prod`.
- [ ] Espacio en disco del host verificado.
- [ ] Cadencia de backup recurrente definida y copia externa fuera del host.

## 2. Seguridad

- [ ] `.env.app` resguardado en gestor de secretos (fuera del repo y de `backups/`) — ver
      [`BACKUP_RESTORE.md` § 3](BACKUP_RESTORE.md#3-cómo-guardar-envapp-de-forma-segura).
- [ ] Certificado `.p12` presente en FileStorage persistente (`/app/data/files/certificates/<companyId>/`).
- [ ] Usuarios operativos creados con perfil de acceso limitado; el administrador inicial no opera a diario.
- [ ] Password inicial del administrador rotada y política de rotación definida.

## 3. Empresa / SRI

- [ ] RUC, razón social y régimen tributario configurados (no quedan identificadores temporales `TMP-`).
- [ ] Establecimiento principal y punto de emisión por defecto activos.
- [ ] Ambiente SRI confirmado (Pruebas o Producción) y WSDL correspondiente.
- [ ] Certificado válido y cargado; `GET /api/companies/operational-readiness` sin faltantes.
- [ ] Factura electrónica de prueba autorizada en ambiente Pruebas.
- [ ] **Decisión del cliente**: fecha/criterio de paso de Pruebas a Producción SRI. No tocar SRI
      Producción sin autorización explícita.

## 4. Caja

- [ ] Cajas físicas definidas y activas.
- [ ] Responsable de caja por turno definido.
- [ ] Saldo inicial real acordado con el cliente.
- [ ] Apertura y cierre de caja probados como flujo operativo.
- [ ] Sin sesiones de caja abiertas residuales de pruebas.

## 5. Inventario

- [ ] Catálogo real cargado; ítems de prueba desactivados (soft delete, nunca borrados).
- [ ] Precios base revisados (sin `base_sale_price = 0` no intencional).
- [ ] IVA configurado por ítem desde catálogo SRI.
- [ ] Inventario inicial cargado y conteo físico conciliado.

## 6. Clientes / Proveedores

- [ ] `Consumidor Final` configurado.
- [ ] Al menos una venta probada con cliente identificado (RUC/Cédula).
- [ ] Proveedores principales registrados.

## 7. Ventas

- [ ] Venta simple y cobro probados con cada método de pago habilitado.
- [ ] Todos los métodos de pago con forma de pago SRI mapeada.
- [ ] RIDE generado y descargable.
- [ ] Ventas y documentos de prueba identificados y anulados (nunca borrados) antes de reportes reales.

## 8. RIDE / Impresión

- [ ] RIDE (PDF) generándose en el stack Docker.
- [ ] Impresión física validada en sitio (impresora térmica/A4, Print Agent).

## 9. Contingencias

Ver tabla de respuestas en [`DAILY_RUNBOOK.md` § Contingencias](DAILY_RUNBOOK.md#contingencias).

- [ ] Procedimiento acordado con el cliente para facturas mal emitidas (nota de crédito vs. anulación).
- [ ] Canal de escalamiento si el sistema no levanta o el SRI no responde en horario de operación.
