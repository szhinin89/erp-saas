# SRI — Catálogos

Catálogos oficiales del SRI referenciados por los distintos comprobantes electrónicos
(tablas de códigos publicadas en los anexos de la ficha técnica de comprobantes
electrónicos: formas de pago, tipos de identificación, códigos de impuestos, etc.).

## Estado

Se auditó `https://www.sri.gob.ec/facturacion-electronica` completa (todos los enlaces
de la sección de facturación electrónica) y no existe, a la fecha de esta auditoría,
ningún archivo descargable independiente (XSD/XML/CSV) publicado por el SRI que
contenga estos catálogos como datos estructurados. Las tablas de códigos están
documentadas únicamente como texto/tablas dentro de la Ficha Técnica de Comprobantes
Electrónicos Esquema Offline (PDF, ya disponible en `docs/` del repositorio) — no como
un recurso descargable separado.

Esta carpeta queda preparada para el día en que el SRI publique un catálogo
descargable independiente, o para cuando el equipo decida transcribir manualmente estas
tablas a un formato de catálogo del ERP (`sri_vat_rates`, `sri_ice_rates`, etc. — ya
existentes como catálogos en base de datos, fuera del alcance de esta carpeta).

## Reglas

- Únicamente archivos **oficiales** del SRI — nunca reconstruidos, editados ni generados
  por el equipo.
- **Nunca se modifican manualmente** una vez incorporados.
- Si el SRI publica una nueva versión, se agrega junto a la anterior — **nunca se
  elimina** una versión existente.
