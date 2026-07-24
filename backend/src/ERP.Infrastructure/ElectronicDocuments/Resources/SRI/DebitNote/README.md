# SRI — Nota de Débito

Archivos oficiales del SRI para el comprobante Nota de Débito, descargados de
`https://www.sri.gob.ec/facturacion-electronica` (paquete "Esquemas XSD Y XML Tipo de
documento Nota de Débito – Versión 1.0.0").

## Archivos

| Archivo | Versión | Tipo |
|---|---|---|
| `NotaDebito_V1.0.0.xsd` | 1.0.0 | Esquema XSD |
| `Examples/NotaDebito_V1.0.0.xml` | 1.0.0 | XML de ejemplo oficial |

## Comprobante asociado

Nota de Débito (codDoc `05`).

## Dependencias

El XSD declara `<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
schemaLocation="xmldsig-core-schema.xsd"/>` — depende del esquema de firma XML estándar
W3C (ver `../Common/README.md`, pendiente de incorporar).

## Hallazgo de auditoría — versión superada existente en el portal SRI

Mismo caso que `CreditNote/README.md`: el portal SRI también publica
`NOTA_DEBITO-1.0.0.xsd` dentro del paquete más antiguo "Devolución IVA" (diciembre
2020), que carece de los elementos `agenteRetencion`/`contribuyenteRimpe` presentes en
`NotaDebito_V1.0.0.xsd` (paquete vigente, incorporado aquí). No se duplicó por la misma
razón: mismo número de versión, contenido desactualizado.

## Resolución (ver `../manifest.json` y `../README.md`)

La entrada `"DebitNote"` de `manifest.json` (clave = nombre del enum
`ElectronicDocumentType.DebitNote`) lista esta versión con su dependencia común.
`activeVersion: null` — todavía no hay validador que la consuma.

## Propósito dentro del ERP

Sin provider/builder implementado todavía en `ElectronicDocuments`. Disponible para una
fase futura.

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
