# Estándar de Mensajes Visuales (INMUTABLE)

Decisión arquitectónica congelada 2026-06-29. No modificar sin revisión arquitectónica formal.

ADR: [`docs/decisions/ADR-018-message-infrastructure.md`](../decisions/ADR-018-message-infrastructure.md)

---

## Arquitectura

```
┌──────────────────────────────────────────────────────┐
│  Módulos (pages, hooks, components)                  │
│    import { message, MSG } from 'lib/messages'       │
│                                                      │
│    message.success('Guardado.')                       │
│    message.error('Falló.')                            │
│    await message.confirm({ title, message, variant })│
│    message.success(MSG.created)                      │
└──────────────────┬───────────────────────────────────┘
                   │ API pública (única entrada)
┌──────────────────▼───────────────────────────────────┐
│  lib/messages/                                       │
│    index.ts          — barrel export                 │
│    messageService.ts — fachada (message.*)            │
│    messageTypes.ts   — tipos públicos                │
│    messageCatalog.ts — MSG constantes reutilizables  │
│    messageDefaults.ts— configuración centralizada    │
│    _internal/                                        │
│      messageStore.ts — Zustand (detalle interno)     │
└──────────────────┬───────────────────────────────────┘
                   │ solo componentes ZH leen el store
┌──────────────────▼───────────────────────────────────┐
│  components/zh/                                      │
│    ZHToast.tsx         — renderiza cola de toasts     │
│    ZHGlobalDialogs.tsx — renderiza confirm/prompt     │
│    ZHConfirmModal.tsx  — UI del modal (sin estado)   │
│    ZHPageNotice.tsx    — alert inline (sin estado)   │
└──────────────────────────────────────────────────────┘
```

**Regla fundamental**: los módulos nunca importan el store interno. Solo usan `message.*` de `lib/messages`.

---

## API pública

```ts
import { message, MSG } from 'lib/messages';

// Toast efímero
message.success('Ítem creado correctamente.');
message.error('Error al guardar.');
message.warning('Stock bajo.');
message.info('Proveedor desactivado.');

// Catálogo de mensajes comunes
message.success(MSG.created);
message.success(MSG.updated);
message.error(MSG.unexpectedError);

// Confirmación (Promise-based)
const ok = await message.confirm({
  title: 'Anular Factura',
  message: 'Esta acción NO se puede deshacer.',
  variant: 'danger',
  confirmLabel: 'Anular',
});
if (ok) { /* proceder */ }

// Prompt con input (Promise-based)
const reason = await message.prompt({
  title: 'Motivo de anulación',
  label: 'Motivo',
  variant: 'danger',
});
if (reason) { /* proceder con reason */ }
```

---

## Tipos de mensaje permitidos

| Tipo | Color | Icono (Material Symbols) | Uso |
|------|-------|--------------------------|-----|
| **success** | Verde (`--color-success`) | `check_circle` | Guardado, creación, actualización, operación completada |
| **error** | Rojo (`--color-error`) | `error` | Error de validación, negocio, servidor, inesperado |
| **warning** | Ámbar (`--color-warning`) | `warning` | Riesgos, advertencias, acciones irreversibles |
| **info** | Azul (`--color-primary`) | `info` | Información neutral, estado de procesos, avisos |
| **confirm** | — | — | Solicitar confirmación via `message.confirm()` |

---

## Catálogo de mensajes comunes (`MSG`)

```ts
import { MSG } from 'lib/messages';

MSG.created        // 'Registro creado correctamente.'
MSG.updated        // 'Registro actualizado correctamente.'
MSG.deleted        // 'Registro eliminado correctamente.'
MSG.saved          // 'Cambios guardados correctamente.'
MSG.operationOk    // 'Operación realizada correctamente.'
MSG.cancelled      // 'Acción cancelada.'
MSG.activated      // 'Registro activado.'
MSG.deactivated    // 'Registro desactivado.'
MSG.unexpectedError// 'Error inesperado. Intente nuevamente.'
MSG.loadError      // 'Error al cargar los datos.'
MSG.saveError      // 'Error al guardar. Revisa los datos.'
MSG.noRecords      // 'No existen registros.'
```

Los módulos pueden usar mensajes personalizados cuando el catálogo no aplica.

---

## Configuración centralizada (`MESSAGE_CONFIG`)

| Propiedad | Valor | Ubicación |
|-----------|-------|-----------|
| `autoCloseDuration` | 4000ms | `lib/messages/messageDefaults.ts` |
| `maxVisible` | 3 | `lib/messages/messageDefaults.ts` |
| `duplicatePolicy` | `reset-timer` | `lib/messages/messageDefaults.ts` |
| `position` | `top-right` | `lib/messages/messageDefaults.ts` |
| `icons` | Mapeo tipo→icono | `lib/messages/messageDefaults.ts` |

### Política de duplicados

Si un mensaje idéntico (mismo texto + mismo tipo) ya está visible:
- **`reset-timer`** (actual): reinicia el temporizador del existente, no agrega otro.
- Nunca se muestran múltiples toasts idénticos simultáneamente.

