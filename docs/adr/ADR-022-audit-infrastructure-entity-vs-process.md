# ADR-022: Infraestructura de Auditoría por Dominio — Entity Audit (FROZEN) + Process Audit (futuro)

**Estado:** ✅ FROZEN — Contratos, dispatcher y modelo de extensión de Entity Audit cerrados definitivamente. Process Audit queda diseñado y documentado, sin implementar.
**Fecha aprobación:** 2026-07-07
**Autor:** Sebastian Zhinin (decisión tomada en sesión de rediseño + auditoría de estabilidad guiada)
**Contexto:** ERP SaaS multiempresa — auditoría de negocio dejó de escribirse manualmente en una tabla genérica (`UserActivity`) y pasó a un patrón por dominio, event-driven, con infraestructura común reutilizable.

> **FROZEN:** `AuditRecordBase`, `AuditActor`, `AuditSource`, `IAuditEvent`,
> `IAuditWriter<T>`, `IAuditReader<T>`, `IAuditService`, `IAuditContext`,
> `EfAuditWriter<T>`, `EfAuditReader<T>`, `HttpAuditContext`, `AuditService`,
> `ConfigureAuditBase<T>()` y el flujo de dispatch (`AggregateRoot.RaiseDomainEvent`
> → `ErpDbContext.SaveChangesAsync` → Outbox → MediatR `IPublisher` →
> `*AuditHandler` → `IAuditService` → `IAuditWriter<T>`) quedan cerrados. Reglas
> ejecutables completas en [AI-RULES/AUDIT-INFRASTRUCTURE.md](../../AI-RULES/AUDIT-INFRASTRUCTURE.md).

---

## Contexto

Antes de este rediseño, la auditoría de negocio (ej. cambios de precio en
`PricingRule`) se escribía a mano en cada handler de Application contra una tabla
genérica (`UserActivity`), serializando el detalle del cambio como texto libre
(`Description`). Este patrón se repetía handler por handler, sin captura tipada de
valores antes/después, y con un antipatrón N+1 al resolver "última modificación"
fila por fila (`GetPricingRulesHandler`).

Se diseñó e implementó una infraestructura común (contratos + dispatcher +
helpers, sin conocimiento de ningún módulo) más un patrón por dominio (una tabla de
auditoría tipada por cada agregado auditado, sin columnas opcionales de otros
dominios — "God table" prohibida). Pricing (`PricingRule`, `PriceListItem`) sirvió
de piloto de referencia.

Tras implementar el piloto, se realizó una auditoría de estabilidad crítica sobre
la infraestructura completa, asumiendo que sería reutilizada por Pricing,
Inventory, Sales, Purchasing, Accounting, Cash & Banks, Electronic Documents, CRM,
Assets, Manufacturing y RRHH durante varios años. Esa auditoría es la base de las
decisiones documentadas en esta ADR.

---

## Decisión

### 1. Entity Audit queda FROZEN como estándar oficial

Los contratos y el flujo de dispatch listados arriba quedan cerrados. Todo dominio
nuevo agrega **únicamente**: su entidad de auditoría (heredando `AuditRecordBase`),
sus domain events, y su `*AuditHandler` — sin tocar la infraestructura común. Ver
detalle operativo completo en `AI-RULES/AUDIT-INFRASTRUCTURE.md`.

### 2. Process Audit queda diseñado, no implementado

Se documenta oficialmente una segunda categoría de auditoría — **Process Audit** —
para procesos completos del ERP sin una única entidad de negocio como sujeto
(importaciones masivas, recálculos, cierres contables/diarios, conteos físicos,
recosteo, sincronización SRI, facturación masiva, generación de asientos, backups,
jobs de Hangfire, provisionamiento SaaS, migraciones, ETL, integraciones externas).

