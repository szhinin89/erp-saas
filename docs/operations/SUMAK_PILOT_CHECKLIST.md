# Checklist operativo — Piloto Sumak (PROD-01J)

> Nivel 3 (documentación operativa). Complementa [`docs/DOCKER_LOCAL_PROD.md`](../DOCKER_LOCAL_PROD.md) y
> [`docs/BACKUP_RESTORE_LOCALPROD.md`](../BACKUP_RESTORE_LOCALPROD.md) — no los reemplaza.
> Ver también el runbook diario: [`SUMAK_DAILY_RUNBOOK.md`](SUMAK_DAILY_RUNBOOK.md).

Este documento es el checklist a completar **antes del primer día de operación real** del piloto
Sumak. No modifica código ni datos — es una guía de verificación y de decisiones pendientes.

Snapshot de referencia (inventario tomado 2026-08-08, ver reporte PROD-01J): tenant `Principal`
(`cd6ba1e0-…`), empresa `ZH TECH` (RUC `0302126842001`), ambiente SRI **Pruebas**.

---

## 1. Infraestructura

- [x] Docker `up` y `healthy` (`erp-api-localprod`, `erp-frontend-localprod`, `postgreszh`, `erp-saas-redis`).
- [x] `/health/live` → `Healthy`.
- [x] `/health/ready` → `Healthy` (database, membership-consistency, masterdata-sync,
      masterdata-reconciliation, redis, sri-external).
- [x] Backup reciente ejecutado (`backups/localprod/20260808-143536/` con `manifest.json` +
      `SHA256SUMS.txt`).
- [x] Restore drill aislado probado (PROD-01I.1, `scripts/restore-check-localprod.ps1`).
- [ ] Espacio en disco del host verificado (no automatizado — revisar manualmente antes del piloto).
- [ ] Definir cadencia de backup recurrente para operación real (hoy es manual bajo demanda).

## 2. Seguridad

