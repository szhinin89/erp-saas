# SRI — Recursos comunes

Archivos oficiales compartidos entre varios tipos de comprobante electrónico —
esquemas auxiliares referenciados por `<xs:import>`/`<xs:include>` desde más de un XSD.

## Dependencia incorporada

| | |
|---|---|
| **Archivo** | `xmldsig-core-schema.xsd` |
| **Origen** | W3C — XML Signature Syntax and Processing (`http://www.w3.org/TR/2002/REC-xmldsig-core-20020212/`) |
| **Namespace** | `http://www.w3.org/2000/09/xmldsig#` |
| **Uso** | Dependencia requerida por los XSD del SRI para firma digital XML (nodo `ds:Signature`) |

Provisto directamente por el usuario (contenido oficial del W3C, versión 0.1 del
esquema — mismo texto publicado en la especificación REC-xmldsig-core-20020212).
Verificado como XML bien formado y como XSD compilable (`XmlSchema.Read` +
`XmlSchemaSet.Compile()`), y verificada su resolución combinada junto a
`Invoice/factura_V1.1.0.xsd` en un mismo `XmlSchemaSet` — el `xs:import` del namespace
`http://www.w3.org/2000/09/xmldsig#` se resuelve correctamente por coincidencia de
`targetNamespace`, sin necesitar un `XmlResolver` personalizado.

Los 15 XSD descargados para Factura, Nota de Crédito, Nota de Débito, Guía de Remisión,
Comprobante de Retención y Liquidación (ver los README de cada carpeta) declaran, todos,
la misma dependencia:

```xml
<xsd:import namespace="http://www.w3.org/2000/09/xmldsig#"
    schemaLocation="xmldsig-core-schema.xsd"/>
```

Pertenece a `Common/` (y no a ninguna carpeta de comprobante individual) precisamente
porque es una dependencia **compartida** por los 15 XSD de los 6 tipos de comprobante —
sin él, ninguno puede compilarse/validar de forma completa (el nodo `ds:Signature` del
comprobante firmado queda sin tipo resuelto).

## Cómo se resuelve esta dependencia (ver `../manifest.json`)

Cada entrada de `manifest.json` que dependa de este archivo lo declara explícitamente en
su lista `dependencies` (p.ej. `["Common/xmldsig-core-schema.xsd"]`).
`EmbeddedXmlSchemaProvider` carga el XSD principal **y** cada dependencia declarada en el
mismo `XmlSchemaSet` antes de compilar. Con este archivo ya incorporado, las 15 entradas
del manifiesto que dependen de él (Factura/NC/ND/Guía de Remisión/Retención/Liquidación)
quedan resolubles sin ningún cambio de código adicional — el glob
`ElectronicDocuments\Resources\SRI\**\*.xsd` ya existente en `ERP.Infrastructure.csproj`
lo embebe automáticamente en el próximo build.

## Reglas

- Únicamente archivos **oficiales** (SRI o, en este caso, el estándar W3C del que el SRI
  depende) — nunca reconstruidos, editados ni generados por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si se publica una nueva versión, se agrega junto a la anterior — **nunca se elimina**
  una versión existente.