**Hallazgo clave que hace posible esta decisión sin romper compatibilidad:**
`EntityId` en `AuditRecordBase` no está semánticamente atado a "una fila de una
tabla de negocio" — es, en realidad, "el identificador de la cosa auditada". Una
corrida de proceso (`ProcessRunId`, un `Guid` generado al iniciar el proceso) es un
`EntityId` perfectamente válido bajo el contrato ya existente. Esto significa que
Process Audit se implementará en el futuro **reutilizando exactamente los mismos
contratos** (`AuditRecordBase`, `IAuditWriter<T>`, `IAuditReader<T>`,
`IAuditService`), sin necesidad de modificarlos — solo agregando:

1. Un pseudo-agregado de proceso (uno por tipo de proceso) que levanta domain
   events de ciclo de vida (`Started`/`Completed`/`Failed`).
2. Una entidad `XxxProcessAudit : AuditRecordBase` con campos propios de proceso
   (`ProcessName`, `StartedAtUtc`, `FinishedAtUtc`, `DurationMs`,
   `RecordsProcessed`, `ErrorCount`, `ResultStatus`).
3. Un `XxxProcessAuditHandler` que traduce el evento — mismo patrón que
   `PricingRuleAuditHandler`.
4. Una implementación nueva de `IAuditContext` para contextos no-HTTP (jobs,
   migraciones, integraciones) — una clase nueva, no una modificación de la
   interfaz existente.

### 3. Regla de no-mezcla

Entity Audit y Process Audit son complementarias, no sustitutas. Un proceso masivo
que modifica entidades individuales (ej. recalcular 25.000 precios) genera **una**
fila de Process Audit (la corrida completa) **y** sigue generando **una fila de
Entity Audit por cada entidad modificada individualmente**, si esa modificación
pasa por el mismo camino de dominio (`PricingRule.UpdateRule()`) que un cambio
manual. Un proceso masivo no es una excusa para saltarse la auditoría de entidad.

### 4. Regla Open/Closed explícita

La infraestructura de auditoría queda **abierta para extensión, cerrada para
modificación**. Los nuevos dominios (Entity Audit o Process Audit) agregan
consumidores nuevos. Ninguno modifica `AuditRecordBase`, `IAuditWriter<T>`,
`IAuditReader<T>`, `IAuditService`, `IAuditContext`, `AuditActor`, `AuditSource` ni
`IAuditEvent`. Cualquier necesidad real de cambiar esas firmas requiere una nueva
ADR — no una extensión "de paso" dentro de un dominio de negocio.

---

## Deuda técnica conocida (no bloquea el freeze del contrato)

La auditoría de estabilidad previa detectó tres defectos de **implementación
concreta** de los dos consumidores ya construidos (Pricing) — no de la forma del
contrato:

1. ~~`CurrentUserService.Email`/`FullName` devuelven `null` hardcodeado~~ —
   **RESUELTO 2026-07-07.** `AccessTokenService` embebe `ClaimTypes.Email`/
   `ClaimTypes.GivenName` en el JWT al emitirlo (snapshot al momento de
   login/refresh); `CurrentUserService` las lee. `AuditActor.UserName` es ahora
   `string` no-nullable, con fallback `"Unknown"` en `HttpAuditContext` (con log
   de advertencia) y en `AuditRecordBase.SetCommon` como última defensa.
   `AuditActor` se amplió (additive) con `FullName`/`Email`/`RoleName` opcionales
   para no tener que volver a tocar el contrato si se necesita mostrar
   "Nombre (Rol)" en el futuro. Columna `user_name` migrada a `NOT NULL`
   (`MakeAuditUserNameRequired`, con backfill defensivo). Confirmó el fix sin
   tocar ningún contrato FROZEN — solo se amplió un value object (`AuditActor`)
   de forma aditiva y se corrigieron dos clases concretas (`CurrentUserService`,
   `AccessTokenService`). Cobertura: `AccessTokenServiceTests` (JWT embeds
   claims) + aserciones de `UserName` en las suites de integración de Pricing.
2. `HttpAuditContext.Actor.Source` está hardcodeado a `AuditSource.UserAction` — no
   existe todavía una implementación de `IAuditContext` para jobs/sistema. Se
   resuelve agregando una implementación nueva (ver Decisión, punto 2.4), no
   modificando la interfaz. **Pendiente.**
