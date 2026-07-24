# SRI — Factura

Archivos oficiales del SRI para el comprobante Factura, descargados de
`https://www.sri.gob.ec/facturacion-electronica` (paquete "Esquemas XSD Y XML Tipo de
documento Factura – Versión 1.0.0 – 1.1.0 – 2.0.0 – 2.1.0", portal actualizado a
febrero 2022; referenciado como esquema vigente por la Ficha Técnica de Comprobantes
Electrónicos Esquema Offline Versión 2.32, noviembre 2025).

## Archivos

| Archivo | Versión | Tipo |
|---|---|---|
| `factura_V1.0.0.xsd` | 1.0.0 | Esquema XSD |
| `factura_V1.1.0.xsd` | 1.1.0 | Esquema XSD |
| `factura_V2.0.0.xsd` | 2.0.0 | Esquema XSD |
| `factura_V2.1.0.xsd` | 2.1.0 | Esquema XSD |
| `Examples/factura_V1.0.0.xml` | 1.0.0 | XML de ejemplo oficial |
| `Examples/factura_V1.1.0.xml` | 1.1.0 | XML de ejemplo oficial |
| `Examples/factura_V2.0.0.xml` | 2.0.0 | XML de ejemplo oficial |
| `Examples/factura_V2.1.0.xml` | 2.1.0 | XML de ejemplo oficial |

## Comprobante asociado

Factura (codDoc `01`).

## Dependencias

Los 4 XSD declaran `<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
schemaLocation="xmldsig-core-schema.xsd"/>` — dependen del esquema de firma XML
estándar W3C, que debe colocarse en `../Common/xmldsig-core-schema.xsd` (ver
`Common/README.md` — pendiente de incorporar, no se pudo descargar automáticamente en
esta etapa).

## Resolución (ver `../manifest.json` y `../README.md`)

`EmbeddedXmlSchemaProvider` no deriva el nombre de archivo por convención — resuelve
`(ElectronicDocumentType.Invoice, "1.1.0")` contra la entrada `"Invoice"` de
`manifest.json`, que apunta explícitamente a `Invoice/factura_V1.1.0.xsd` y declara
`Common/xmldsig-core-schema.xsd` como dependencia.

## Propósito dentro del ERP

El motor `ElectronicDocuments` usa actualmente el esquema **v1.1.0** (`factura_V1.1.0.xsd`)
como validador XSD de Factura — es la versión con la que `InvoiceXmlBuilder` construye
el XML hoy. Las versiones 1.0.0 (legacy), 2.0.0 y 2.1.0 se conservan para referencia y
para una eventual actualización de esquema sin perder trazabilidad de versiones
anteriores.

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
