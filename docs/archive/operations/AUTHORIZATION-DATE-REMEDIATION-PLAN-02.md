# Plan de remediación — `AuthorizationDate` histórico (DATE-02C)

**Estado:** PROPUESTO — **no ejecutado**. **Ticket:** ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 · **ADR:** [ADR-034](../decisions/ADR-034-temporal-contract-single-source.md)
**Separación:** la migración de esquema del ticket no toca estas filas. Esto es remediación de datos, con su propia aprobación.

## Problema

Antes del ticket, dos caminos guardaban una **hora local de Ecuador etiquetada como UTC** (`SpecifyKind(Utc)`), es decir, el valor almacenado quedó **5 h antes** del instante real:

1. `PurchaseInvoiceTxtParser` — `FECHA_AUTORIZACION` del TXT SRI (hora Ecuador, sin offset).
2. Formularios con `datetime-local` (Compras) que enviaban la hora sin zona.

Además, Gastos podía **sumar +5 h por cada edición** (zona del navegador al guardar, `getUTC*()` al cargar).

El código ya no produce estos valores (ver ADR-034). Las filas históricas siguen como estaban.

## Medición (BD dev `dberpsaas`, 2026-09-25, solo lectura)

| Tabla | Filas con fecha | Clasificación | Diagnóstico |
|---|---|---|---|
| `purchase_reception_documents` | 151 | 85 con XML descargado (`xml_downloaded_at` no nulo) | `AttachSriAuthorization` sobrescribió con `fechaAutorizacion` del SOAP (offset explícito → UTC real). **Correctas.** |
| | | 66 solo TXT (`xml_downloaded_at` nulo) | Hora Ecuador etiquetada UTC. **Probablemente 5 h antes.** |
| `purchase_invoices` | 4 | 4 enlazadas (por `access_key`) a recepciones con XML | Igual a la recepción truncada al minuto (paso por `datetime-local`). Instante correcto ± segundos. |
| `purchase_credit_notes` | 5 | 5 enlazadas a recepción con XML, valor idéntico | Correctas. |
| `expense_documents` | 3 | 3 enlazados a recepción con XML, valor idéntico | Correctas (no hubo drift por edición). |
| `electronic_documents` | 0 | — | — |

Consultas usadas (repetibles en cualquier ambiente):

```sql
-- Recepciones por procedencia
SELECT (xml_downloaded_at IS NOT NULL) AS xml_descargado, count(*)
  FROM purchase_reception_documents WHERE authorization_date IS NOT NULL GROUP BY 1;

-- Compras/NC/Gastos vs. su recepción
SELECT CASE WHEN r.id IS NULL THEN 'manual/sin_recepcion'
            WHEN r.xml_downloaded_at IS NOT NULL THEN 'recepcion_con_xml'
            ELSE 'recepcion_txt' END AS origen,
       count(*), count(*) FILTER (WHERE e.authorization_date = r.authorization_date) AS igual
  FROM expense_documents e
  LEFT JOIN purchase_reception_documents r ON r.id = e.reception_document_id
 WHERE e.authorization_date IS NOT NULL GROUP BY 1;
-- (misma forma para purchase_credit_notes vía reception_document_id,
--  y purchase_invoices vía access_key + tenant_id)
```

## Por qué no se autocorrige

- No hay columna que registre la **procedencia** del valor. "Solo TXT" es una inferencia (`xml_downloaded_at` nulo), no un hecho registrado.
- La corrección depende del `Company.Timezone` vigente **cuando se importó**, que puede haber cambiado.
- Documentos manuales o editados en Gastos pudieron acumular +5 h × N ediciones: N no es recuperable.
- Corregir un valor que ya era correcto lo dañaría. La regla del ticket prohíbe adivinar.

## Remediación propuesta (por pasos, cada uno con aprobación)

1. **Auto-sanación sin tocar datos (recomendado primero).** Descargar el XML/autorización SRI de las 66 recepciones solo-TXT. `AttachSriAuthorization` sobrescribe con la fecha real del SOAP (UTC correcto). Cero inferencia: la fuente oficial corrige el dato.
2. **Corrección explícita solo si el paso 1 no aplica** (XML no disponible). Por empresa, previa confirmación de negocio de que su `Timezone` no cambió desde la importación:

   ```sql
   -- DRY-RUN: listar candidatos y el valor corregido propuesto
   SELECT r.id, r.access_key,
          r.authorization_date AS actual,
          (r.authorization_date AT TIME ZONE 'UTC') AT TIME ZONE c.timezone AS propuesto
     FROM purchase_reception_documents r
     JOIN companies c ON c.id = r.company_id AND c.tenant_id = r.tenant_id
    WHERE r.xml_downloaded_at IS NULL AND r.authorization_date IS NOT NULL
      AND r.company_id = :company_id;
   ```

   Ejecución (solo tras revisar el dry-run): respaldo previo en tabla `_remediation_auth_date_02` (`id`, `valor_anterior`), `UPDATE` en una transacción con el mismo `WHERE`, verificación de conteo, y `COMMIT`.
   **Reversión:** `UPDATE ... SET authorization_date = b.valor_anterior FROM _remediation_auth_date_02 b WHERE b.id = r.id`.
3. **Compras / NC / Gastos** heredan el valor de su recepción. Si una recepción se corrige en el paso 2, recalcular las compras enlazadas desde la recepción, nunca sumando horas a ciegas. En el dataset medido no hay ninguna enlazada a una recepción solo-TXT.
4. **Gastos con drift acumulado**: no hay fórmula segura. Revisión manual contra el XML/RIDE del proveedor.

## Impacto si no se remedia

Solo la **presentación** de la hora de autorización de esas 66 recepciones: aparecerá 5 h antes. `AuthorizationDate` no alimenta contabilidad, secuencias, vigencias ni el XML emitido. La fecha de negocio (`IssueDate`, `DateOnly`) no está afectada.