3. `CorrelationId`/`RequestId` no se validan/truncan antes de persistir en columnas
   `varchar(100)` — un header `X-Correlation-Id` fuera de rango puede abortar la
   transacción de negocio completa. Fix interno de truncado, no toca la forma del
   contrato.

Estos tres puntos se documentan como remediación obligatoria de corto plazo, fuera
del alcance de esta ADR (que cierra el **contrato**, no certifica que los datos ya
capturados sean confiables para decisiones de negocio hasta que se corrijan).

---

## Alternativas consideradas

| Alternativa | Razón de descarte |
|---|---|
| **Tabla genérica única con `EntityType` + columnas opcionales por dominio (patrón SAP CDHDR/CDPOS)** | Universal y simple de consultar de forma cruzada, pero degenera en "God table" a medida que crecen los dominios (11 módulos futuros); dificulta índices eficientes por dominio; contradice la restricción explícita de este rediseño. |
| **Un `EntityId` opcional/nullable en `AuditRecordBase` desde el día 1, pensando ya en Process Audit** | Se descartó para el cierre de Entity Audit: introduce ambigüedad en el contrato ya usado por Pricing (¿qué significa una fila sin `EntityId`?) sin necesidad — el hallazgo de que `ProcessRunId` es un `EntityId` válido resuelve el caso sin relajar la restricción `NotEmpty`. |
| **Un `IProcessAuditWriter`/`IProcessAuditReader` totalmente paralelo, sin relación con `AuditRecordBase`** | Duplicaría la implementación genérica (`EfAuditWriter`/`EfAuditReader`) sin necesidad — el modelo "proceso como pseudo-entidad" permite reutilizar el 100% de la infraestructura ya construida. |
| **No declarar FROZEN todavía, esperar a tener 3+ dominios de Entity Audit implementados** | Se descartó: los contratos ya demostraron ser suficientemente genéricos con dos dominios muy distintos entre sí (`PricingRule` con campos old/new tipados, `PriceListItem` sin ningún campo de valor) sin requerir ningún cambio de forma entre ambos — evidencia suficiente de estabilidad del contrato. |

---

## Consecuencias

### Positivas

- Cero duplicación de lógica de persistencia/lectura entre dominios — un único
  `EfAuditWriter<T>`/`EfAuditReader<T>` genérico sirve a todos.
- Ningún dominio futuro necesita diseñar su propia estrategia de auditoría desde
  cero — solo sigue el checklist de la sección 2 de `AI-RULES/AUDIT-INFRASTRUCTURE.md`.
- Process Audit tiene un camino de implementación claro y sin fricción
  arquitectónica, validado conceptualmente antes de que exista la primera
  necesidad real (evita el rediseño reactivo).
- La auditoría de estabilidad crítica previa a este cierre evitó congelar
  silenciosamente tres defectos de implementación como si fueran parte del
  contrato — quedan documentados como deuda técnica explícita, no como sorpresas
  futuras.

### Limitaciones y riesgos residuales

- Los defectos restantes (2 y 3) de la sección "Deuda técnica conocida" deben
  remediarse antes de usar los datos de auditoría para decisiones de negocio que
  dependan de ellos (ej. distinguir auditoría de usuario de auditoría de job de
  sistema, o tolerar correlationIds arbitrariamente largos sin abortar la
  transacción). El defecto 1 (`UserName` siempre `null`) ya quedó resuelto — un
  reclamo de cliente o una auditoría legal sobre "quién cambió este precio" ya
  puede responderse con el nombre visible del usuario, no solo con su Guid.
- No existe todavía ningún endpoint/API que exponga el historial completo de
  auditoría a un usuario final — los datos existen pero no son consultables fuera
  de la base de datos o de los campos resumen (`LastModifiedAt`/`LastModifiedByName`)
  ya expuestos en Pricing.
- No hay política de retención/particionado implementada — aceptado como
  P4/roadmap, igual que el Outbox (ADR-011).
