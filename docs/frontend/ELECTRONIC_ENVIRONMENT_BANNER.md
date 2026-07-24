# `ZHElectronicEnvironmentBanner` — indicador de estado de facturación electrónica

Infraestructura transversal del ERP. Componente de Design System sin props, que muestra el
estado operativo de facturación electrónica en cualquier pantalla emisora de documentos
electrónicos (Ventas, y a futuro Notas de Crédito/Débito, Retenciones, Guías de Remisión).

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
| `Disabled` | **Reservado** — ningún camino del handler lo produce hoy; para cuando exista un toggle de habilitar/deshabilitar emisión por empresa | Gris |
| `Error` | Excepción inesperada al resolver el estado (ver try/catch del handler) — nunca se propaga como 500 | Rojo |

Orden de precedencia en el handler: certificado (instalado/password) → vencimiento → vencimiento
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

Sin props, sin configuración adicional. Integrado hoy en `SalesPage.tsx`; para incorporarlo a
Notas de Crédito/Débito, Retenciones o Guías de Remisión basta con importar y colocar la misma
línea.

## Nota de gobernanza

Este documento describe el contrato tal como quedó implementado. Formalizarlo como una entrada
oficial "Infraestructura CLOSED" en `CLAUDE.md` (con ADR, gates CI bloqueantes, etc., como
Secuencias Documentales o Entity Tracking) es una decisión de gobernanza que requiere aprobación
explícita del equipo — no se agregó aquí sin esa confirmación.
