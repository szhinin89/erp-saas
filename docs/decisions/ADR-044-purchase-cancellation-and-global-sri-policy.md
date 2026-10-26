# ADR-044 — Anulación de compra y política fiscal global

Estado: Accepted. Decisión arquitectónica vigente; no es especificación funcional.

## Decisión

Cancel representa una operación que puede deshacerse íntegramente: una salida efectiva
posterior del mismo recurso bloquea Cancel permanentemente aunque luego se revierta;
una entrada posterior sola no lo bloquea. La norma de plazo fiscal se representa en el
catálogo global versionado y se resuelve por fecha de emisión.

## Justificación

Con promedio ponderado no existe identidad física de unidades de cada compra. Comprobar
saldo actual o cambiar Cancel a costo promedio ocultaría una operación consumida.
Versionar la norma evita constantes fiscales por handler o empresa.

## Responsabilidad y límites

Cancel ERP no anula la factura del proveedor en SRI. Retenciones conserva ADR-036;
GLOBAL conserva ADR-037. No añade calendario oficial de feriados ni ventana interna
Company; el plazo ordinario informativo no se convierte en bloqueo fiscal automático.

## SSOT y dependencias

Comportamiento: [purchases.md](../architecture/purchases.md).
Integración contable: [Accounting](../architecture/accounting.md).
Instalación vigente: [GLOBAL](../architecture/architecture.md#global-immutable-schema-global).
Gates/cierres: [STATUS](../../STATUS.md). PostgreSQL real es obligatorio para validar persistencia,
concurrencia y aislamiento; este ADR no acredita nuevas ejecuciones.