- No hay gate de arquitectura automatizado (tipo `ATT-GATE-01`/`SEQ-GATE-0x`) que
  impida a un futuro dominio inyectar `IAuditWriter<T>` fuera de un event handler.
  Queda como mejora futura opcional, no bloqueante para este cierre.

### Consideraciones para evolución futura

- La primera implementación real de Process Audit debe seguir exactamente el
  patrón de la sección "Decisión, punto 2" de esta ADR y actualizar
  `AI-RULES/AUDIT-INFRASTRUCTURE.md` con el primer ejemplo concreto — sin abrir
  una nueva ADR, salvo que se descubra que el modelo "proceso como pseudo-entidad"
  no alcanza (en cuyo caso sí se requiere una ADR nueva, dado que sería un cambio
  de diseño, no una extensión).
- Cualquier necesidad de cambiar la forma de `AuditRecordBase` o de los contratos
  de la sección "Decisión, punto 1" requiere una nueva ADR aprobada y, si aplica,
  una migración de todas las tablas de auditoría ya existentes.

---

## Addendum (2026-07-07): AuditActor como único modelo del actor + corrección de claim

Refinamiento posterior al cierre inicial de esta ADR, sin reabrir ningún contrato:

1. **Corrección de claim.** La primera implementación embebía `IdentityUser.FullName`
   bajo `ClaimTypes.GivenName` — semánticamente incorrecto (`GivenName` es "solo el
   nombre", no el nombre completo). Se corrigió a `ClaimTypes.Name`.
   `CurrentUserService.FullName` mantiene un fallback transitorio a `GivenName`
   exclusivamente para no invalidar tokens ya emitidos antes de la corrección
   (expira solo por el vencimiento natural de esos tokens). Ningún token nuevo
   emite `GivenName`.
2. **`AuditActor` confirmado como único modelo oficial del actor** — ver regla
   Open/Closed nueva más abajo y detalle operativo en
   `AI-RULES/AUDIT-INFRASTRUCTURE.md` secciones 8-9.
3. **Cero cambios de forma incompatibles**: `AuditActor` sigue siendo el mismo
   `record struct` (ampliado de forma aditiva en el cierre anterior con
   `FullName`/`Email`/`RoleName`); `IAuditWriter<T>`, `IAuditReader<T>`,
   `IAuditService`, `IAuditContext`, el dispatcher, CQRS y Domain Events no se
   tocaron.

## Regla arquitectónica permanente

> La infraestructura de auditoría (Entity Audit y su extensión futura, Process
> Audit) queda abierta para extensión y cerrada para modificación. Todo dominio
> nuevo agrega su propia entidad de auditoría, sus propios domain events y su
> propio handler — nunca modifica `AuditRecordBase`, `IAuditWriter<T>`,
> `IAuditReader<T>`, `IAuditService`, `IAuditContext`, `AuditActor`, `AuditSource`
> ni `IAuditEvent`. Queda prohibido agregar lógica de auditoría directamente en
> Controllers, Handlers de negocio, Repositories o Frontend — toda auditoría nueva
> se integra exclusivamente mediante esta infraestructura oficial. Cualquier
> necesidad de auditoría de procesos se implementa como extensión independiente de
> Process Audit, sin modificar la infraestructura base de Entity Audit.
>
> **Toda información relacionada con el actor que ejecutó una operación auditada
> vive exclusivamente dentro de `AuditActor`.** Queda prohibido agregar columnas
> de identidad del usuario (nombre, email, rol o variantes) directamente en las
> entidades de auditoría de cada dominio (`PricingRuleAudit`, `PriceListItemAudit`
> o cualquier futura `XxxAudit`). Cualquier evolución sobre "qué se sabe del
> actor" se realiza únicamente mediante una extensión aditiva y controlada de
> `AuditActor`. `AuditActor` es un snapshot histórico inmutable: se calcula una
> vez en el momento del evento y nunca se recalcula, resincroniza ni actualiza.
> Estas reglas deben considerarse permanentes.
