# SRI — Liquidación de Compra de Bienes y Prestación de Servicios

Archivos oficiales del SRI para la Liquidación de Compra de Bienes y Prestación de
Servicios, descargados de `https://www.sri.gob.ec/facturacion-electronica` (paquete
"Esquemas XSD Y XML Tipo de documento Liquidación – Versión 1.0.0 – 1.1.0").

## Archivos

| Archivo | Versión | Tipo |
|---|---|---|
| `LiquidacionCompra_V1.0.0.xsd` | 1.0.0 | Esquema XSD |
| `LiquidacionCompra_V1.1.0.xsd` | 1.1.0 | Esquema XSD |
| `Examples/LiquidacionCompra_V1.0.0.xml` | 1.0.0 | XML de ejemplo oficial |
| `Examples/LiquidacionCompra_V1.1.0.xml` | 1.1.0 | XML de ejemplo oficial |

## Comprobante asociado

Liquidación de Compra de Bienes y Prestación de Servicios (codDoc `03`).

## Dependencias

Ambos XSD declaran `<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
schemaLocation="xmldsig-core-schema.xsd"/>` — dependen del esquema de firma XML
estándar W3C (ver `../Common/README.md`, pendiente de incorporar).

## Resolución (ver `../manifest.json` y `../README.md`)

**Atención a la clave**: el manifiesto usa `"PurchaseSettlement"` (nombre del enum
`ElectronicDocumentType.PurchaseSettlement`), no `"Liquidation"` — el nombre de esta
carpeta es la organización física elegida por el equipo, distinta del nombre del enum de
dominio. `activeVersion: null` — todavía no hay validador que la consuma.

## Propósito dentro del ERP

Sin implementación en ningún módulo del ERP todavía. Disponible para una fase futura.

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