### Cola global

- Máximo `maxVisible` toasts visibles a la vez.
- Orden FIFO — el más antiguo se descarta cuando la cola está llena.
- Toda la aplicación comparte la misma cola.

---

## Componentes oficiales

### 1. Alert inline — `ZHPageNotice` / `ZHFormAlert`

- **Uso**: Mensajes persistentes en la página (errores, estado, banners informativos)
- Los módulos importan directamente `ZHPageNotice` — es un componente UI sin estado.

```tsx
<ZHPageNotice variant="error" message="Error al cargar." detail={error} />
```

### 2. Toast — `ZHToast` (global, montado en AppLayout)

- Renderiza la cola de mensajes del store interno.
- Los módulos **nunca** importan ni montan `ZHToast`.

### 3. Confirm/Prompt — `ZHGlobalDialogs` (global, montado en AppLayout)

- Renderiza el diálogo de confirmación activo del store interno.
- Los módulos llaman `message.confirm()` / `message.prompt()` — **nunca** montan `ZHConfirmModal` directamente para nuevos flujos.
- Los módulos que ya usan `ZHConfirmModal` con estado local propio (Sales, Purchases, WarehouseListTab) son compatibles y se migrarán incrementalmente.

---

## Colores (tokens del tema)

| Token | Valor | Tipo |
|-------|-------|------|
| `--color-success` | `#1A7A4A` | Verde |
| `--color-error` | `#C0392B` | Rojo |
| `--color-warning` | `#BA7517` | Ámbar |
| `--color-info` / `--color-primary` | `#3a5f84` | Azul |

---

## Validaciones automáticas (ESLint)

El proyecto tiene reglas ESLint que detectan automáticamente:

- Import directo de `_internal/messageStore` desde módulos → **error**
- Import de `stores/toastStore` (eliminado) → **error**

Configurado en `eslint.config.js` → `noDirectMessageStore`.

---

## Prohibido

- Importar `_internal/messageStore` o `useMessageStore` desde módulos
- Crear componentes locales de toast, alert o confirm
- Usar `window.confirm()` o `window.alert()`
- Hardcodear colores hex para feedback
- Crear stores de toast locales por módulo
- Inventar tipos de mensaje distintos a los cinco oficiales
- Usar estilos inline para banners de error/éxito
- Hardcodear duración, z-index o posición del toast fuera de `messageDefaults.ts`

---

## Architecture Gate — Criterios de cierre

| # | Criterio | Estado |
|---|----------|--------|
| VM-1 | Toasts usan exclusivamente `message.*` de `lib/messages` | obligatorio |
| VM-2 | Alerts inline usan `ZHPageNotice` o `ZHFormAlert` | obligatorio |
| VM-3 | Confirmaciones destructivas usan `message.confirm()` o `ZHConfirmModal` | obligatorio |
| VM-4 | Colores de feedback vienen de tokens CSS, no hardcodeados | obligatorio |
| VM-5 | Iconos de feedback son Material Symbols del mapeo oficial | obligatorio |
| VM-6 | No existen componentes locales de toast, alert o confirm | obligatorio |
| VM-7 | No existe `window.confirm()` ni `window.alert()` | obligatorio |
| VM-8 | No existe import directo de `_internal/messageStore` | obligatorio |
| VM-9 | Configuración de toast viene de `messageDefaults.ts`, no hardcodeada | obligatorio |

---

## Checklist de revisión (para auditorías futuras)

- [ ] No se usa `alert()`, `window.alert()`, `confirm()`, ni `window.confirm()`.
- [ ] No se importa `_internal/messageStore` desde módulos.
- [ ] No se accede directamente al store Zustand desde páginas o componentes de módulo.
- [ ] No existen componentes locales de toast, alert o confirm.
- [ ] No existen colores hardcodeados para feedback.
- [ ] Todo toast se invoca exclusivamente via `message.*`.
- [ ] La configuración centralizada en `messageDefaults.ts` es la única fuente de verdad.
- [ ] Los tests en `lib/messages/__tests__/messageStore.test.ts` pasan.
- [ ] ESLint no reporta violaciones de `no-restricted-imports` relacionadas con mensajes.

---

## Preparación para evolución

La arquitectura actual permite agregar estas capacidades **sin modificar la API pública** ni los módulos consumidores:

- **i18n** — las claves de `MSG` pueden resolverse via `t()` en el llamador, o el catálogo puede evolucionar a claves i18n.
- **Modo oscuro** — los colores ya usan CSS variables; basta agregar un bloque `:root[data-theme="dark"]`.
- **Telemetría** — interceptar en `messageService.ts` antes de delegar al store.
- **SignalR / push** — `messageService.ts` puede recibir pushes del servidor e invocar `push()`.
- **Notificaciones persistentes** — el campo `persistent` ya existe en `MessageItem`.
- **Prioridad / severidad** — extensible en `MessageItem` sin romper consumidores.
