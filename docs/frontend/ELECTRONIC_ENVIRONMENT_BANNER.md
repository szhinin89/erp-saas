# `ZHElectronicEnvironmentBanner` — indicador de estado de facturación electrónica

Infraestructura transversal del ERP. Componente de Design System sin props, que muestra el
estado operativo de facturación electrónica en cualquier pantalla emisora de documentos
electrónicos (Ventas, Caja y Retenciones de Compras; a futuro Notas de Crédito/Débito y Guías de Remisión).

## Contrato — backend es la única fuente de verdad

```
GET /api/v1/electronic-invoicing/status   (Authorize — cualquier usuario autenticado de la empresa)
```

Calculado exclusivamente por
`GetElectronicInvoicingStatusQueryHandler` (`ERP.Application/Modules/ElectronicInvoicing/UseCases/GetElectronicInvoicingStatus/`).
El frontend **nunca** recalcula el estado combinando `configured`/`certificateInstalled`/etc. —
solo lee `status` y renderiza. El resto de los campos del DTO es detalle/diagnóstico para
ampliaciones futuras (mostrar fecha de expiración, disponibilidad SRI, etc.).

```csharp
public sealed record ElectronicInvoicingStatusDto(
    ElectronicInvoicingStatus Status,      // campo principal
    bool Configured,
    string? Environment,                   // "Production" | "Test"
    string? EnvironmentName,               // "Producción" | "Pruebas"
    string? EmissionType,                  // "Normal"
    bool CertificateInstalled,
    bool CertificateValid,
    DateTime? CertificateExpiresAt,
    int? CertificateDaysRemaining,
    SriAvailability SriAvailability,       // Unknown | Available | Unavailable
    bool CanIssue);
```

Nunca expone rutas de certificado, contraseñas ni ningún dato sensible.

## Estados oficiales (`ElectronicInvoicingStatus`)

| Estado | Cuándo se produce | Color | 
|---|---|---|
| `Ready` | Configurado, certificado válido y vigente, ambiente Producción, SRI responde | Verde |
| `Testing` | Igual que `Ready` pero ambiente Pruebas | Ámbar |
| `Incomplete` | Sin certificado instalado, certificado inválido (password/corrupto), o WSDL con formato inválido | Naranja |
| `NotConfigured` | No existe fila `SriSettings` para la empresa | Rojo |
| `CertificateExpired` | Certificado válido pero `NotAfterUtc` ya pasó | Rojo |
| `CertificateExpiring` | Certificado válido, vence dentro de `CertificateExpiringThresholdDays` (30 días) | Ámbar |
| `SriUnavailable` | Todo lo demás correcto, pero `ISriConnectivityChecker.PingAsync` falla | Naranja |
| `Disabled` | Existe `SriSettings` pero `electronic_documents.emission_enabled` está ausente o en `false` (SRI-TOGGLE-02/06/07): pausa total, ninguna llamada de la empresa al SRI (comprobantes propios, XML/NC de proveedores, ping) | Gris |
| `Error` | Excepción inesperada al resolver el estado (ver try/catch del handler) — nunca se propaga como 500 | Rojo |

Orden de precedencia en el handler: `NotConfigured` → `Disabled` (sin evaluar certificado ni hacer ping) → certificado (instalado/password) → vencimiento → vencimiento
próximo → formato de WSDL → conectividad SRI → `Ready`/`Testing`. El ping al SRI **solo** se
ejecuta cuando todo lo anterior ya es válido, para no gastar una llamada de red cuando el estado
ya está determinado por otra causa.

## Frontend — un único registro visual

`frontend/src/components/zh/electronicInvoicingStatusRegistry.ts` es el **único** lugar que
traduce `ElectronicInvoicingStatus` → `{ variant, icon, messageKey, detailKey }`. El componente
(`ZHElectronicEnvironmentBanner.tsx`) hace únicamente `REGISTRY[status.status]` y renderiza vía
`ZHPageNotice`/`ZHFormAlert` — cero lógica de decisión en el componente.

### Extender con un estado nuevo

1. Backend: agregar el valor a `ElectronicInvoicingStatus` (enum) y la rama correspondiente en
   `GetElectronicInvoicingStatusQueryHandler`.
2. Frontend: agregar la unión de tipo en `ElectronicInvoicingStatus` (`electronicInvoicingService.ts`)
   y una entrada en `ELECTRONIC_INVOICING_STATUS_REGISTRY`.

Ninguna pantalla consumidora (`SalesPage.tsx` u otras) requiere cambios.

## Caché — Zustand, sin refetch por pantalla

`frontend/src/store/electronicInvoicingStatusStore.ts` (mismo patrón que `useSessionStore`).
Se refresca en exactamente 3 eventos:

1. Login / bootstrap de sesión — `SessionBootstrap.tsx`.
2. Cambio de empresa — `CompanySwitcher.tsx`.
3. Guardar configuración SRI o subir certificado — `useSriConfigPage.ts`.

Ninguna pantalla emisora dispara su propia petición HTTP.

