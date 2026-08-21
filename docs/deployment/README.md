# Deployment — ERP SaaS

Plantillas y guías de despliegue (placeholder enterprise).

| Recurso | Ubicación |
|---------|-----------|
| Docker Compose prod | [`docker-compose.prod.yml`](../../docker-compose.prod.yml) |
| Docker Compose localprod (piloto) | [`docker-compose.localprod.yml`](../../docker-compose.localprod.yml) — ver [`docs/DOCKER_LOCAL_PROD.md`](../DOCKER_LOCAL_PROD.md) |
| Base de servicios | [`infrastructure/docker/compose.base.yml`](../../infrastructure/docker/compose.base.yml) |
| Ops / deployment IaC | [`infrastructure/deployment/`](../../infrastructure/deployment/) |
| CI deploy opcional | [`.github/workflows/build-and-deploy.yml`](../../.github/workflows/build-and-deploy.yml) |

> Antes de producción real: secrets, redes, volúmenes, réplicas y variables de entorno según [`docs/DEVELOPMENT.md`](../DEVELOPMENT.md). `docker-compose.prod.yml` es hoy un stub (solo incluye `compose.base.yml`) — no define aún los servicios `erp-api`/`erp-frontend` para producción real; para piloto se usa `docker-compose.localprod.yml`.

## Backup / Restore / Rollback

Ya implementado y documentado en detalle — no procedimiento manual improvisado:

| Necesidad | Cómo |
|---|---|
| Backup (Postgres + FileStorage, con checksums) | `.\scripts\backup-localprod.ps1` |
| Validar que un backup realmente restaura (drill aislado, no toca el stack real) | `.\scripts\restore-check-localprod.ps1` |
| Restaurar sobre el sistema real | Ver `docs/BACKUP_RESTORE_LOCALPROD.md` §8 |
| Rollback de la aplicación (API/Frontend) a un commit anterior | Ver `docs/DOCKER_LOCAL_PROD.md` § Rollback de la aplicación |
| Rollback de esquema de base de datos | Ver `docs/DOCKER_LOCAL_PROD.md` § Rollback de la aplicación (requiere revisar `Down()` de cada migración) |

Detalle completo, incluyendo qué se respalda, qué NO se respalda (`.env.docker.local`, deliberadamente — contiene secretos) y cómo resguardar los secretos por separado: [`docs/BACKUP_RESTORE_LOCALPROD.md`](../BACKUP_RESTORE_LOCALPROD.md).

**Pendiente real**: los backups son on-demand (ejecutados manualmente por un operador), no programados automáticamente (cron/Task Scheduler/job recurrente) — para un piloto con datos reales de producción, conviene agendar `backup-localprod.ps1` con una periodicidad y copiar `backups/localprod/` fuera del host.

## SMTP (correo de factura autorizada)

El envío de correo (ej. "factura autorizada") nunca bloquea una venta ni una autorización SRI — el
encolado (`CommunicationOutbox`) está desacoplado del flujo comercial, y si no hay SMTP configurado
el correo simplemente no se envía (queda `Pending`/`Failed` en el outbox, reintentable, sin afectar
la factura ya emitida).

`CommunicationSettingsResolver` (`backend/src/ERP.Infrastructure/Communications/CommunicationSettingsResolver.cs`)
resuelve la configuración SMTP en dos capas, en este orden:

1. **`OrgSettings` por empresa** (claves `communications.email.*` — `OrgSettingKeys.Communications` en
   `backend/src/ERP.Domain/Modules/Configuration/Constants/OrgSettingKeys.cs`). Hoy **no existe un
   endpoint/pantalla de administración** para escribir estas claves vía la app — solo el resolver las
   lee. Exponerlas en un endpoint/pantalla de configuración es trabajo funcional nuevo, fuera de
   alcance de este cierre.
