# Sales/InvoiceIssued: mantenimiento de Production

Patrón auditado: `Program.cs` ejecuta migraciones EF antes de comandos de mantenimiento y sale
antes del bootstrap global/host web. Precedentes: `backfill-accounting-chart-hierarchy`
(transacción por empresa) y `remediate-purchase-return-postings` (dry-run y `apply`).
El startup `AccountingChartBackfillService.EnsureAsync` excluye Production y sigue igual.

El nuevo comando usa el servicio existente, sin ejecutar el bootstrap general. Examina todas
las empresas existentes, incluidas las inactivas, con aislamiento explícito tenant/company.
Solo cambia reglas activas con encabezado canónico y combinación exacta de 4 o 5 líneas.
Comparte el reconocimiento/corrección con AccountingBootstrapStep. Conserva todos los IDs
existentes; en 4 líneas cambia el AmountKind de CxC a PendingBalance y agrega Caja.
Agrega Discount e IRBPNR. Las 7 canónicas quedan intactas. Las reglas custom/parciales,
inactivas, ausentes o con cuentas faltantes/inactivas/no imputables se diagnostican sin cambios.
No crea cuentas, reglas ausentes ni asientos; no consulta ni modifica JournalEntries.

Cada empresa se valida y actualiza en una transacción serializable. Un error aborta el comando
y revierte esa empresa; las anteriores ya confirmadas permanecen aplicadas. Es seguro repetir.

## Ejecución de despliegue

El workflow actual publica/despliega la API; **no ejecuta este comando automáticamente**.
Ejecutar desde el directorio publicado, con la configuración/secrets del servicio Production
(conexión, JWT, CORS y PasswordReset:PublicBaseUrl); no usar credenciales demo.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:DOTNET_ENVIRONMENT = 'Production'
dotnet ERP.API.dll backfill-sales-invoice-posting-rule
dotnet ERP.API.dll backfill-sales-invoice-posting-rule apply
dotnet ERP.API.dll backfill-sales-invoice-posting-rule
```

Revisar el diagnóstico del primer comando antes de ejecutar `apply`. El último debe reportar
Canonical7 para las reglas corregidas; custom/InvalidAccounts requieren resolución separada,
nunca se fuerza su actualización. Como los demás comandos existentes, incluso el dry-run
ejecuta primero las migraciones EF pendientes; dry-run se refiere al backfill de reglas.
No hay migración EF nueva en este cambio. No se ejecutó contra Production durante el desarrollo.