## Design System — variantes nuevas

`ZHFormAlertType` se amplió con `attention` (naranja, `--color-attention: #C2540B`, nuevo token
en `design-tokens.css`) y `neutral` (gris, reutiliza `--color-text-secondary`/
`--color-surface-container`, mismos tokens que `.badge--gray`). `ZHFormAlert`/`ZHPageNotice`
aceptan además un `icon` opcional (Material Symbol) que sobrescribe el icono por defecto de la
variante — necesario porque varios estados comparten color pero deben distinguirse por icono
(p. ej. `Incomplete` y `SriUnavailable` son ambos `attention`).

## Reutilización

```tsx
<ZHElectronicEnvironmentBanner />
```

Sin props, sin configuración adicional. Integrado en Ventas, Caja y `RetentionSection` de
Compras mediante `PurchaseRetentionDraft`: en borrador, con retención aplicable y la opción
«Emitir la retención al confirmar la compra» activa, aparece junto al punto y fecha de emisión,
antes de «Confirmar compra». Usa el store global y el registro visual existentes, sin valores
de ambiente fijos. Para incorporarlo a otras pantallas basta con importar y colocar la misma línea.

## Salud técnica SRI — monitor central (`ZHSriServiceHealth`)

Indicador separado del banner: **disponibilidad técnica** de Recepción y Autorización del SRI en el
ambiente de la empresa. Nunca expresa una decisión fiscal (DEVUELTA / NO AUTORIZADO son respuestas
válidas del servicio).

**Fuente (backend).** `SriServiceHealthMonitor` (Application, singleton en memoria) agrupado por
(host del `WsdlUrl`, servicio): `celcer` (Pruebas) y `cel` (Producción) quedan separados. Lo
alimentan solo decoradores de DI sobre los adaptadores existentes — `ObservedSriReceptionClient`,
`ObservedSriAuthorizationClient`, `ObservedSriConnectivityChecker` (`ERP.Infrastructure/Services/Sri/Health/`)
— que devuelven el resultado intacto y registran solo servicio, clasificación, duración e instante
(sin clave de acceso, XML, empresa ni mensajes del SRI). No agrega llamadas SOAP ni pings, ni
persiste nada: un reinicio vuelve a "Sin verificar". ConsultaComprobante (anulación de
retenciones) no se observa: no es Recepción ni Autorización.

**Clasificación de una llamada** (`SriObservationOutcome`): respuesta válida; polling de
autorización agotado sin decisión (respuestas SOAP válidas → **no** es lentitud ni caída, y su
duración no se mide porque incluye esperas deliberadas); SOAP Fault / respuesta no interpretable;
fallo de transporte (`ERROR_CONEXION`, ya tras los reintentos del cliente); no determinable (consulta
de decisiones fallida, que solo trae texto); ping alcanzable / no alcanzable (evidencia débil).

**Estados** (`SriServiceHealthEvaluator`, umbrales en appsettings `SriHealth`):

| Estado | Regla |
|---|---|
| Sin verificar | Sin observaciones en `EvidenceFreshnessMinutes` (15) o configuración SRI incompleta |
| No disponible | `ConsecutiveTransportFailuresForUnavailable` (2) llamadas reales seguidas con fallo de transporte, sin un ping alcanzable posterior |
| Intermitente | Último fallo aislado, SOAP Fault, fallo no determinable, fallos recientes con recuperación, o solo pings fallidos (un ping nunca diagnostica caída) |
| Lento | Todo responde, pero la última llamada medible tardó ≥ `SlowCallThresholdSeconds` (8 s) |
| Disponible | Respuestas válidas vigentes (o solo un ping alcanzable, marcado como evidencia `Probe`) |

**Contrato.** `GET /api/v1/electronic-invoicing/sri-health` (`SriServiceHealthController`,
autenticado + empresa propia, igual que `/status`): `environment`, `environmentName`, y por servicio
`state`, `reason`, `evidence`, `lastObservedAtUtc`. Solo lee memoria.

**Frontend.** Slice `health` + `refreshHealth()` del mismo `electronicInvoicingStatusStore` (TTL 60 s,
deduplicado; se relee tras `refreshConnectivity` y tras "Validar configuración"). Presentación única en
`sriServiceHealthRegistry.ts`; `ZHSriServiceHealth` (`compact` con motivo y fecha en el tooltip,
`detail` con motivo y fecha visibles). Integrado en Ventas (reemplaza el badge básico; el ping de
entrada y el aviso al emitir se conservan), Nota de crédito y Retenciones (Compra/Gasto) solo mientras
el comprobante depende del SRI, Monitor y Configuración SRI (`detail`).

## Nota de gobernanza

Este documento describe el contrato tal como quedó implementado. Formalizarlo como una entrada
oficial "Infraestructura CLOSED" en `CLAUDE.md` (con ADR, gates CI bloqueantes, etc., como
Secuencias Documentales o Entity Tracking) es una decisión de gobernanza que requiere aprobación
explícita del equipo — no se agregó aquí sin esa confirmación.
