# SRI — Comprobante de Retención

Archivos oficiales del SRI para el Comprobante de Retención, descargados de
`https://www.sri.gob.ec/facturacion-electronica` (paquete "Esquemas XSD Y XML Tipo de
documento Comprobante de Retención – Versión 1.0.0 – 2.0.0").

## Archivos

| Archivo | Versión | Tipo |
|---|---|---|
| `ComprobanteRetencion_V1.0.0.xsd` | 1.0.0 | Esquema XSD |
| `ComprobanteRetencion_V2.0.0.xsd` | 2.0.0 | Esquema XSD |
| `Examples/ComprobanteRetencion_V1.0.0.xml` | 1.0.0 | XML de ejemplo oficial |
| `Examples/ComprobanteRetencion_V2.0.0.xml` | 2.0.0 | XML de ejemplo oficial |

## Comprobante asociado

Comprobante de Retención (codDoc `07`).

## Dependencias

Ambos XSD declaran `<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
schemaLocation="xmldsig-core-schema.xsd"/>` — dependen del esquema de firma XML
estándar W3C (ver `../Common/README.md`, pendiente de incorporar).

## Resolución (ver `../manifest.json` y `../README.md`)

La entrada `"Retention"` de `manifest.json` (clave = nombre del enum
`ElectronicDocumentType.Retention`) lista ambas versiones con su dependencia común.
`activeVersion: null` — todavía no hay validador que la consuma.

## Propósito dentro del ERP

El dominio de Retenciones existe en Compras (cálculo, emisión, CxP) pero todavía no está
conectado a `ElectronicDocuments` — no genera XML/firma/envío electrónico. Estos
archivos quedan disponibles para cuando se implemente esa conexión.

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
