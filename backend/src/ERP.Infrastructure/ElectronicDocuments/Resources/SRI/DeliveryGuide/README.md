# SRI — Guía de Remisión

Archivos oficiales del SRI para el comprobante Guía de Remisión, descargados de
`https://www.sri.gob.ec/facturacion-electronica` (paquete "Esquemas XSD Y XML Tipo de
documento Guía de Remisión – Versión 1.0.0 – 1.1.0").

## Archivos

| Archivo | Versión | Tipo |
|---|---|---|
| `GuiaRemision_V1.0.0.xsd` | 1.0.0 | Esquema XSD |
| `GuiaRemision_V1.1.0.xsd` | 1.1.0 | Esquema XSD |
| `Examples/GuiaRemision_V1.0.0.xml` | 1.0.0 | XML de ejemplo oficial |
| `Examples/GuiaRemision_V1.1.0.xml` | 1.1.0 | XML de ejemplo oficial |

## Comprobante asociado

Guía de Remisión (codDoc `06`).

## Dependencias

Ambos XSD declaran `<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
schemaLocation="xmldsig-core-schema.xsd"/>` — dependen del esquema de firma XML
estándar W3C (ver `../Common/README.md`, pendiente de incorporar).

## Resolución (ver `../manifest.json` y `../README.md`)

**Atención a la clave**: el manifiesto usa `"ShippingGuide"` (nombre del enum
`ElectronicDocumentType.ShippingGuide`), no `"DeliveryGuide"` — el nombre de esta
carpeta es la organización física elegida por el equipo, distinta del nombre del enum de
dominio. El manifiesto es exactamente lo que desacopla ambos: no se puede derivar la
carpeta a partir del enum ni viceversa. `activeVersion: null` — todavía no hay validador
que la consuma.

## Propósito dentro del ERP

Sin provider/builder implementado todavía en `ElectronicDocuments` — el módulo de
logística/guías es solo un directorio placeholder en el ERP hoy. Disponible para una
fase futura.

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
