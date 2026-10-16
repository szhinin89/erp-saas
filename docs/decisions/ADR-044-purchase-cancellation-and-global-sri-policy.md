# ADR-044 — Anulación completa de compra y política global SRI

Estado: Accepted. Fecha: 2026-10-16. Clasificación: BUG REAL / EXTEND.
Decisión funcional del responsable: cualquier salida efectiva posterior bloquea Cancel
definitivamente, aunque posteriormente se revierta; una entrada posterior sola no bloquea.
Complementa ADR-027, ADR-034, ADR-036, ADR-037, ADR-040 y ADR-041.

## Contexto y evidencia

Compra A = 10 × $2; compra B = 1 × $10; salida de 8 unidades al promedio.
Quedan 3 unidades con aproximadamente $8,18 de valor. El reverso histórico de B
intenta retirar $10 y produce `Inventory cost cannot be negative.` El rollback
protege persistencia, pero faltaba una precondición funcional. La reproducción
PostgreSQL ejecuta esa salida histórica antes de comprobar el bloqueo del handler.

## Operaciones distintas

| Operación | Sentido y efecto |
|---|---|
| Discard | Draft → Discarded; sin reversos operativos/financieros. |
| Cancel | Confirmed → Cancelled por el flujo único existente; operación que no ocurrió y cuyas precondiciones permiten revertir. |
| Devolución | Documento posterior de salida real al proveedor, con su propia trazabilidad y valoración aprobada en ADR-041. |
| NC por devolución | Evidencia comercial/fiscal asociada a la devolución; no duplica sus efectos. |
| NC por descuento | Ajuste comercial/financiero, sin simular devolución física. |

Estos otros flujos permanecen CLOSED y no se rediseñan.

## Elegibilidad y movimientos posteriores

`PurchaseCancellationEligibility` concentra estado, pagos, devoluciones autorizadas,
crédito de proveedor y créditos/NC aplicados a CxP, y el nuevo gate de inventario.
API y finalización SRI siguen pasando por `CancelPurchaseHandler.ExecuteAsync`.
El gate fiscal conserva su única implementación en `IRetentionCanceller` (ADR-036).

Las entradas originales se identifican mediante SourceDocId + SourceDocType
PurchaseInvoice + MovementType PurchaseEntry. Por Tenant + Company + Item + Warehouse,
se toma la secuencia inicial de esas entradas y se busca cualquier movimiento persistido
con `Quantity < 0` y `SequenceNumber` posterior. No se usan CurrentStock, fecha comercial
retroactiva, etiquetas de referencia ni una lista cerrada de tipos de salida.
Las salidas canceladas conservan su movimiento original: la entrada compensatoria
no rehabilita Cancel. No se intenta atribuir unidades físicas a una compra específica.
Es una regla conservadora para proteger integridad de inventario y contabilidad:
con promedio ponderado no existe identidad física de compra ni evidencia suficiente
para reconstruir el estado causal previo después de una reversa.

Antes de consultar las salidas se adquieren los mismos advisory locks PostgreSQL de
AppendMovementAsync, ordenados por Item + Warehouse y retenidos hasta commit/rollback.
El control y el reverso comparten transacción: una salida concurrente confirmada antes
del control se observa después de esperar su lock. No hay reversos previos al gate.

Código público registrado en ApiResponseCodes + MessageCatalog (ADR-027):
`PURCHASE_CANCELLATION_SUBSEQUENT_INVENTORY_EXIT`. Mensaje orienta a utilizar
devolución o nota de crédito según corresponda.

## Norma SRI, vigencias y SSOT

Se extiende el patrón global ADR-037: tablas tipadas concepto + versión,
HasData materializado por migración EF, sin tenant/company, sin CRUD/configuración
operativa paralela. `ISriCatalogResolver` / `SriCatalogResolver` ya existentes resuelven
exactamente una versión por fecha de emisión; ausencia o solapamiento falla cerrado.
FK a `global.sri_normative_source`, fechas inclusivas y unicidad de inicio por concepto.