2. **Fallback por variables de entorno** (`Communications:Email:*` en `appsettings`/env vars) — este es
   el camino real y recomendado para el piloto (una sola empresa, sin necesidad de autoservicio):

   | Variable | Ejemplo (Zoho) |
   |---|---|
   | `Communications__Email__Enabled` | `true` |
   | `Communications__Email__SmtpHost` | `smtp.zoho.com` |
   | `Communications__Email__SmtpPort` | `587` |
   | `Communications__Email__SmtpUsername` | `facturacion@tudominio.com` |
   | `Communications__Email__SmtpPassword` | *(credencial real de Zoho — nunca en el repo)* |
   | `Communications__Email__SenderEmail` | `facturacion@tudominio.com` |
   | `Communications__Email__SenderName` | `Nombre Comercial` |
   | `Communications__Email__UseSsl` | `true` |
   | `Communications__Email__MaxRetries` | `3` |

   Agregar estas variables al bloque `environment:` de `erp-api` en `docker-compose.localprod.yml` (o a
   `.env.docker.local`, gitignored, y referenciarlas desde ahí) — igual patrón que `JWT_SECRET_KEY`/
   `POSTGRES_PASSWORD`. **Nunca** comitear la contraseña SMTP real en ningún archivo versionado.

**Smoke real de envío queda pendiente únicamente por la credencial SMTP real** — con las variables de
arriba configuradas, el flujo ya está implementado y probado (ERP-CORE-CLOSEOUT-06/07); falta solo
que el piloto provea una cuenta Zoho real para verificar la entrega end-to-end.

## SRI / Certificado electrónico

- **Factura física NO depende del certificado**: `PhysicalSalesInvoiceEmissionStrategy` no tiene ninguna dependencia de `IElectronicDocumentIssuer`/certificado — garantía estructural, no solo de configuración (confirmado en ERP-CORE-CLOSEOUT-07). Un piloto puede operar 100% con facturación física sin ningún dato SRI configurado.
- **Venta electrónica SÍ requiere certificado + settings**: ambiente SRI + WSDL (`SriSettings`) y certificado `.p12` subido vía `PUT`/`POST /api/v1/electronic-invoicing/sri-configuration` y `.../sri-configuration/certificate` (pantalla `/settings/electronic-invoicing`). Sin esto, la emisión electrónica devuelve un error de validación claro ("La empresa no tiene un certificado digital (P12) registrado..." / "no tiene configuración SRI registrada..."), nunca un 500 — confirmado en ERP-CORE-CLOSEOUT-06/07.
- **Readiness**: `GET /api/companies/operational-readiness` indica exactamente qué falta (`sriConfig`, `certificate`, `emissionPoint`) antes de intentar una venta electrónica — no hace falta descubrirlo por ensayo y error.
- **Proveedor de sistema en el XML — pendiente de fuente normativa**: `SystemProviderSettings` (RUC/razón social/CIIU de ZH Technologies como proveedor de sistema, ERP-CORE-CLOSEOUT-09) ya tiene configuración dinámica lista (`GET`/`PUT /api/v1/system/provider-settings`), pero **todavía no se inyecta en ningún XML de comprobante** — falta el texto de la Resolución NAC-DGERCGC26-00000027 o la ficha técnica SRI que confirme el campo/elemento exacto antes de tocar `InvoiceXmlBuilder`/`CreditNoteXmlBuilder`. No inventar esa estructura sin esa fuente.

## Dominio / SSL

**Pendiente externo, no resuelto en este repo.** `docker-compose.localprod.yml` expone la API/frontend en `localhost` sin TLS. Para el piloto real falta: dominio `.com.ec` registrado, terminación SSL (reverse proxy tipo Caddy/Traefik/nginx+certbot delante de `erp-frontend`), y actualizar `Cors:AllowedOrigins`/`VITE_API_URL` al dominio real. No inventar aquí una configuración de SSL sin el dominio real confirmado.

## Pendientes externos del piloto (resumen)

Ver también `STATUS.md` (cierre ERP-CORE-CLOSEOUT-10-FINALIZE) para el veredicto final y la clasificación completa (cerrado / externo inevitable / no aplicable).

- SMTP real (Zoho u otro) — falta la credencial real para completar el smoke de envío (ver sección SMTP arriba).
- Impresora térmica física — falta hardware para la prueba física del Print Agent (ver `print-agent/README.md`).
- Dominio `.com.ec` y SSL (ver sección anterior).
- Certificado `.p12` SRI real y confirmación de ambiente (pruebas/producción) por empresa piloto.
- Confirmación normativa de la Resolución NAC-DGERCGC26-00000027 (proveedor de sistema) antes de tocar el XML electrónico (ver sección SRI arriba).
- Backups productivos con periodicidad automatizada (los scripts ya existen y funcionan — `backup-localprod.ps1`/`restore-check-localprod.ps1`; falta solo agendarlos).
