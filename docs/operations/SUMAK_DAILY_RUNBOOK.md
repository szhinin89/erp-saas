# Runbook diario — Piloto Sumak (PROD-01J)

> Nivel 3 (documentación operativa). Guía corta para el operador diario y para un agente IA que
> asista en mantenimiento. Ver checklist previo al primer día:
> [`SUMAK_PILOT_CHECKLIST.md`](SUMAK_PILOT_CHECKLIST.md).

---

## Inicio del día

1. Verificar salud del sistema:
   ```powershell
   curl.exe -s http://localhost:5003/health/live
   curl.exe -s http://localhost:5003/health/ready
   ```
   Ambos deben responder `"status":"Healthy"`. Si no, ver [Contingencias](#contingencias).
2. Abrir caja (`Caja Principal`) con el usuario responsable del turno.
3. Confirmar usuario y sucursal correctos en la sesión (no operar con `sadmin`).
4. Si se usará impresión física: verificar impresora encendida y con papel. Si se usará descarga
   de RIDE en PDF, confirmar que el navegador/estación puede descargar archivos.
5. Si se emitirá factura electrónica: confirmar conectividad a internet y que `sri-external` en
   `/health/ready` está `Healthy`.

## Durante el día

- **Ventas**: registrar cada venta con el cliente correcto (`Consumidor Final` si no se identifica
  al comprador, o cliente con RUC/Cédula si aplica).
- **Cobros**: seleccionar el método de pago correcto (`Efectivo`, `Tarjeta de Crédito`,
  `Transferencia Bancaria`, `Cheque`, `Crédito`). Un método de pago sin forma de pago SRI asociada
  hará fallar la autorización electrónica (ver error conocido abajo).
- **Consulta de stock**: verificar disponibilidad antes de prometer venta si el producto no tiene
  stock cargado.
- **Facturación electrónica**: se emite automáticamente al confirmar la venta. Si falla, el
  documento queda en estado de reintento — no reintentar manualmente sin revisar el motivo del
  error.
- **Descarga de RIDE**: disponible una vez el documento esté autorizado por el SRI.

### Errores frecuentes conocidos

| Error | Causa | Acción |
|---|---|---|
| "La factura no tiene una forma de pago SRI asignada." | El método de pago usado en la venta no tiene mapeado un código de forma de pago SRI. | No reintentar a ciegas. Reportar a soporte técnico para revisar el mapeo de métodos de pago (`payment_methods`) antes de reintentar. |
| SRI no responde / timeout | Ambiente SRI (Pruebas o Producción) caído o sin internet. | Ver [Contingencias](#contingencias) — la venta puede completarse localmente; el documento electrónico queda pendiente de reintento automático. |
| RIDE no descarga | Documento aún no autorizado, o fallo del renderizador. | Confirmar que el documento tiene `authorization_number`. Si el documento está autorizado y el RIDE no genera, reportar como bloqueo técnico. |

## Cierre del día

1. Revisar ventas del día (totales, medios de pago).
2. Revisar documentos electrónicos fallidos o pendientes de reintento — no dejar pendientes de un
   día para otro sin registrar el motivo.
3. Cerrar caja (`Caja Principal`), verificando el saldo esperado vs. el contado.
4. Ejecutar backup:
   ```powershell
   cd C:\ProyectCursor\erp-saas
   .\scripts\backup-localprod.ps1
   ```
5. Guardar copia del backup fuera del servidor (USB, almacenamiento externo, o gestor de backups
   del cliente) — hoy `backups/localprod/` es solo local.

---

## Contingencias

| Situación | Qué hacer |
|---|---|
| **SRI no responde** | Continuar operando: la venta y el cobro se registran igual. El documento electrónico queda en cola de reintento automático. No forzar reintentos manuales repetidos. Informar al cliente que el RIDE se emitirá cuando el SRI responda. |
| **No hay internet** | Igual que arriba — operar localmente. Facturación electrónica y validaciones SRI quedarán pendientes hasta recuperar conexión. |
| **Falla el RIDE (PDF)** | La venta y la autorización SRI no dependen del PDF. Reintentar la descarga; si persiste, reportar como bloqueo técnico — no es motivo para anular la venta. |
| **El sistema no levanta** | Verificar Docker: `docker compose -f docker-compose.localprod.yml --env-file .env.docker.local ps`. Si los contenedores no están `healthy`, escalar a soporte técnico. No reiniciar con `down -v` ni tocar volúmenes. |
| **Se equivocan en una factura** | Si **no** fue autorizada por el SRI: se puede corregir/anular el borrador. Si **ya** fue autorizada: no se puede eliminar — requiere nota de crédito según el procedimiento fiscal correspondiente. Nunca borrar el registro (regla global del proyecto: solo anulación/soft delete). |
| **Falta stock** | No forzar la venta si el sistema indica stock insuficiente. Registrar el faltante y reportarlo para reposición. |

---

## Guía para mantenimiento asistido por IA

Para cualquier agente (Claude Code u otro) que asista en mantenimiento de este entorno durante el
piloto:

**Siempre al empezar:**
- `git status --short` antes de cualquier cambio.
- No tocar datos reales sin backup previo y confirmación explícita del usuario.
- No imprimir secretos (passwords, certificados, JWT, cookies, `.env.docker.local`).
- No ejecutar `docker compose ... down -v`.
- No resetear la base de datos.
- No borrar volúmenes Docker.
- No truncar tablas ni hacer `DELETE` físico — usar solo anulación/`IsActive=false`.

**Antes de cualquier cambio en el entorno del piloto:**
```powershell
cd C:\ProyectCursor\erp-saas
.\scripts\backup-localprod.ps1
.\scripts\restore-check-localprod.ps1
curl.exe -s http://localhost:5003/health/live
curl.exe -s http://localhost:5003/health/ready
```

**Después de cualquier cambio:**
- Repetir `health/live` y `health/ready`.
- Si hubo cambios de código o configuración: `npm run architecture:check` (frontend) y `dotnet test`
  (backend) según corresponda.
- Revisar `git diff` completo antes de proponer commit.

**Si el cambio toca SRI:**
- Confirmar explícitamente si el ambiente es Pruebas o Producción antes de actuar — **nunca tocar
  SRI Producción sin autorización explícita**.
- No imprimir certificado, password del certificado, ni el XML completo de un documento.
- Si se autoriza o reintenta un documento electrónico, validar que el RIDE se genera correctamente
  después.

**Si el cambio toca backup:**
- Validar con `restore-check-localprod.ps1` que el backup restaura correctamente antes de confiar
  en él.
- Nunca restaurar sobre `dberpsaas` (base real) sin confirmación explícita — usar siempre una base
  temporal descartable o el drill aislado.
