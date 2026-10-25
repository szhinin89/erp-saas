# Compras — sincronización de códigos y líneas listas

Fecha: 2026-10-24. Clasificación: **BUG REAL**.

## Causa y ajuste

`pendingCount` solo consideraba el schema/precio y errores recibidos al guardar; los conflictos
asíncronos se enviaban exclusivamente a la fila. El hook devolvía una lista vacía tanto durante
la consulta como al confirmar ausencia de conflictos.

El hook compartido conserva ahora una respuesta asociada a sus candidatos, revisión y estado de
habilitación. Expone verificación pendiente/confirmada; oculta inmediatamente el resultado de
otros candidatos y conserva la cancelación de respuestas obsoletas. Compras deriva del mismo
diagnóstico por fila las etiquetas, contadores y habilitación de Resolver, con guarda adicional
en el submit. Debounce, endpoint, candidatos y validadores existentes se conservan.

Sin componentes ni validadores nuevos. El detalle compacto y «Ver ítem» permanecen disponibles.
Ítems, variantes e importador conservan su presentación y validación existentes.
SSOT: [Compras](../../architecture/purchases.md#productos-pendientes-compactos).

## Evidencia antes de pulsar Resolver

Arnés temporal con modal, hook, traducciones españolas y CSS reales; catálogos y respuestas
de diagnóstico fixture (800 ms de latencia). Sin sesión, backend ni datos de negocio. Arnés retirado.
**Cero llamadas a resolveLines y cero pulsaciones de Resolver en toda la secuencia.**

| Estado | Listas | Pendientes | Resolver |
|---|---:|---:|---|
| Código válido verificado | 1 | 0 | Habilitado |
| Nuevo código, verificación pendiente | 0 | 1 | Deshabilitado |
| SKU duplicado confirmado | 0 | 1 | Deshabilitado |
| SKU corregido, verificando | 0 | 1 | Deshabilitado |
| SKU corregido, verificado | 1 | 0 | Habilitado |

- [Válido](01-valid-before-save.png)
- [Verificación pendiente](02-checking-before-save.png)
- [Duplicado y contador actualizado antes de resolver](03-duplicate-before-save.png)
- [Detalle compacto con «Ver ítem»](04-duplicate-detail.png)
- [Corregido, aún verificando](05-corrected-checking.png)
- [Corregido y listo sin guardar](06-corrected-before-save.png)
- [Resultados medidos del navegador](browser-results.json)

Limitación visual del arnés: la fuente externa Material Symbols no estuvo disponible; algunos
iconos aparecen como texto. No se modificaron iconos ni estilos del DS.

## Pruebas

- **Antes: FAIL 4/4** regresiones nuevas contra la implementación anterior.
- **Después: PASS 767/767**, 72 archivos (Compras, Ítems, importador, componentes de Ítems y notices).
- Transición válido → duplicado → corregido, incluyendo debounce/consulta y contadores antes
  de guardar; respuestas fuera de orden en ambos sentidos (duplicado obsoleto y éxito obsoleto),
  fallo de consulta y ausencia de llamadas de resolución: **PASS**.
- TypeScript (`tsc --noEmit`): **PASS**.
- Build y ESLint: **PASS**, 0 errores de lint y 35 advertencias existentes.
- `architecture:check`: **PASS**, 0 infracciones nuevas. Diff review y `git diff --check`: **PASS**.
- Logs: [regresión previa](tests-before.txt), [suite posterior](tests-after.txt).

No se ejecutó PostgreSQL: no se modifican backend, transacciones, scope, locks ni persistencia.
Se conservaron los cambios previos del workspace. Sin datos, migraciones, cambios de reglas,
commit, push ni deploy.
