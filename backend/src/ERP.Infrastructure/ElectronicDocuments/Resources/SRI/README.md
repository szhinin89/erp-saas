# Recursos oficiales del SRI — estrategia de resolución

Esta carpeta contiene los XSD y XML de ejemplo oficiales del SRI para cada tipo de
comprobante electrónico, organizados por tipo (`Invoice/`, `CreditNote/`, `DebitNote/`,
`DeliveryGuide/`, `Retention/`, `Liquidation/`), más `Common/` (esquemas compartidos,
p.ej. firma XML) y `Catalogs/` (catálogos oficiales, hoy vacío — ver su README).

## `manifest.json` — única fuente de verdad

Los archivos XSD del SRI **no siguen una convención de nombre consistente entre
comprobantes** (`factura_V1.1.0.xsd` vs. `ComprobanteRetencion_V2.0.0.xsd`, por ejemplo)
— derivar el nombre físico a partir del `ElectronicDocumentType`/versión es frágil.
`manifest.json` es la única fuente de verdad de qué archivo corresponde a cada
combinación (tipo, versión), y de sus dependencias `xs:import`/`xs:include`:

```json
{
  "documentTypes": {
    "Invoice": {
      "activeVersion": "1.1.0",
      "versions": [
        { "version": "1.1.0", "xsd": "Invoice/factura_V1.1.0.xsd",
          "dependencies": ["Common/xmldsig-core-schema.xsd"] }
      ]
    }
  }
}
```

- Las claves de `documentTypes` son los nombres del enum `ElectronicDocumentType`
  (`Invoice`, `CreditNote`, `DebitNote`, `Retention`, `ShippingGuide`,
  `PurchaseSettlement`) — **no** los nombres de carpeta (`DeliveryGuide`,
  `Liquidation`); el manifiesto es precisamente lo que desacopla ambos.
- `activeVersion` documenta qué versión usa hoy el validador activo del ERP (`null` si
  el tipo todavía no tiene provider/builder/validador implementado).
- `dependencies` lista, en orden, los XSD auxiliares que deben cargarse en el mismo
  `XmlSchemaSet` **antes** de compilar — la resolución de `xs:import` en .NET ocurre por
  coincidencia de `targetNamespace` dentro del set ya cargado, no por resolución de
  `schemaLocation` en disco, así que no se necesita un `XmlResolver` personalizado.

## Consumo desde código

`ERP.Infrastructure/Services/ElectronicDocuments/EmbeddedXmlSchemaProvider.cs`
(implementa `IXmlSchemaProvider`, contrato sin cambios) carga `manifest.json` como
`EmbeddedResource`, resuelve la entrada `(documentType, schemaVersion)`, carga el XSD
principal + cada dependencia como recursos embebidos, y compila todo en un único
`XmlSchemaSet`. Si falta cualquiera de los archivos (incluida una dependencia), devuelve
`null` de forma controlada — nunca una excepción; el validador que consume la interfaz
decide qué significa esa ausencia (hoy: `IsValid=false` explícito).

Todos los `.xsd` bajo esta carpeta (y `manifest.json`) se embeben automáticamente vía el
glob `ElectronicDocuments\Resources\SRI\**\*.xsd` en `ERP.Infrastructure.csproj` — agregar
un XSD nuevo no requiere tocar el `.csproj`, pero sí requiere agregar su entrada en
`manifest.json` (el glob embebe el archivo; el manifiesto lo hace resoluble).

## Reglas (aplican a todas las subcarpetas)

- Únicamente archivos **oficiales** — nunca reconstruidos, editados ni generados por el
  equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si se publica una nueva versión, se agrega junto a la anterior — **nunca se elimina**
  una versión existente — y se agrega su entrada correspondiente en `manifest.json`.
