# ADR-040 — Item por empresa y naturaleza operativa de inventario

Estado: Accepted. Fecha: 2026-10-05. Entrega: A1.

## Decisión

Item pertenece a Tenant + Company, sin ownership de Branch. La empresa se obtiene del contexto autenticado; no se acepta como autoridad desde el payload. Sin contexto válido, las lecturas quedan cerradas. BusinessPartner conserva su alcance tenant-wide y ADR-017 no cambia.

`ItemTypeDefinition` es clasificación pura. `ItemNature` es naturaleza operativa independiente, con `Product` y `Service`. Esta decisión aprobada no reabre la infraestructura CLOSED de Tipos de Ítem: sus entidades, catálogo, configuración y comportamiento permanecen intactos. Digital queda FUTURE.

Product siempre participa en Inventory. Service nunca genera StockMovement, CurrentStock, kardex ni costo promedio de Inventory y no utiliza Warehouse. El Item existente `10001204 — BIS MAN REG 12X10` se migra a Product.

`TracksStock` deja de ser autoridad de participación. Su reemplazo, `StockControlEnabled`, controla la exigencia de existencias, junto con `inventory.stock_control_enabled` de Company (default true). Si cualquiera está OFF, Product mantiene los movimientos sin bloqueo por disponibilidad. Con ambos ON se conserva la validación existente y la excepción existente `AllowSellWithoutStock`. La semántica de stock negativo y su costo se conserva; cualquier evolución corresponde a A2.

## Aislamiento y persistencia

SKU, SKU de variante y barcode son únicos por Tenant + Company; código de proveedor por Tenant + Company + Supplier. Los hijos se filtran a través del Item propietario. CompanyId físico en hijos se limita a las columnas shadow necesarias para constraints compuestos de unicidad y FK; no se duplica en el modelo de dominio.

Pricing solamente asocia Items de la misma empresa. Sales, Purchases, Inventory, matching, lookups y reportes resuelven el Item dentro de la empresa autenticada. Los Products conservan Warehouse por línea y el caso multiwarehouse.

## Migración

`20261006002212_ItemCompanyScopeAndInventoryNature` identifica ownership a partir de las referencias operativas existentes y exige una única empresa por Item. Ante referencias a varias empresas aborta. Para Items sin referencias utiliza la única empresa del tenant; si esa asignación resulta ambigua aborta. Conserva datos, convierte el flag existente a control individual y backfillea Nature como Product, incluida la referencia explícita 10001204. Añade FK y unicidad compuestas y restricciones de inventario y barcodes en PostgreSQL.

## Límites y validación

No cambia BusinessPartner, ItemTypeDefinition, ElectronicDocuments ni SRI. No introduce Digital, Promotions ni A2. Se detiene ante ownership compartido, cambios obligatorios en esas infraestructuras o decisiones de costo negativo. Las pruebas críticas usan PostgreSQL real y migraciones completas; las verificaciones finales y sus resultados se registran en el informe A1.