Fuentes oficiales verificadas:

- [Guía SRI de anulación, art. 7 NAC-DGERCGC16-00000092](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/c97242e6-c271-4eb8-8f6a-f687313118ba/Guia%20para%20contribuyentes%20de%20anulaci%C3%B3n%20de%20comprobantes%20electr%C3%B3nicos.pdf): historia de 90 días posteriores a emisión, conservada desde 2016-02-22 hasta 2025-07-31.
- [NAC-DGERCGC25-00000017](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar?id=e98fc8a6-299e-4ea9-8de7-2f6c70dbb4f5&nombre=NAC-DGERCGC25-00000017.pdf), art. 1 a) y transitoria primera: reforma la resolución 14; día 7 del mes siguiente para documentos emitidos desde 2025-08-01. El plazo admite extensión por descanso obligatorio o feriado nacional/local según el art. 3 reformado. Excepción ISD fuera del dominio IVA/Renta.

El texto original del día 10 se reformó antes de su aplicabilidad; no se inventa una
ventana de emisión en que ese límite haya regido. Su antecedente queda documentado,
sin registrar una vigencia efectiva ficticia.

`global.sri_annulment_policy` + `global.sri_annulment_policy_version` representan día N
del mes siguiente o días posteriores a emisión, inicio/fin y extensión al día hábil.
La migración `SriAnnulmentPolicyVersioning` es necesaria: las tablas existentes de
tarifas/retenciones no representaban plazos de anulación. No se modifica la baseline.
Se elimina la constante fiscal de RetentionAnnulmentDeadline; el único dato vigente
se siembra en la versión global. La solicitud guarda OrdinaryDeadline como snapshot:
ningún dato persistido anterior se recalcula. UI muestra esa fecha sin un día fijo.

## Emitidos frente a recibidos

La factura recibida es emitida por el proveedor: Cancel ERP no anula ese comprobante
en SRI ni atribuye al comprador potestad de emisor. No se aplica el plazo de emisión
propia directamente a esa factura como si fuera una factura emitida por la empresa.
La retención que nuestra empresa sí emite conserva el proceso asistido de ADR-036:
si está autorizada, se exige confirmación fiscal ANULADO antes de reversos locales.
Solicitar el trámite no equivale a anular; no se añade ningún envío ni ruta SRI.

## Impactos y alternativas rechazadas

Inventario: Cancel válido conserva cantidades base y reverso al costo histórico;
Cancel bloqueado no agrega movimientos ni cambia cantidad/promedio/valoración.
CxP: no se cancela ni altera ante bloqueo; se conservan pagos/créditos/retenciones.
Contabilidad: se conserva el traductor y el reverso existente. El gate no crea eventos,
asientos ni reversos parciales; las pruebas comparan persistencia antes/después.

Se rechazan: costo promedio como parche de Cancel (oculta una operación ya consumida
y cambia el significado del reverso); atribución FIFO/LIFO o reconstrucción de cadenas;
rehabilitar tras reversa; bloquear por entradas solamente; comprobar saldo actual;
hardcode fiscal en handler/UI; configuración por empresa paralela; alterar devoluciones/NC.

## STRUCTURAL DEBT

- No existe calendario oficial de feriados nacionales/locales. La versión representa
  extensión y calcula con calendario explícito, pero producción conserva el plazo
  **ordinario informativo** y el aviso de verificar el efectivo en SRI en Línea.
  No se inventan feriados ni se rechaza automáticamente un trámite por ese plazo
  informativo. El gate de confirmación fiscal existente sigue siendo obligatorio.
- No se encontró mecanismo de ventana interna de anulación por Company. No se crea
  en esta fase. Una futura restricción interna solo podrá acortar el límite legal.

Validación y comandos: [entrega focal](../agent/PURCHASE_CANCELLATION_DELIVERY.md).