- [ ] `.env.docker.local` resguardado en gestor de secretos (fuera del repo, fuera de `backups/`) —
      ver [`BACKUP_RESTORE_LOCALPROD.md` § 3](../BACKUP_RESTORE_LOCALPROD.md#3-cómo-guardar-envdockerlocal-de-forma-segura).
- [x] Certificado `.p12` presente en FileStorage persistente
      (`/app/data/files/certificates/<companyId>/certificate.p12`, subido 2026-08-08).
- [ ] **Usuarios reales creados** — hoy solo existe `sadmin` (Admin, sin `require_password_reset`).
      Bloqueante para operación diaria (ver Tarea 6).
- [ ] Password inicial de `sadmin` rotada / política de rotación definida para el piloto.
- [ ] **No usar `sadmin` para operación diaria** — crear usuario(s) operativo(s) con perfil de acceso
      limitado (existe un perfil `DataEntry` creado pero sin membresías asignadas todavía).

## 3. Empresa / SRI

- [x] RUC configurado: `0302126842001` (empresa `ZH TECH`, tenant `Principal`).
- [x] Razón social: `ZH TECH`.
- [x] Régimen tributario: código `01`.
- [x] Establecimiento principal: `001 — Establecimiento Principal` (activo, `is_main=true`).
- [x] Punto de emisión: `001 — Punto de Emisión Principal` (`emission_type=1`, `is_default=true`).
- [x] Ambiente SRI confirmado: **Pruebas** (`environment=1`, WSDL `celcer.sri.gob.ec`).
- [x] Certificado válido y cargado (`sri_settings.cert_uploaded_at_utc = 2026-08-08`).
- [x] `validate SRI` OK — factura `001-001-000000006` autorizada en Pruebas el 2026-08-08
      (clave de acceso `0808202601030212684200110010010000000068198457310`).
- [ ] **Decisión del usuario**: fecha/criterio de corte para pasar de ambiente Pruebas a Producción SRI.
      No tocar producción SRI sin autorización explícita (regla dura de este bloque).

## 4. Caja

- [x] Caja física definida: `001 — Caja Principal` (activa).
- [ ] Usuario responsable de caja definido (hoy la única sesión abierta pertenece a `sadmin`).
- [ ] Saldo inicial real acordado con el cliente (las sesiones registradas usan `opening_amount = 0.00`
      — válido para pruebas, revisar antes de operación real).
- ⚠️ **Hay una sesión de caja abierta sin cerrar** desde 2026-08-04 (`cash_sessions.status = 1`,
      sin `closed_at`). Cerrarla o confirmar que es intencional antes del piloto — ver Tarea 6.
- [ ] Apertura de caja probada como flujo operativo (no solo vía datos de prueba).
- [ ] Cierre de caja probado como flujo operativo.

## 5. Inventario

- ⚠️ Solo **7 productos** cargados, de los cuales **2 son ítems E2E de prueba**
  (`E2E97820831`, `E2E97883024`) — deben limpiarse/desactivarse antes del piloto, no borrarse
  (soft delete, regla global del proyecto).
- ⚠️ De los 5 productos "reales" (cervezas), **3 tienen `base_sale_price = 0.00`**
  (`8409`, `22642`, `17115`) — deben revisarse precios antes de vender.
- ⚠️ Solo **2 de 7 productos tienen registro en `current_stocks`** (`84d92337… Bodega Principal`) —
  el resto no tiene stock inicial cargado.
- [x] IVA configurado por ítem (`sale_vat_code = 4` en los 5 productos reales — código SRI, no
      hardcodeado, cumple regla global).
- [ ] Catálogo de productos real completo pendiente de carga (decisión/tarea del usuario).
- [ ] Stock inicial real pendiente de carga y conteo físico.

## 6. Clientes / Proveedores

- [x] Cliente `Consumidor Final` (`07 / 9999999999999`) configurado.
- [ ] **No hay clientes con RUC/Cédula reales probados en una venta** — todas las facturas emitidas
      hasta ahora son a `Consumidor Final`. Probar al menos un flujo con cliente identificado antes
      del piloto.
- [x] 3 proveedores (`master_business_partners`, role Supplier) ya registrados (`Zhinin`,
      `QUALA ECUADOR S.A.`, `DINADEC S.A.`).

## 7. Ventas

- [x] Venta simple probada (6 facturas de prueba emitidas 2026-08-03 a 2026-08-08).
- [x] Factura electrónica de prueba autorizada por el SRI (Pruebas) — `001-001-000000006`.
- [x] RIDE generado y disponible (`ride_pdf_document`, 1 registro).
- ⚠️ Existe **1 documento fallido**: `001-001-000000005` — error
  *"La factura no tiene una forma de pago SRI asignada."* Causa raíz probable: falta mapeo de
  forma de pago SRI en `payment_methods` para ese método. Reportar como bloqueante funcional a
  revisar (no corregido en este bloque — ver Tarea 6).
- ⚠️ Existe una factura `DRAFT-dc5f1150` sin número asignado — confirmar si es residual de prueba.
- [ ] **Todas las ventas actuales son de prueba/SMOKE** — deben identificarse y excluirse (o
      anularse según la regla de "no borrar", solo anular) antes de reportes reales al cliente.

## 8. RIDE / Impresión

- [x] RIDE (PDF) generándose correctamente en Docker (PROD-01H cerrado — SkiaSharp).
- [ ] Validar impresión física en el entorno real de Sumak (impresora térmica/A4 — no probado en
      este checklist, es responsabilidad del piloto en sitio).

## 9. Backup / Restore

- [x] `scripts/backup-localprod.ps1` probado y funcional.
- [x] `scripts/restore-check-localprod.ps1` (drill aislado) probado — PROD-01I.1.
- [ ] Definir backup **antes de iniciar el piloto** (ejecutar el día 0).
- [ ] Definir copia externa del backup (fuera del host) — hoy los backups quedan solo en
      `backups/localprod/` local, ignorados por Git.
- [ ] Definir cadencia de `restore-check` (recomendado: semanal o antes de cualquier cambio grande).

## 10. Contingencias

Ver tabla de respuestas en [`SUMAK_DAILY_RUNBOOK.md` § Contingencias](SUMAK_DAILY_RUNBOOK.md#contingencias).

- [ ] Confirmar con el cliente el procedimiento aceptado para facturas mal emitidas (nota de
      crédito vs. anulación — depende de si ya fue autorizada por el SRI).
- [ ] Confirmar canal de escalamiento si el sistema no levanta o el SRI no responde durante horario
      de operación.

---

## Resumen — qué falta antes de operación real

Ver bloqueantes y recomendaciones detallados en el reporte PROD-01J (Tarea 6). En síntesis, lo
marcado con ⚠️ o `[ ]` arriba es lo pendiente; lo marcado `[x]` ya está verificado en el entorno
localprod al 2026-08-08.
