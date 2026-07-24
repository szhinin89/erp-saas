# SRI — Nota de Crédito

Archivos oficiales del SRI para el comprobante Nota de Crédito, descargados de
`https://www.sri.gob.ec/facturacion-electronica` (paquete "Esquemas XSD Y XML Tipo de
documento Nota de Crédito – Versión 1.0.0 – 1.1.0").

## Archivos

| Archivo | Versión | Tipo |
|---|---|---|
| `NotaCredito_V1.0.0.xsd` | 1.0.0 | Esquema XSD |
| `NotaCredito_V1.1.0.xsd` | 1.1.0 | Esquema XSD |
| `Examples/NotaCredito_V1.0.0.xml` | 1.0.0 | XML de ejemplo oficial |
| `Examples/NotaCredito_V1.1.0.xml` | 1.1.0 | XML de ejemplo oficial |

## Comprobante asociado

Nota de Crédito (codDoc `04`).

## Dependencias

Ambos XSD declaran `<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
schemaLocation="xmldsig-core-schema.xsd"/>` — dependen del esquema de firma XML
estándar W3C (ver `../Common/README.md`, pendiente de incorporar).

## Hallazgo de auditoría — versión superada existente en el portal SRI

El portal SRI también publica un paquete separado, más antiguo ("Esquemas XSD de notas
de crédito y débito con el campo Devolución IVA", diciembre 2020, `XSD.zip`), con
archivos `NOTA_CREDITO-1.0.0.xsd`/`NOTA_CREDITO-1.1.0.xsd`. Se comparó su contenido
contra los archivos aquí incorporados: la versión de diciembre 2020 **ya no incluye**
los elementos `agenteRetencion`/`contribuyenteRimpe` (agregados posteriormente por la
normativa RIMPE) presentes en `NotaCredito_V1.0.0.xsd`/`NotaCredito_V1.1.0.xsd` — es
decir, el paquete de diciembre 2020 quedó superado por el paquete vigente que sí se
incorporó aquí. No se duplicó el archivo antiguo porque comparte el mismo nombre de
versión (1.0.0/1.1.0) que el vigente pero con contenido distinto y desactualizado —
guardarlo con el mismo nombre generaría ambigüedad, no historial real de versiones.
Queda documentado aquí como referencia; si se necesita para algún caso de compatibilidad
retroactiva, debe evaluarse explícitamente antes de incorporarlo.

## Resolución (ver `../manifest.json` y `../README.md`)

La entrada `"CreditNote"` de `manifest.json` (clave = nombre del enum
`ElectronicDocumentType.CreditNote`, no el nombre de esta carpeta) lista ambas versiones
con su dependencia común. `activeVersion: null` — todavía no hay validador que la
consuma.

## Propósito dentro del ERP

Sin provider/builder implementado todavía en `ElectronicDocuments` (módulo Sales solo
emite Factura). Estos archivos quedan disponibles para cuando se implemente el
comprobante Nota de Crédito en una fase futura.

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
