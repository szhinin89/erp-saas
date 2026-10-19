# Accounting — SSOT operativa

Única especificación funcional de Accounting. Describe exclusivamente el comportamiento
implementado y respaldado por pruebas
([regla SSOT](enforcement.md#ssot-por-módulo)). No requiere consultar ADRs ni otros
documentos para determinar qué regla contable aplica. Toda modificación funcional se
incorpora aquí en la misma fase; los ADRs solo registran razones arquitectónicas y los
documentos de avance solo resumen estado.

El sistema está en preproducción. No existe compatibilidad con políticas contables anteriores.

## Estado real de implementación

| Clasificación | Implementado |
|---|---|
| KEEP | Core, plan de cuentas, reglas, períodos, motor de partida doble, idempotencia, numeración, lectura y reportes del libro implementados |
| IMPLEMENTADO | ACCOUNTING-CONTINUITY-05: costo de ventas (reconocimiento, cobertura, devolución) sobre la obligación común; apertura bajo la política operativa con su modelo propio; Disabled operativo con precondiciones (§20) |
| IMPLEMENTADO | ACCOUNTING-CONTINUITY-01: política operativa versionada por Company, obligación contable durable común con historial de intentos, intento inicial posterior al commit y reproceso manual individual para Compra, Gasto (confirmación de borrador y creación directa) y Retención emitida; anulación común de esos orígenes; gate mínimo de cierre; `AccountingPostingMode` retirado |
| IMPLEMENTADO | ACCOUNTING-CONTINUITY-04: Venta, devolución de venta, pago a proveedor, cobro, devolución de compra, NC compra Discount y aplicación/reembolso de saldo a favor registran la obligación durable; anulación de devolución de compra y reversas de aplicación/reembolso usan el reverso histórico común (§19) |
| KEEP | ACCOUNTING-CONTINUITY-03: espejo histórico, original inmutable, fecha efectiva y período Open destino, idempotencia y obligación durable para los consumidores migrados |
| IMPLEMENTADO | ACCOUNTING-CONTINUITY-06: readiness único de completitud (cierre, pre-cierre y reportes), conciliación de orígenes, apertura/ASI, costo pendiente, cierre secuencial y gate Closed/Locked en el origen (§21) |
| IMPLEMENTADO | ACCOUNTING-07: estructura temporal de períodos (contigüidad, orden frente a Closed/Locked, exclusión PostgreSQL de solape), ejercicios mensuales desde 2027 con generación manual, cierre solo después de `EndDate`, Locked auditivo irreversible, permisos dedicados, Entity Audit y pantalla de Períodos (§22) |
| IMPLEMENTADO | ACCOUNTING-08: asientos manuales Draft/Posted/Discarded, edición versionada, publicación con intentos durables, reverso histórico común, permisos dedicados, Entity Audit y UI sobre el libro existente (§11) |
| IMPLEMENTADO | ACCOUNTING-11: catálogo único de hechos contables soportados, administración de reglas desde UI (`accounting.rules.manage`) y Balance General con resultados no cerrados calculados (§23) |
| IMPLEMENTADO | ACCOUNTING-12A: Estado de Resultados con signo por sección (coincide con el Resultado del ejercicio del Balance) y contabilización de ajustes de inventario `Inventory/StockAdjustmentIn` y `StockAdjustmentOut` con cuenta del motivo (§24) |
| IMPLEMENTADO | ACCOUNTING-12C: Caja contable — movimientos manuales con cuenta del motivo, depósito bancario explícito y diferencia de arqueo al cerrar (§25) |
| IMPLEMENTADO | ACCOUNTING-12D: cierre anual — asiento de cierre generado en Draft desde Períodos, publicado con el flujo de manuales, y `YEAR_END_CLOSING_MISSING` en el último período (§26) |

El resto del documento define el contrato vigente de lo implementado. Las prohibiciones
(§15) y los límites de alcance describen lo que el sistema no hace; no son capacidades pendientes.

## 1. Propósito y límites

Accounting registra hechos económicos y permite operar el libro independientemente de
Compras/Ventas: plan de cuentas, asientos manuales, ajustes, reversos, períodos y reportes.
Los documentos operativos aportan importes ya determinados. Accounting no recalcula
precios, descuentos, impuestos, cantidades ni valoración de inventario; tampoco decide
elegibilidad tributaria, autorización SRI, disponibilidad o saldo operativo.

El núcleo existente se conserva: `Account`, `PostingRule`, `AccountingPeriod`,
`JournalEntry`, `JournalEntrySequence`, `PostingFact`, `IPostingEngine`, motor genérico,
partida doble, idempotencia, correcciones mediante nuevos asientos y reportes del libro.
No se crea una arquitectura contable paralela.

## 2. Modelo conceptual y fuentes de verdad

| Concepto | Autoridad única |
|---|---|
| Obligación fiscal | `Company.IsAccountingReq` existente |
| Uso operativo y vigencia | Decisión Accounting Enabled/Disabled por Company, separada de la fiscal |
| Hecho económico e importes | Módulo origen; evidencia durable con identidad y datos necesarios para contabilizar |
| Cuentas y configuración de posting | `Account` y `PostingRule` de la misma Company |
| Resultado pendiente/fallido | Obligación contable durable, nunca el log ni `OutboxMessage.ProcessedOnUtc` |
| Libro e importes históricos | `JournalEntry` y sus líneas; el reverso referencia el original |
| Período | `AccountingPeriod` que contiene la fecha contable efectiva |
| Numeración | `JournalEntrySequence`; no secuencia fiscal SRI |
| Costo operativo | Inventory/Kardex y obligaciones de valoración descritas en §10 |

La obligación durable común es `AccountingPosting`, con su historial de intentos en
`AccountingPostingAttempt`. La apertura conserva su modelo propio
(`OpeningBalancePosting` y `OpeningJournalEntryPosting`); `InventoryCostPosting` se retiró (§27).
Cada efecto tiene una sola identidad y una sola obligación autoritativa.

### Core y configuración contable

`Account` tiene código, nombre, padre, tipo, naturaleza, estado activo y AllowsPosting.
Los tipos son Asset, Liability, Equity, Income, Cost y Expense (Cost = costo de ventas, separa
utilidad bruta de neta); no son catálogos tributarios
ni decisiones por tenant. Código/tipo/naturaleza se conservan tras crear. La jerarquía
no admite ciclos ni padres de otra Company. El motor exige cuentas existentes, de la
misma empresa, activas y con AllowsPosting. No se introduce aquí una regla nueva que
deduzca AllowsPosting exclusivamente de tener o no hijos. La baja es lógica y no borra
líneas ni historia; los cambios de configuración solo afectan contabilizaciones nuevas.

`PostingRule` es configuración por Company y tipo de hecho, con líneas de cuenta,
naturaleza y componente monetario. Solo existen reglas para los hechos del catálogo único
`PostingFactCatalog` (§23); se administran desde la UI. El resolver trabaja por datos; el motor genérico
no agrega ramas por documento. Los traductores convierten hechos ya resueltos en
PostingFact. Los montos, descuentos e impuestos provienen del origen y las líneas
efectivas de la regla; no se resuelven desde campos legacy de débito/crédito planos.

Un asiento debe balancear, tener líneas monetarias válidas y cuentas verificadas;
en cada línea exactamente uno de Debe/Haber es positivo. No publicar encabezados
vacíos ni asientos parciales. La precisión monetaria es `AccountingDecimals` de la política única de precisión
([data-standards.md](data-standards.md#estándar-de-precisión-numérica)).
JournalEntrySequence reserva número por Company y ejercicio bajo lock transaccional
PostgreSQL; no reutiliza punto de emisión ni secuencia de comprobante SRI.

## 3. Configuración por Company: fiscal frente a operativa

Todo dato contable está aislado por Tenant + Company, resueltos en servidor desde
el contexto autenticado. Branch es trazabilidad operativa, no un libro compartido.

| `IsAccountingReq` | Decisión operativa | Resultado |
|---|---|---|
| true | Obligatoria | Enabled; no se permite deshabilitar |
| false | Uso voluntario | Enabled |
| false | Sin uso contable | Disabled |

No crear otro booleano de obligación fiscal. La preferencia operativa es una política
versionada por Company (`accounting_operational_policies`: modo, `EffectiveFrom` y
`EffectiveTo` inclusivos; una versión nueva cierra la abierta el día previo, sin solapes ni
borrado). Un hecho está sujeto a Accounting si una versión Enabled cubre su fecha de negocio.
La activación tiene fecha efectiva; no genera asientos históricos automáticamente.
Las empresas existentes al adoptar la política quedaron Enabled desde 1900-01-01. Una empresa
nueva elige su modo explícitamente: sin ninguna versión la decisión no puede tomarse y la
operación que generaría el hecho se rechaza (fail-closed); Accounting nunca se activa de forma
automática. Si existe `Company.OpeningBalanceDate`, la primera versión Enabled debe regir desde esa
fecha o antes (Carga Inicial abierta o cerrada); una vigencia posterior se rechaza, porque dejaría la
apertura no sujeta mientras las operaciones posteriores sí se contabilizan. La pantalla propone la
fecha de apertura cuando existe. Marcar `IsAccountingReq = true` exige Enabled vigente hoy y ninguna versión
posterior Disabled; en otro caso el cambio fiscal se rechaza.
Una desactivación no borra el libro ni resuelve pendientes; se rechaza si hay obligaciones
Pending/Failed o hechos que aún deberían tener resolución. El cambio no exime obligaciones
ya contraídas ni permite suprimir el reverso de asientos existentes.

Disabled no exige plan/reglas/cuentas para continuar la operación comercial y no genera
obligaciones nuevas de posting automático durante su vigencia (precondiciones y vigencia en §20).
La lectura del historial se conserva. El uso voluntario de asientos manuales requiere activar Accounting.

## 4. Pipeline y frontera transaccional

```text
Hecho operativo válido + decisión efectiva por Company
  → misma transacción: núcleo operativo + obligación durable (si Enabled)
  → commit operativo
  → intento inicial de contabilización de la obligación
  → PostingFact → IPostingEngine → reglas/cuentas/período → JournalEntry
  → resultado durable Posted o Failed
  → recuperación posterior: únicamente reproceso manual autorizado
```

Forma física (Compra, Gasto, Retención y los hechos de §19): el traductor del hecho registra la obligación
Pending en la transacción del núcleo, bajo lock compartido por Company y el advisory lock de
idempotencia de la identidad, sin invocar el motor. Tras el commit, el caso de uso ejecuta el
intento inicial de las obligaciones que creó, cada una en su propia transacción (lock de fila,
hecho reconstruido con la configuración vigente, `IPostingEngine`, resultado Posted/Failed e
intento, atómicos con el asiento). Una excepción interna queda Failed `INTERNAL` en una
transacción nueva; si el proceso cae antes del intento, la obligación permanece Pending. El contrato exige que un error
exclusivamente contable no invalide el núcleo comercial y que no se pueda perder la
obligación. No basta capturar una excepción y continuar con un DbContext/transacción
contaminados. Si no se puede persistir núcleo + obligación, no se confirma la operación:
es un fallo de durabilidad del núcleo, no un error de configuración contable tolerable.

El intento inicial no es un reintento. No se introduce aquí un worker, un nuevo Outbox
ni otro pipeline. Un hecho Enabled debe quedar Posted o con obligación Pending/Failed.
El motor conserva fail-closed para el **asiento**: no inventar cuentas, no crear líneas
parciales, no aceptar desbalance; ese rechazo se registra sin rollback comercial.

## 5. Obligación durable y estados

Datos mínimos: Tenant, Company, módulo/tipo de origen, documento y hecho identificables,
fecha de negocio, fecha contable efectiva, tipo de hecho, importes/datos operativos
necesarios, estado, código/mensaje de error, intentos, último intento, autor/correlación
y asiento generado si existe. Conservar referencias de destino caja/banco/medio de pago
para resolver sus cuentas con configuración vigente; no congelar un error de mapeo.
El payload es versionado (versión 2; la versión 1 se sigue leyendo) y contiene solo datos del
hecho: importes y componentes, destinos financieros por identidad y referencias de origen,
nunca cuentas ni reglas (salvo el asiento original de un reverso).

| Estado | Significado y transición |
|---|---|
| Pending | Compromiso persistido aún sin resolución; intento inicial o manual → Posted/Failed |
| Failed | Intento fallido, error e intentos durables; solo acción manual autorizada → Posted/Failed |
| Posted | Asiento persistido y enlazado, salvo resolución de importe cero expresamente justificada |
| Canceled | Solo si se extinguió el efecto antes de publicarse; no oculta un fallo ni sustituye un reverso |

Si falla la persistencia del resultado de un intento, la obligación durable sigue sin
resolver y el reproceso debe descubrir el asiento por su identidad antes de crear otro.
No marcar Posted por haber emitido un evento o escrito un warning.

Anular un origen sin asiento cancela sus obligaciones todavía no publicadas cuando
semánticamente ya no existe efecto que reconocer; no fabricar un reverso de un asiento
inexistente. Si hubo reconocimiento parcial por hechos distintos, reversar lo efectivamente
publicado y cancelar lo no publicado, conservando la trazabilidad de cada hecho.

## 6. Errores, reproceso manual e idempotencia

No hay reintento contable automático: ni cron, ni backoff, ni redespacho de eventos,
ni una operación comercial posterior que vuelva a ejecutar Failed de forma implícita.
La recuperación fiscal/SRI y Communications mantienen sus políticas propias.

Reproceso individual y en bloque, siempre solicitado por usuario autorizado dentro
de su Tenant/Company (individual y por lote de IDs, §17, con el mismo ejecutor del intento inicial).
Cada obligación tiene aislamiento transaccional: un error no
detiene las demás. Cada intento ejecutado termina Posted o Failed; el lote informa
resultados individuales, no un éxito global que esconda fallos. Las dependencias de
costo se respetan: un ajuste no precede al reconocimiento que necesita; una dependencia
sin resolver deja el intento fallido con causa explícita y permite continuar otros hechos.

Pending/Failed normal usa la configuración contable **vigente al reprocesar** y los datos
económicos persistidos del origen. No recalcula el negocio ni utiliza cuentas adivinadas.
Corregir configuración no dispara procesamiento automático.

Conservar la identidad existente del motor `(CompanyId, SourceModule, SourceEventId,
FactType)`, el advisory lock transaccional y el índice único de respaldo. Tenant se
valida en el acceso aunque no integre ese índice existente. La obligación conserva su
identidad al reprocesar; no generar un nuevo SourceEventId por intento. Publicación del
asiento y resolución Posted deben ser atómicas. El reverso tiene identidad única por
asiento original; las ejecuciones concurrentes no pueden duplicarlo.

## 7. Reversos, fecha efectiva e inmutabilidad

Reversar crea un asiento nuevo: mismas cuentas, mismas líneas e importes, con débitos
y créditos intercambiados; conserva referencia al asiento y documento originales y
motivo/autor. No resolverlo mediante reglas vigentes, cuentas sustitutas o valoración nueva.

El original permanece **totalmente inmutable**, también en Open y en Closed/Locked, incluso
su estado y metadatos persistidos. La relación inversa se consulta desde el nuevo reverso
o evidencia append-only (`OriginalJournalEntryId`, único); el original no guarda estado ni
referencia de reverso.
El reverso se registra en el período Open que contiene la fecha efectiva de la
anulación/reversión. No reabrir automáticamente ni elegir arbitrariamente otro período.
Si no existe un período abierto válido para esa fecha, **se bloquea la anulación/reversión**
hasta resolver el período. Esta precondición expresamente aprobada es una excepción
a la continuidad ante errores exclusivamente contables; no convierte otros fallos de
cuentas/reglas en causas de rollback comercial.

Fecha efectiva es la del acto de anular/revertir, no el día del futuro reproceso ni la
fecha del asiento original cerrado. Fecha de negocio usa `DateOnly` y reloj de Company;
instantes de auditoría, UTC. Reprocesar no permite trasladar el hecho a otro período.
No confundir una devolución/NC comercial nueva con el espejo exacto de un asiento.

Para hechos normales se usa la fecha de negocio aportada por el origen: IssueDate
de factura/retención, PaymentDate de pago/cobro, AccountingDate del gasto o EffectiveDate
de la transacción financiera. Si el hecho no trae fecha propia, se fija al crearse
con `ICompanyClock.TodayAsync`; nunca con el día UTC. La obligación conserva esa fecha
durante el reproceso. `TimeProvider` permite fijar el instante en tests de frontera
de mes/año sin cambiar la zona horaria de la empresa.

Los asientos Posted no permiten editar importes/cuentas ni borrarse. Closed/Locked
impide además toda mutación de sus asientos. Las correcciones posteriores son ajustes
o reversos nuevos. En períodos abiertos se conserva la trazabilidad existente sin editar
líneas históricas. El flujo especial de corrección de apertura mantiene sus restricciones
operativas; esta regla no habilita el reverso genérico de una carga inicial.

## 8. Períodos y cierre

Períodos no solapados y contiguos por Company (estructura y periodicidad en §22). Posting únicamente en un período abierto válido
para la fecha del hecho; un error de período de un posting normal deja obligación Failed,
sin mover la fecha ni revertir el núcleo comercial. Para la reversión del apartado 7
se aplica la precondición aprobada antes de anular.

No cerrar con Pending, Failed, documentos que deberían contabilizarse sin resolución,
asientos incompletos o inconsistencias relevantes. La evaluación debe conciliar orígenes,
obligaciones y libro; revisar solo JournalEntry no detecta hechos sin asiento. Considerar
la vigencia Enabled/Disabled para evitar exigir documentos no sujetos a Accounting.
Las incertidumbres de costo descritas en §10 deben resolverse antes de considerar definitivo
el resultado financiero del período; un retry contable no fabrica una base de costo.

El gate de cierre y la creación/publicación de obligaciones concurrentes deben
serializarse en PostgreSQL, evitando cerrar entre la consulta y el commit de un hecho.
Gate implementado (§21): el cierre evalúa el readiness único bajo lock exclusivo por Company
frente al lock compartido de la creación de obligaciones. Un hecho sujeto cuya fecha contable
pertenece a un período Closed/Locked se rechaza en el origen, antes de crear su obligación.
Locked no puede eludir el gate de cierre. Closed/Locked no permiten nuevas escrituras
en ese período ni edición, borrado o reescritura de lo ya registrado.

## 9. Reportes

Importes exclusivamente de JournalEntry/líneas con efecto contable, incluyendo el
original y el reverso según **sus respectivas fechas**. No excluir el original por
haber sido reversado ni forzar neto cero en un rango que solo contiene uno de los dos.
No reconstruir balances desde documentos operativos ni sumar las obligaciones como
si ya fueran asientos.

Balance General: Patrimonio incluye las filas calculadas de resultados no cerrados (§23), así que
el cuadre Activo = Pasivo + Patrimonio se cumple durante el ejercicio y un descuadre es real.

Mostrar advertencia de información incompleta si existen pendientes/fallidos, hechos
sin resolución o incertidumbres relevantes en el alcance del reporte. Permitir consulta
provisional, sin presentarla como estado financiero definitivo. Mostrar fecha de corte
y alcance de Company; los filtros de pantalla no deben ocultar la advertencia de
integridad del alcance financiero consultado.

## 10. Integración y matriz de atomicidad

En todas las filas, Enabled exige obligación durable junto al núcleo; Disabled permite
la operación sin posting nuevo. Fallo del núcleo siempre produce rollback. La columna
de fallo contable presupone que pudo persistirse la obligación. Los gates de negocio,
saldo, inventario y SRI existentes se conservan. No se reabren módulos CLOSED.

| Operación | Núcleo operativo | Accounting Enabled | Fallo contable | Resultado |
|---|---|---|---|---|
| Compra confirmada | Compra + Inventory/Kardex + CxP + retención solicitada válida | Obligaciones de compra y retención | Pending/Failed, sin rollback del núcleo | Confirmada y trazable |
| Compra anulada | Elegibilidad operativa + gate fiscal + reversos Kardex/CxP/retención | Reversar asientos reales; cancelar no publicados | Pending/Failed; gate de período del §7 obligatorio | Anulada o bloqueada por precondición |
| Devolución compra | Devolución + salida al promedio + crédito/CxP | Hecho de devolución con costo persistido del Kardex | Pending/Failed | Devolución autorizada |
| NC compra | Documento comercial/fiscal + aplicación financiera; no duplicar devolución | Efecto propio, sin doble posting del mismo hecho | Pending/Failed | NC válida conservada |
| Venta autorizada | Venta + Stock/Kardex + Caja/CxC + documento | Ingreso/cobro y COGS como hechos distintos | Pending/Failed | Venta autorizada |
| Venta anulada | Elegibilidad + reversos Stock/Caja/CxC/documento | Reversos exactos de lo publicado + cancelación pendiente | Pending/Failed; gate §7 | Anulada o bloqueada por precondición |
| Devolución venta | Documento + cantidades/costo original reconocido + reembolso/CxC + NC | Efectos comerciales y COGS sin duplicarlos | Pending/Failed | Devolución válida |
| Gasto | Gasto + CxP + retención solicitada válida | Obligaciones separadas de gasto y retención | Pending/Failed | Gasto confirmado |
| Pago proveedor | Pago + aplicaciones CxP + Caja/Banco + anticipo SupplierCredit | Aplicado a CxP + anticipo + medios | Pending/Failed; no rechazar por cuenta faltante | Pago registrado |
| Reversa pago | Reversa aplicaciones/medios + anticipo íntegro | Espejo de asiento publicado; cancelar pendiente sin asiento | Pending/Failed; gate §7 | Pago reversado o bloqueado por precondición |
| Cobro cliente | Cobro + aplicaciones CxC + Caja/Banco | Obligación con destino real del cobro | Pending/Failed | Cobro registrado |
| Reversa cobro | Cobro reversado + saldos CxC/Caja/Banco | Reverso exacto si publicado | Pending/Failed; gate §7 | Reversado o bloqueado por precondición |
| Retención emitida | Origen confirmado + documento tributario + CxP retenida | Obligación Retentions/DocumentIssued | Pending/Failed; no rollback por posting | Retención válida conservada |
| Retención anulada | Anulación por origen + gate SRI + ReverseRetention | Reverso de lo realmente publicado | Pending/Failed; gate §7 | Retención anulada o bloqueo legítimo |
| Costos inventario | Kardex + SaleCostObligation/Allocation; costo operativo resuelto | Obligación común por hecho de costo, anclada a la factura; reconocimiento antes de ajustes (§20) | Failed durable; sin retry implícito | Operación y costo trazables |
| Carga inicial | Lote de saldos + Inventario/CxC/CxP atómicos | Modelo propio OpeningBalancePosting/ASI si la fecha de apertura está sujeta; no reconstruir compras/ventas históricas | Failed sin deshacer lote | Apertura incompleta hasta conciliar; cierre propio protegido |
| Asiento manual | Solo libro, sin documento comercial ni saldo de submayor | Requiere Enabled; líneas explícitas y guardas comunes | No publicar asiento inválido; resultado durable del intento | Posted o Pending/Failed; no hay operación comercial que conservar |
| Reembolso crédito proveedor / reversa | SupplierCredit + Caja/Banco y sus restricciones | Obligación o espejo exacto del publicado | Pending/Failed; gate §7 al reversar | Núcleo financiero conservado |
| Movimiento de Caja/Banco con efecto contable | Movimiento financiero válido y saldo | Obligación del efecto propio, sin duplicar venta/pago/cobro | Pending/Failed | Movimiento operativo conservado |

Fuentes de cuentas conservadas: efectivo → caja real; transferencia → banco real;
tarjeta/cheque → configuración del medio; crédito → regla CxC. No usar una cuenta
general de fallback. La falta de cuenta es error contable recuperable, no invalida
una caja/banco/medio operacionalmente válidos. Efectivo y transferencia no consultan
PaymentMethodAccount ni tienen fallback a esa tabla; esta configuración corresponde
exclusivamente a tarjeta/cheque. El crédito de la factura se reconoce como CxC; su
cobro posterior es otro hecho, sin volver a reconocer el ingreso de la venta.

### Importes de pagos, costos y apertura

- Pago a proveedor: Debe por importe realmente aplicado a CxP + Debe por remanente
  en Anticipos/SupplierCredit; Haber por los medios reales. El crédito creado desde
  el pago no genera otro asiento por el mismo anticipo. Aplicaciones, reembolsos y
  reversos son hechos separados; la reversa operativa exige anticipo aún íntegro.
- Reembolso de crédito de proveedor: Debe Caja/Banco real y Haber Anticipos; su reversa
  espeja el asiento publicado (§7). No depende de una cuenta genérica. La cuenta guardada en
  la transacción de reembolso es solo referencia histórica opcional, no autoridad contable.
- Inventory determina el costo por promedio ponderado y conserva la incertidumbre
  cuando no existe base conocida: null no significa cero. Sin costo conocido se
  mantiene SaleCostObligation; nunca inventar COGS desde el precio de venta. Con base
  conocida se reconoce costo provisional, y la compensación con costo real genera
  ajustes append-only. SaleCostAllocation conserva cantidades/costos y prioridad por
  secuencia del recurso Company + Item + Warehouse; no se reescribe Kardex histórico.
- Devolución de venta: unidades sin costo reconocido no revierten COGS inexistente;
  unidades regularizadas revierten el costo efectivamente reconocido, incluyendo sus
  ajustes y remanentes monetarios. La anulación revierte lo publicado y extingue lo
  pendiente. La devolución no usa indiscriminadamente el promedio vigente.
- Devolución de compra: NC comercial/fiscal prorrateada de la factura original;
  inventario sale al promedio vigente en unidad base. HistoricalCostTotal procede
  del valor real retirado por Kardex; CostVarianceTotal = HistoricalCostTotal −
  AuthorizedSubtotal se reconoce con componentes de diferencia de costo configurados.
  La cancelación conserva cantidad base y costo de la salida registrada. Cancel de
  compra completa conserva su costo histórico y sus precondiciones operativas: una
  salida efectiva posterior del mismo Item/Bodega lo bloquea incluso si se revirtió;
  una entrada posterior sola no lo bloquea. No sustituir Cancel por devolución/NC.
- Carga inicial: lotes de inventario/CxC/CxP representan saldos netos al corte, no
  compras/ventas históricas ficticias. La contabilización por lote usa los importes
  persistidos y la cuenta puente de apertura; Failed no deshace la carga. No reversar
  un lote desde el comando genérico si dejaría el submayor sin contrapartida.
- ASI de apertura: asiento específico con líneas explícitas, sin crear submayores;
  permite reclasificar el puente y excluye cuentas de control Inventario/CxC/CxP.
  La corrección especial publica versiones, sin editar líneas históricas; solo procede
  si todos los períodos desde apertura hasta hoy están abiertos. Con un período
  cerrado se conserva la apertura y se corrige mediante ajuste posterior. Cierre de
  carga exige lotes resueltos/conciliados, ASI vigente Posted, puente cero y ningún
  lote en curso. Cerrada la carga no se publican/corrigen saldos iniciales ni su ASI.

## 11. Asientos manuales y ajustes

Accounting puede usarse voluntariamente sin documentos comerciales. Alcance canónico:
asiento manual con fecha, concepto, líneas Debe/Haber y cuentas explícitas; ajuste como
nuevo asiento con motivo/referencia; reverso de un asiento existente; administración
de períodos y reportes. Usar las guardas comunes de Company, cuentas, balance,
período, numeración, idempotencia y persistencia. No traducir una compra/venta ficticia
ni exigir PostingRules de documentos para un asiento manual de líneas explícitas.

La capacidad general de ACCOUNTING-08 es independiente del ASI de apertura existente.
No habilitar desde un asiento manual cambios de stock, CxP/CxC, caja o documentos SRI.
Su corrección no altera el origen operativo. No se añade workflow de aprobación,
consolidación ni multimoneda en esta fase.

**Identidad y estados.** El manual utiliza `SourceModule = Accounting`,
`SourceEventType = ManualJournalEntry` y `SourceEventId = IdempotencyKey` de creación.
No utiliza el código ASI ni una PostingRule documental. La unicidad existente es la autoridad
de identidad. Repetir la creación devuelve la identidad y respuesta inicial sin sobrescribir datos.
Se crea Draft sin número; `Version` empieza en 1 y aumenta al editar, publicar o descartar.
La edición requiere la versión esperada; una versión vieja devuelve conflicto sin overwrite.
No se limita al creador. Solo Manual + Draft admite edición/descarte/publicación.
`Discarded` es terminal, histórico, con motivo/usuario/instante UTC; no tiene efecto contable
ni bloquea el cierre. No existe estado `Reversed` (§27): un original reversado sigue Posted y su
reverso Posted lo referencia.

**Captura.** Fecha contable, concepto obligatorio (500), referencia opcional (500) y líneas
cuenta/descripción/Debe/Haber. Draft admite fecha futura, menos de dos líneas y desbalance.
Cada línea exige cuenta existente de Tenant + Company, importes no negativos y exactamente
una cara positiva; no admite líneas vacías, escala excedida ni importes fuera de `numeric(18,2)`.
Una cuenta no puede aparecer en ambos lados del asiento, aunque esté en líneas distintas;
puede repetirse en el mismo lado. La precisión viene de la política canónica. Las validaciones
de líneas, mínimo, exclusión de lados y balance tienen autoridad única en Domain, compartida
con el pipeline automático, sin excepción para manuales.

El período del Draft puede ser nulo cuando aún no existe uno para su fecha; no se inventa
un período. Crear/editar/descartar no permite escribir sobre una fecha de período Closed/Locked.
Editar verifica tanto la fecha anterior como la nueva. El cierre reconoce los manuales por
fecha además del vínculo de período, de modo que un Draft anterior a la creación de su período
también bloquea el cierre hasta publicarse o descartarse.

**Publicación.** Bajo transacción propia, lock compartido Company y lock de mutación por identidad:
Draft y versión esperada, `EntryDate <= ICompanyClock.TodayAsync`, Enabled en EntryDate,
período que contiene la fecha Open, cuentas activas y habilitadas para movimiento, mínimo dos
líneas y balance exacto. Reutiliza PostingAccountGuard/PostingPeriodGuard, Domain y
JournalEntrySequenceRepository; no llama al motor basado en reglas documentales ni crea otro motor.
Número, Posted, Entity Audit e intento exitoso se confirman atómicamente.

**Intentos.** `ManualJournalAttempt` es la autoridad específica append-only de cada solicitud
de publicación identificada: asiento, usuario/instante UTC, key, versión esperada, resultado,
código/mensaje y snapshot lógico de respuesta. Un rechazo conserva Draft, sin número ni
asiento parcial, y persiste el intento. Repetir key + versión devuelve el resultado durable;
usar esa key con otra versión devuelve conflicto. Un nuevo intento explícito usa nueva key.
Ante excepción de persistencia se revierte y limpia el contexto; otra transacción registra
el fallo o recupera un resultado ya confirmado. Si tampoco puede persistirse ese resultado,
la operación falla: no se afirma durabilidad inexistente. No hay retry automático.

**Reverso y auditoría.** Solo manual Posted, con fecha efectiva y motivo obligatorio;
reutiliza JournalEntryReversal y su unicidad/locks, período Open y numeración destino.
No exige configuración actual de cuentas/reglas para reconstruir el espejo histórico.
Entity Audit existente (`JournalEntryAudit`) conserva Created, Edited, Posted, Discarded y
Reversed con snapshot de cabecera/líneas y actor/correlación; no hay auditoría paralela.

**API/UI.** Bajo `/api/v1/accounting/manual-journal-entries`: POST de creación, PUT `/{id}`,
POST `/{id}/post`, `/{id}/discard` y `/{id}/reverse`. Conserva los GET del libro; el detalle
agrega referencia, versión, autoría, descarte e intentos para manuales. El listado admite filtro
`manual=true/false` (incluye sus reversos en Manual). UI: listado existente con nuevo manual;
editor `/accounting/journal-entries/new` y `/:id/edit`, inputs decimales canónicos, totales y
diferencia, guardar/publicar; detalle con acciones, intentos y relación de reverso.
Los manuales no muestran una advertencia de documento comercial faltante.
Draft/Discarded quedan fuera de las consultas actuales de reportes; Posted y su reverso entran
según sus fechas propias, sin una lógica de reportes nueva.

## 12. Outbox y mecanismos existentes

`ErpDbContext.SaveChangesAsync` publica Domain Events in-process y persiste el Outbox
con los datos de negocio: primera escritura, publicación in-process, segunda escritura
de efectos y commit en la misma transacción. El `OutboxProcessor` actual **solo marca procesado**; no
republica eventos hacia Accounting ni garantiza recuperación del posting. Un mensaje
procesado no demuestra que exista JournalEntry. Los jobs de SRI y Communications
tampoco son un pipeline de recuperación contable.

El costo de ventas usa la obligación común (§20); `inventory_cost_postings` se retiró (§27) y la
recuperación implícita de Failed también. Apertura conserva su modelo propio
(OpeningBalancePosting/ASI) como única autoridad de su efecto; no hay segundo libro ni cola paralela.

## 13. DocumentFlowPolicy.AccountingPostingMode

Auditoría focal: el enum (`None`, `OnConfirmation`, `OnAuthorization`) se almacena,
siembra, devuelve y actualiza; no tiene una lectura decisoria en los traductores/motor.
La mención en Gastos es un comentario, no autoridad ejecutable.

Retirado en ACCOUNTING-CONTINUITY-01 (dominio, contratos, configuración, seed, UI, tests y
columna), sin conservar semántica legacy. No convertirlo en segundo
interruptor de Accounting. La autoridad de uso es Company + vigencia; el hecho de
negocio de cada módulo determina cuándo nace el efecto. `None` no exime un hecho
contable Enabled.

## 14. Permisos y auditoría

Denegar por defecto. Consultar libro/reportes, configurar cuentas/reglas, cambiar uso
operativo, reprocesar individual/en bloque, registrar manuales/ajustes, reversar y
cerrar/bloquear son capacidades autorizadas en servidor. Reutilizar catálogo IAM
vigente y extenderlo donde falte; no inventar nombres de permisos como si ya existieran.
El permiso del origen sigue gobernando el acto comercial y no concede administración
de Accounting. DocumentFlowPolicy no es un permiso.

Evidencia actual: AccountingController usa `AccountingPermissions.View/Create/Update/Delete`
para sus consultas y administración, salvo Períodos (`accounting.periods.create` /
`accounting.periods.close`, §22) y Reglas contables (`accounting.rules.manage`, §23); `accounting.settings.update` cambia el modo operativo,
`accounting.postings.view` consulta obligaciones e intentos y `accounting.postings.reprocess`
ejecuta el reproceso manual individual y en lote, también para el costo de ventas. La apertura usa sus
permisos combinados existentes. ACCOUNTING-08 agrega `accounting.manual.create`,
`accounting.manual.edit`, `accounting.manual.post`, `accounting.manual.reverse` y
`accounting.manual.discard`, exigidos individualmente en servidor y registrados como acciones
relacionadas del NavItem Asientos contables. La consulta conserva `accounting.view`.

Trazabilidad implementada, sin rediseñar contratos CLOSED:

| Elemento | Registro durable |
|---|---|
| Asientos (manuales, reversos) | Entity Audit `JournalEntryAudit` (§11) |
| Períodos (creación, cierre, bloqueo) | Entity Audit `AccountingPeriodAudit` (§22) |
| Uso operativo y vigencia | Versiones append-only de `AccountingOperationalPolicy` con autor (§3) |
| Obligación (creación, resolución, cancelación) | `AccountingPosting` con autor y asiento asociado |
| Intentos inicial y manual | `AccountingPostingAttempt`: usuario, motivo, instante UTC, resultado, error y correlación (§17); manuales en `ManualJournalAttempt` (§11) |
| Cuentas y reglas contables | Campos de auditoría de creación/última modificación (`AuditableEntity`); sin historial Entity Audit |

## 15. Reglas no permitidas

- Warning + continuar + perder el asiento o la obligación.
- Rollback comercial por una cuenta/regla faltante o fallo exclusivamente contable.
- Confirmar un núcleo inválido o sin poder persistir su obligación Enabled.
- Reintentar Failed automáticamente o desde una operación ajena sin acción manual.
- Confundir `IsAccountingReq` con preferencia operativa o duplicar obligación fiscal.
- Deshabilitar Required, deshabilitar con pendientes o generar historia al activar.
- Cambiar identidad por intento, duplicar asientos o reprocesar con datos económicos recalculados.
- Recalcular un reverso con reglas vigentes, cambiar sus importes o cuentas originales.
- Mutar el asiento de un período cerrado, reabrirlo automáticamente o elegir otro período arbitrario.
- Cerrar con pendientes, omisiones o inconsistencias; presentar reportes incompletos como definitivos.
- Asumir que Outbox/SRI/Communications recuperan Accounting o crear otro pipeline paralelo.

## 16. Adopción y evidencia

Esta sección registra los cortes de adopción ya implementados; no introduce otra política.
Rutas abreviadas desde `backend/src/`:

| Clasificación | Evidencia actual |
|---|---|
| RESUELTO | ACCOUNTING-CONTINUITY-04: Venta, devoluciones, pagos, cobros, NC compra Discount y saldo a favor de proveedor dejaron de contabilizar en línea; los núcleos operativos ya no resuelven cuentas ni exigen líneas IRBPNR/anticipo |
| KEEP | ACCOUNTING-CONTINUITY-03: JournalEntryReversal y JournalEntry.Reverse conservan el original y espejan el libro histórico en fecha/período/ejercicio destino; pago, cobro y anulaciones existentes adoptados |
| RESUELTO | ACCOUNTING-CONTINUITY-06: el cierre concilia orígenes sin obligación ni asiento, apertura/ASI y costo pendiente con el readiness único (§21) |
| RESUELTO | ACCOUNTING-CONTINUITY-05: InventoryCostAccounting, su evento y el retry masivo de costos retirados; costo de ventas en la obligación común |

Cortes adoptados: Compra/Gasto + Retención (ACCOUNTING-CONTINUITY-01) y demás integraciones
comerciales/financieras (ACCOUNTING-CONTINUITY-04), costos/apertura/Disabled (ACCOUNTING-CONTINUITY-05),
integridad de cierre/reportes (ACCOUNTING-CONTINUITY-06, §21) y asientos manuales (ACCOUNTING-08, §11).
ACCOUNTING-CONTINUITY-03 reemplaza el rollback por fallos posteriores al gate de período con una
obligación durable en los consumidores migrados. No existe pipeline paralelo.

PostgreSQL real es obligatorio para verificar transacciones, concurrencia, scope,
locks e invariantes de persistencia; no sustituir por SQLite, memoria o mocks.
No hay decisiones funcionales pendientes para consolidar este contrato; si la
implementación descubre una contradicción de dominio, se eleva la decisión necesaria.

## 17. Reproceso manual ACCOUNTING-CONTINUITY-02

EXTEND: `POST /api/v1/accounting/postings/reprocess` recibe `{ ids, reason }`, hasta
100 IDs explícitos por solicitud, deduplicados conservando orden. Reutiliza
`accounting.postings.reprocess` y el mismo ejecutor individual con `IPostingEngine`.
Cada obligación tiene transacción propia y resultado `Attempted` (Posted/Failed),
`AlreadyResolved` (Posted/Canceled), `NotFound` (incluye otro Tenant/Company) o
`NotAttempted` (cancelación). Un fallo no detiene las siguientes. Si tampoco puede
persistirse el fallo, `Attempted` incluye error interno seguro y Posting null;
la obligación conserva su estado durable anterior.

`AccountingPostingAttempt.Reason` almacena el motivo manual obligatorio (hasta 500
caracteres, recortado en sus extremos), junto con usuario y correlación común del
lote. Los intentos iniciales y los registros históricos pueden tener motivo null.
El endpoint individual recibe `{ reason }`. No hay tabla Batch ni retry automático.

BUG REAL: el ejecutor limpia tracking antes/después de cada intento independiente,
fuera de cualquier transacción ajena; conserva tracking durante la transacción
para permitir varias reservas válidas de JournalEntrySequence. Los locks y las
claves de idempotencia existentes permanecen. La UI reutiliza componentes ZH,
selecciona solo Pending/Failed, exige motivo y confirmación, y muestra cada resultado.

## 18. Reverso común ACCOUNTING-CONTINUITY-03

El reverso conserva `OriginalJournalEntryId`; su identidad es Accounting / Reversal /
original.Id. Motivo completo y autor viven en el reverso. La relación inversa del detalle
se consulta desde ese asiento; los campos inversos antiguos permanecen solo para lectura histórica.
La fecha efectiva se fija convirtiendo el instante del acto con `ICompanyClock.LocalDateAsync`
y queda persistida en la obligación. El núcleo resuelve el período y ejercicio destino,
protege Company con el lock compartido compatible con cierre y serializa por identidad.
Consulta el reverso existente antes de validar nuevamente el período o reservar un número.

Pago proveedor, cobro, Compra/Gasto/Retención y las anulaciones de Venta/NC de compra que
ya consumían el comando registran la obligación en su transacción, tras superar el gate
Open. Pending/Failed sin asiento se cancela, sin espejo. El intento inicial posterior al
commit y el reproceso manual usan el mismo AccountingPostingExecutor y JournalEntryReversal;
jamás PostingRules/cuentas actuales. Un fallo queda durable; no hay reintento automático.
Apertura conserva fecha y restricciones propias; costos solo adapta la firma existente.
No se corrigen reversos históricos previos. ACCOUNTING-CONTINUITY-04 extiende este reverso a
la anulación de devolución de compra y a las reversas de aplicación y reembolso de saldo a favor.

## 19. Integraciones durables ACCOUNTING-CONTINUITY-04

Cada hecho registra su obligación con `AccountingPostingRecorder` dentro de la transacción
operativa y su intento inicial corre después del commit con `AccountingPostingExecutor`; ningún
flujo operativo invoca el motor. Se conservan las identidades contables existentes:

| Hecho (SourceModule / FactType) | Identidad | Fecha | Datos persistidos |
|---|---|---|---|
| Sales / InvoiceIssued | Factura | IssueDate | Importes, IRBPNR, contado sin destino, saldo CxC, destino de cada cobro no-Crédito |
| Sales / SalesReturn | Devolución | Fecha Company al autorizar | Importes, IRBPNR, factura de origen, reembolso en Caja (`CashApplied`, caja de la sesión del `SaleRefund` sustituye la cuenta Haber de Caja) y crédito CxC (`PendingBalance`) desde `RefundAllocations`. Regla: Debe 4.1.03.001 Devoluciones en ventas/IVA/ICE/IRBPNR; Haber Descuentos/Caja/CxC |
| Payables / SupplierPaymentConfirmed | Pago | PaymentDate | Total, aplicado a CxP, remanente anticipo, caja/banco de cada medio |
| Finance / CollectionApplied | Cobro | PaymentDate | Monto, caja o banco elegido (sustituye la cuenta Debe GrandTotal de la regla) |
| Purchases / PurchaseReturn | Devolución | Fecha Company al autorizar | IVA/ICE/IRBPNR, CxP aplicada, saldo a favor, costo histórico, diferencia de costo Debe/Haber |
| Purchases / PurchaseCreditNoteAuthorized | NC (solo Discount) | Fecha Company al autorizar | Subtotal, IVA, ICE, IRBPNR, total, CxP aplicada |
| Purchases / SupplierCreditApplied | Movimiento de aplicación | Fecha Company al aplicar | Monto, saldo a favor y CxP destino |
| Purchases / SupplierCreditRefunded | Movimiento de reembolso | Fecha efectiva | Monto, caja o banco que recibe |

Destinos: Efectivo → caja de la sesión; Transferencia → cuenta bancaria; Tarjeta/Cheque → el
medio de pago; Crédito → CxC por la regla. El intento resuelve la cuenta vigente del destino
(caja y banco: su cuenta; tarjeta/cheque: `PaymentMethodAccount` de la Company), agrupada por
cuenta y naturaleza; un destino inexistente en la Company o sin cuenta deja la obligación Failed.
También fallan en el intento, nunca en el núcleo: IRBPNR > 0 sin línea IRBPNR en la regla,
remanente de anticipo sin línea de anticipos, cuentas inválidas, regla o período. El núcleo
operativo conserva sus bloqueos no contables: pertenencia Tenant/Company, integridad del
documento, saldo, disponibilidad, destino comercial/financiero requerido y restricciones de Caja.

El `SupplierCredit` creado por el remanente de un pago no genera asiento propio; la NC tipo
Return no genera asiento (su efecto es el de la devolución). Reversos con el comando común de
§18 sobre el asiento original: Purchases/PurchaseReturn al anular la devolución autorizada
(desde Draft no hay hecho), Purchases/SupplierCreditApplied y Purchases/SupplierCreditRefunded
del movimiento revertido. Los FactType de reverso por regla anteriores (PurchaseReturnCancelled,
SupplierCreditApplicationReversed, SupplierCreditRefundReversed) ya no se generan ni se siembran;
las reglas existentes se conservan sin uso, nunca se borran. Lo mismo `Payables/SupplierPaymentReversed`
(ACCOUNTING-FINAL-FIX): fuera de `PostingFactCatalog` y del bootstrap; la reversa del pago espeja el
asiento de `Payables/SupplierPaymentConfirmed` (§18).

Sin backfill: no se crean obligaciones ni asientos para operaciones previas; los fallos se resuelven
con el reproceso manual de §17. (El comando `remediate-purchase-return-postings` se retiró en §27.)

## 20. Costos, apertura y Disabled ACCOUNTING-CONTINUITY-05

**Costo de ventas.** Tres hechos con la identidad existente: reconocimiento (Sales/CostOfGoodsSold,
factura, fecha de emisión, costo de los movimientos de la venta), cobertura de costo pendiente
(asignación, fecha del movimiento que cubre) y devolución valorizada (movimiento, fecha de la
devolución; factura sin valoración A2: obligación agregada por devolución, fecha Company). El signo
fija CostOfGoodsSold/CostOfGoodsSoldReversed; importe cero no genera obligación. El Kardex publica el
hecho (valoración operativa, independiente de Accounting); `AccountingPostingRecorder` registra la
obligación en la transacción operativa y el intento corre después del commit. `SourceDocumentId` =
factura agrupa sus obligaciones. Delta contable de una cobertura o de una devolución valorizada,
resuelto en el intento: `costo objetivo reconocido − costo ya contabilizado`, donde el objetivo es el
costo vigente de la factura redondeado tras la cobertura o la devolución (guardado en la obligación;
`SaleCostObligation` es la autoridad de valoración y cantidad) y lo ya contabilizado es la suma con signo de
los asientos de las obligaciones de costo Posted anteriores de la misma factura (Pending/Failed/Canceled
no cuentan; la dependencia garantiza que todas las anteriores estén resueltas). El delta operativo solo
fija el sentido del hecho: delta contable cero → Posted sin asiento (resolución de importe cero, sin
reverso al anular); sentido contrario al hecho → Failed `COST_DELTA_SIGN_MISMATCH`.
Costo objetivo, costo contabilizado, delta, comparación con cero y validación de signo usan
`AccountingDecimals` de la política única de precisión ([data-standards.md](data-standards.md#estándar-de-precisión-numérica)).
La aplicabilidad se ancla a la emisión de la factura (BusinessDate): venta nacida en Disabled → ni su
costo ni sus coberturas/devoluciones generan obligación; venta sujeta → sus ajustes posteriores sí.
`SaleCostObligation`/`SaleCostAllocation` son solo valoración. Un intento de costo con una obligación
de costo anterior de la misma factura que no esté Posted/Canceled queda Failed
`COST_DEPENDENCY_PENDING`; la anterior nunca se ejecuta implícitamente. Anular la factura aplica la
anulación común a cada obligación de costo: Posted → reverso espejo con la fecha del acto;
Pending/Failed → Canceled. El costo se consulta y reprocesa en Contabilizaciones (§17);
`stock/costs/pending` solo informa incertidumbre operativa (ACCOUNTING-12B: cada fila trae
`ObligationId`, factura —Id, número, fecha de emisión, línea—, ítem —Id, SKU, nombre—, bodega —Id, nombre—,
cantidad pendiente, costo provisional y estado `Provisional|CostPending`; permiso propio
`inventory.pending-costs.view`, no `inventory.stock.view` que corresponde a Kardex). Pantalla
`/inventory/pending-costs`, NavItem "Costos pendientes" en Inventario → Inventario con el mismo permiso: tabla
de pendientes, enlaces a Registrar compra (`purchases.create`), ajuste positivo (`inventory.adjustments.create`)
y Kardex del ítem, estado vacío. Sin acción "Contabilizar": la cobertura sigue siendo automática al ingresar
costo válido (compra, ajuste positivo, carga inicial).

Migración `AccountingContinuity05`: copia cada fila existente de `inventory_cost_postings` a la
obligación común conservando identidad, estado, asiento y fechas (BusinessDate = emisión de la
factura). Única excepción: filas con importe cero y sin asiento, en cualquier estado (Pending, Failed o
Posted): no tienen efecto contable que preservar y una obligación de importe cero quedaría sin
resolución posible; permanecen solo como evidencia legacy de solo lectura. Una fila con asiento se migra
siempre. No crea obligaciones para hechos sin fila. La tabla legacy se eliminó después (§27). Histórico:
la migración quedó consolidada en `20261018014647_InitialProductionBaseline` (MIGRATION-SQUASH); una
instalación nueva nace sin `inventory_cost_postings` y sin nada que copiar.

**Apertura.** La decisión operativa se toma sobre `Company.OpeningBalanceDate`, bajo el lock
compartido de política. Sujeta: se conservan OpeningBalancePosting/ASI y sus reglas especiales. No
sujeta: no se publican asientos de lote ni ASI (`OPENING_ACCOUNTING_NOT_APPLICABLE`) y el cierre de la
carga inicial exige solo fecha de apertura y ningún lote en curso. Sin modo elegido: fail-closed.
El reverso/corrección del ASI toma el lock compartido y sigue permitido en Disabled. Un asiento de
lote o ASI vigente Pending/Failed bloquea el cierre del período que contiene su fecha.

**Disabled.** Toda versión nueva se valida bajo el lock exclusivo de política, que también toma
`UpdateCompanyFiscal` (`IsAccountingReq` se lee dentro del lock). Enabled: desde hoy (fecha Company);
una fecha pasada solo en la primera elección y solo si no existe ningún hecho contablemente relevante
(venta, devolución de venta, compra, gasto, NC o devolución de compra, pago a proveedor, cobro/pago) con
fecha igual o posterior; nunca se generan obligaciones históricas. Disabled:
rechazado si la empresa es obligada, si la vigencia es anterior a hoy o no posterior a la última fecha
contable de una obligación automática no cancelada, si existe alguna obligación Pending/Failed, si una
venta sujeta conserva costo pendiente de resolver, o si la apertura sujeta no está cerrada y tiene lotes
sin asiento Posted o ASI vigente no publicado. Los reversos de asientos de la era Enabled siguen
generándose durante Disabled.

## 21. Cierre y completitud ACCOUNTING-CONTINUITY-06

**Readiness único.** `AccountingCompletenessService` (`IAccountingCompletenessEvaluator`) es la única
evaluación de completitud contable por Company y alcance; alimenta el cierre, el pre-cierre y los reportes.
Cada motivo es `{ Code, Count }`, sin listar documentos:

| Código | Alcance | Condición |
|---|---|---|
| `POSTINGS_UNRESOLVED` | Rango | Obligaciones Pending/Failed por `EntryDate` |
| `OPENING_BATCHES_UNPOSTED` | Fecha de apertura en rango | Lotes de saldos Completed sin asiento Posted (sin estado, Pending o Failed) |
| `OPENING_ASI_MISSING` / `OPENING_ASI_UNRESOLVED` | Fecha de apertura en rango | ASI vigente ausente / no Posted |
| `SALE_COST_PENDING` | Rango | Facturas con `SaleCostObligation` pendiente cuya emisión está sujeta (Enabled) |
| `ORIGINS_WITHOUT_ACCOUNTING` | Rango ∩ ventanas Enabled desde la adopción | Documento sin `AccountingPosting` ni `JournalEntry` de su identidad (módulo + documento) |
| `JOURNAL_ENTRIES_NOT_FINAL` / `JOURNAL_ENTRIES_WITHOUT_NUMBER` / `REVERSALS_INCOMPLETE` | Solo cierre | Asientos del período |
| `PREVIOUS_PERIOD_OPEN` | Solo cierre | Período anterior de la misma Company todavía Open |
| `YEAR_END_CLOSING_MISSING` | Solo cierre del último período del ejercicio | Cuentas de resultados del ejercicio con saldo (cierre anual no Posted o desactualizado, §26) |

La apertura solo aplica si la fecha de apertura está sujeta y la Carga Inicial no está cerrada
(`OpeningAccountingCompleteness`, compartido con las precondiciones de Disabled; allí el ASI ausente no
bloquea). La adopción de una versión Enabled es la fecha Company de su alta: los documentos previos a la
adopción contable y los de ventanas Disabled no se concilian. Orígenes conciliados (estado que genera
la obligación; fecha contable): factura de venta Authorized/Cancelled (IssueDate); devolución de venta
Authorized (día Company de su autorización: Authorized es terminal y Cancel solo procede desde Draft);
compra Confirmed/Cancelled (IssueDate); devolución de compra y NC Discount con `AuthorizedAtUtc` (día
Company); aplicación de saldo a favor (movimiento Application, día Company); reembolso de saldo a favor
(transacción RefundReceived, EffectiveDate); gasto Confirmed/Cancelled (AccountingDate); retención
Issued/Cancelled (IssueDate); pago a proveedor (PaymentDate); cobro Applied/Reversed (PaymentDate). Una
obligación en cualquier estado o un asiento histórico de la misma identidad resuelve el origen. El costo
de ventas se cubre con sus obligaciones y `SALE_COST_PENDING`.

**Cierre.** `CloseAccountingPeriod` evalúa el readiness bajo el lock exclusivo y lo traduce a
`JournalEntryClosureReadiness`, cuyas banderas son todas obligatorias. Cierre secuencial: no se cierra N
con un período anterior Open; no se reabre ni modifica ningún período previo.
`GET accounting-periods/{id}/closure-readiness` (solo lectura, `accounting.view`) devuelve el mismo
resultado, sin lock.

**Gate Closed/Locked.** `AccountingPostingRecorder`, bajo el lock compartido y solo si el hecho está
sujeto, rechaza la operación (`ACCOUNTING_PERIOD_CLOSED`) cuando la fecha contable pertenece a un período
Closed/Locked: no se mueve la fecha, no se crea Failed permanente y no se reabre. Sin período para la fecha
no se rechaza (Failed recuperable). Los reversos conservan su propio gate Open (§7).

**Reportes.** Diario, Mayor, Balance de Comprobación, Estado de Resultados y Balance General devuelven
`Completeness { IsComplete, AsOf, Reasons[] }` del mismo readiness. Diario y Estado de Resultados evalúan
su rango; Mayor y Balance de Comprobación, desde el inicio contable hasta `ToDate` (arrastran saldo
inicial); Balance General, desde el inicio hasta `AsOfDate`. El frontend solo muestra la advertencia de
reporte provisional.

**Permiso de cierre.** `accounting.periods.close` (cierre y bloqueo), asignable desde el NavItem
Períodos contables (ACCOUNTING-07, §22); `accounting.update` ya no gobierna cierre ni bloqueo.

**Performance (DEBT).** Cada reporte ejecuta ~8 consultas adicionales de completitud; medir con volumen
piloto y consolidar después solo si hace falta, sin cache.

## 22. Períodos contables ACCOUNTING-07

**Periodicidad.** El ejercicio es el año calendario: `FiscalYear` = año de `StartDate` y de `EndDate`, y
`PeriodNumber` 1..12. Los ejercicios anteriores a 2027 conservan el período anual sembrado (2026 no se
divide retroactivamente); desde 2027 el ejercicio son 12 períodos mensuales naturales
(`PeriodNumber` = mes). El bootstrap de una Company nueva siembra el año en curso con esa regla.

**Generación del siguiente ejercicio.** Manual, desde la pantalla de Períodos
(`POST accounting-periods/fiscal-years`, `GenerateAccountingFiscalYear`): crea los 12 meses naturales
contiguos. Idempotente: completa los meses faltantes al final de un prefijo mensual exacto y no crea nada
si ya están los 12; un ejercicio con otra estructura (p. ej. anual) se rechaza. No hay generación
automática; la pantalla avisa desde el 1-dic si el ejercicio siguiente no existe y cuando hoy no tiene período.

**Estructura (sin huecos).** Un período nuevo (manual o generado) no puede solaparse; si existe un
anterior debe empezar exactamente al día siguiente de su `EndDate` y, si existe uno posterior, terminar
el día previo a su inicio; nunca puede quedar antes de un período Closed/Locked. El número es
correlativo dentro del ejercicio (anterior + 1; 1 al iniciar el ejercicio). Se admite un período de un
solo día (`EndDate >= StartDate`). El solape tiene defensa final en PostgreSQL
(`ex_accounting_periods_company_no_overlap`, exclusión `btree_gist` sobre Company y rango de fechas);
la validación previa en Application solo da el mensaje de UX. Un período existente no se edita ni se elimina.

**Cierre temporal.** Además del readiness (§21) y del cierre secuencial, un período solo se cierra si
`EndDate` es anterior a hoy en la fecha de la empresa (`ICompanyClock`, nunca UTC). El pre-cierre solo
evalúa períodos Open: Closed/Locked responden explícitamente no elegibles, sin readiness. La respuesta
incluye `HasEnded`, `CompanyToday` y `CanClose` (Open + terminado + completo).

**Locked.** Estado final administrativo/auditivo e irreversible, solo desde Closed; registra
`LockedAtUtc`/`LockedBy`. No cambia lo que el período admite (Closed ya rechaza toda escritura). No
existe reapertura ni desbloqueo.

**Permisos.** Listado, detalle y pre-cierre: `accounting.view`. Crear período y generar ejercicio:
`accounting.periods.create`. Cerrar y bloquear: `accounting.periods.close`. NavItem
Contabilidad › Configuración › Períodos contables (`accounting.view`, acciones relacionadas
`accounting.periods.create`, `accounting.periods.close`).

**Auditoría.** Entity Audit (`AccountingPeriodAudit`) de Created, Closed y Locked con ejercicio, número
y rango; `ClosedAtUtc`/`ClosedBy` y `LockedAtUtc`/`LockedBy` persisten en el período.

**Pantalla.** `/accounting/configuration/periods`: lista (ejercicio, número, rango, estado, cerrado
el/por), generación del ejercicio siguiente, creación manual prellenada con el rango contiguo,
revisión de cierre con `{Code, Count}`, `AsOf` y refresco, cierre con confirmación de irreversibilidad y
bloqueo. Cada motivo dirige a su pantalla (contabilizaciones, asientos, período anterior, carga inicial, costos pendientes);
`SALE_COST_PENDING` enlaza a `/inventory/pending-costs` si el usuario tiene `inventory.pending-costs.view` (ACCOUNTING-12B); `ORIGINS_WITHOUT_ACCOUNTING` solo se
explica con su conteo (sin endpoint de detalle).

## 23. Reglas contables y Balance General ACCOUNTING-11

**Catálogo único.** `PostingFactCatalog` (Application, Posting Engine) es la autoridad runtime de los
hechos que admiten PostingRule: clave (SourceModule, FactType), AmountKinds permitidos (los montos que el
traductor del hecho aporta) y lados dinámicos (Debe y/o Haber completados por el origen con
`Allocations`: cajas, bancos, medios de pago, categorías de gasto). Sin AmountKinds la regla es
habilitadora y no admite líneas fijas (ASI de apertura). El mismo catálogo valida Create/Update/Enable,
alimenta `GET posting-rules/catalog` y da la clave a cada regla del bootstrap, que además se omite (log)
si sus líneas no cumplen la forma. Un hecho fuera del catálogo (p. ej. anulaciones que espejan el asiento
histórico, §7) no se crea, edita ni reactiva; sus reglas antiguas solo se listan.

**Validación (servidor).** Solo claves del catálogo, nunca texto libre. Líneas: AmountKind permitido por el
hecho; sin repetir la misma dirección y tipo de monto; al menos 2 líneas, una al Debe y una al Haber,
salvo en el lado dinámico (p. ej. pago a proveedor: el Haber lo aportan los medios de pago, así que basta
el Debe); cada cuenta existe en la misma Company, activa y con AllowsPosting. Editar reemplaza todas las
líneas y solo afecta contabilizaciones futuras; nunca modifica asientos publicados. Reactivar exige que la
regla vuelva a cumplir la validación. Desactivar deja fail-closed (`RULE_NOT_FOUND`) toda operación futura
de ese tipo hasta reactivar una regla válida. Los campos planos heredados DebitAccountId/CreditAccountId/TaxCode
se eliminaron del modelo (el motor nunca los leyó); `Lines` es la única configuración de la regla.

**Permisos.** Consulta (reglas y catálogo): `accounting.view`. Crear, editar, activar y desactivar:
`accounting.rules.manage`, acción relacionada del NavItem Contabilidad › Configuración › Reglas contables.

**Pantalla.** `/accounting/configuration/posting-rules`: hechos soportados sin regla (para que una empresa
nueva los configure sin SQL), reglas con su diagnóstico según el catálogo, editor de líneas (cuenta,
Debe/Haber, tipo de monto del catálogo) y desactivación con confirmación explícita.

**Balance General.** Activo suma Debe − Haber y Pasivo/Patrimonio Haber − Debe por sección (una cuenta
correctora resta dentro de su sección). Patrimonio agrega dos filas calculadas desde asientos Posted hasta
`AsOfDate`, sin asiento ni persistencia: **Resultado del ejercicio** = Ingresos − Costos −
Gastos del 01-ene del año de `AsOfDate` a `AsOfDate` (ejercicio = año calendario, §22) y **Resultados no
cerrados de ejercicios anteriores** = el mismo cálculo antes del 01-ene. `TotalEquity` las incluye e
`IsBalanced` representa un descuadre real. Un asiento de cierre que deja las cuentas nominales en cero
lleva esas filas a cero sin duplicar el patrimonio trasladado. El cierre anual es un asiento generado a
pedido del usuario y publicado por él (§26); no existe cierre automático.

**Estado de Resultados (ACCOUNTING-12A).** Usa el mismo signo por sección que el Balance: Ingresos =
Haber − Debe; Costos y Gastos = Debe − Haber, sin importar la naturaleza de la cuenta (una correctora resta
dentro de su sección). Para el mismo rango, Utilidad neta = Resultado del ejercicio del Balance General.
El asiento de cierre anual y su reverso se excluyen del Estado de Resultados (§26).

## 24. Ajustes de inventario ACCOUNTING-12A

**Hechos.** `Inventory/StockAdjustmentIn` (Debe Inventario / Haber cuenta del motivo) e
`Inventory/StockAdjustmentOut` (Debe cuenta del motivo / Haber Inventario), en `PostingFactCatalog` con
`GrandTotal` y lado dinámico (Haber en In, Debe en Out). El bootstrap siembra la línea fija `1.1.04.001`
Inventario; la contrapartida llega como destino `InventoryAdjustmentReason` por identidad del motivo.

**Importe y fecha.** Importe = suma del `TotalCost` real de los movimientos Kardex del ajuste
(PositiveAdjust/NegativeAdjust), leídos ya persistidos y redondeados con `AccountingDecimals`; un movimiento
sin costo conocido no movió el valor del Kardex y no aporta importe. Importe cero no genera obligación.
Fecha contable y BusinessDate = fecha efectiva de ejecución, la misma `EffectiveDate` de los movimientos
Kardex del ajuste (nunca la fecha de creación del borrador); el gate Closed/Locked (§21) se evalúa contra ella,
así que un borrador creado en un período luego cerrado y ejecutado hoy contabiliza hoy. Identidad = ajuste;
`SourceDocumentId` = ajuste.
`ExecuteStockAdjustment` publica el hecho y `AccountingPostingRecorder` registra la obligación en la misma
transacción; el saldo inicial de Carga Inicial (`PostInitialBalance`) no lo publica (tiene su propio hecho).

**Cuenta del motivo.** Configuración Company-scoped `InventoryAdjustmentReasonAccount` (TenantId, CompanyId,
motivo, cuenta; único CompanyId + motivo), mismo criterio que `PaymentMethodAccount`: el motivo puede ser
tenant-wide y no guarda cuenta. Se administra desde la pantalla de motivos para la Company activa
(`PUT inventory/adjustment-reasons/{id}/account`, Company del contexto autenticado; la cuenta debe ser de esa
Company, activa e imputable; se reemplaza, no se quita). Se resuelve en cada intento con la Company del hecho,
sin fallback: sin configuración → Failed `INVENTORY_ADJUSTMENT_ACCOUNT_NOT_CONFIGURED`; cuenta inactiva o no
imputable → Failed `INVENTORY_ADJUSTMENT_ACCOUNT_INVALID`. El ajuste nunca se revierte; se corrige
configurando la cuenta y reprocesando (§17).

**Anulación.** Anular un ajuste ejecutado aplica el reverso histórico común (§7): Posted → espejo con la
fecha del acto; Pending/Failed → Canceled.

**Completitud.** `ORIGINS_WITHOUT_ACCOUNTING` incluye ajustes Executed/Cancelled con valor Kardex por la
fecha efectiva de su movimiento Kardex de ejecución; también cuentan como hecho contablemente relevante para la elección de vigencia (§20).
Sin backfill.

## 25. Caja contable ACCOUNTING-12C

**Hechos.** `Caja/ManualIncome` (Debe caja / Haber cuenta del motivo), `Caja/ManualExpense` y `Caja/Withdrawal`
(Debe cuenta del motivo / Haber caja), `Caja/BankDeposit` (Debe cuenta contable del banco / Haber caja): reglas
habilitadoras en `PostingFactCatalog` (sin AmountKinds, ambos lados dinámicos). `Caja/CashCountSurplus` (Debe caja /
Haber `4.2.01.003` Diferencias positivas de caja) y `Caja/CashCountShortage` (Debe `6.5.01.001` Descuadres de caja /
Haber caja): `GrandTotal` = |Difference|, la caja es el lado dinámico. El bootstrap siembra las seis reglas. La caja es
siempre la cuenta vigente de la `CashRegister` de la sesión (destino `CashRegister`); el banco, la de la
`CompanyBankAccount` elegida (destino `BankAccount`). Withdrawal es retiro genérico, nunca depósito bancario.

**Orígenes.** Solo generan hecho los movimientos manuales con motivo elegido por el cajero
(`CashSession.RecordManualMovement`), el depósito (`RecordBankDeposit`) y el cierre con `Difference ≠ 0`
(`Close`); Difference = 0 no genera obligación ni asiento. Los demás movimientos de caja (apertura, ventas,
devoluciones, pagos a proveedor y los ManualIncome/ManualExpense internos de reembolso de saldo a favor, sin motivo)
los contabiliza su documento de origen; Posting nunca consume `CashMovement`/`CashSession`. Identidad = movimiento
(sesión para la diferencia de arqueo); `SourceDocumentId` = sesión. Fecha contable y BusinessDate = día Company del
instante del movimiento o del cierre. Importe redondeado con `AccountingDecimals`. La obligación se registra en la
transacción operativa con `AccountingPostingRecorder`; una falla contable posterior nunca revierte el movimiento ni el
cierre (se aplican los gates comunes: modo operativo no configurado y período Closed/Locked, §3/§21).

**Cuenta del motivo.** `CashMovementReason.AccountingAccountId`: el motivo de caja ya es Tenant+Company obligatorio
(nunca se comparte entre empresas), así que la cuenta vive en el propio motivo, mismo criterio que
`CashRegister.AccountingAccountId`; no se usa una tabla puente como `InventoryAdjustmentReasonAccount` (cuyo motivo es
tenant-wide). Se administra en la pantalla de motivos de caja (`PUT cash-movement-reasons/{id}/account`,
`caja.manage`, Company del contexto; cuenta de esa Company, activa e imputable; se reemplaza, no se quita). Se resuelve
en cada intento, sin fallback: sin cuenta → Failed `CASH_MOVEMENT_REASON_ACCOUNT_NOT_CONFIGURED`; cuenta inactiva o no
imputable → Failed `CASH_MOVEMENT_REASON_ACCOUNT_INVALID`; se corrige configurando y reprocesando (§17).

**Depósito bancario.** `POST cash-sessions/{id}/bank-deposits` (`caja.record`): sesión abierta y operada por su
cajero, `CompanyBankAccountId` activa de la Company de la sesión, monto > 0, nota y referencia/papeleta opcional,
idempotente por `ClientRequestId` (intención V1 propia). Crea `CashMovement` tipo `BankDeposit` (egreso de caja) con
`ReferenceType = CompanyBankAccount` y `ReferenceId` = cuenta bancaria destino. El seed ya no crea el motivo
"DEPOSITO_BANCARIO" de Withdrawal; los existentes se conservan.

**Reversos.** Los movimientos de caja y el cierre no tienen anulación operativa: no aplica reverso.

**Completitud.** `ORIGINS_WITHOUT_ACCOUNTING` incluye movimientos manuales con motivo, depósitos (día Company de
`CreatedAt`) y sesiones Closed con diferencia (día Company de `ClosedAt`); también cuentan como hecho contablemente
relevante para la elección de vigencia (§20). Sin backfill.

## 26. Cierre anual ACCOUNTING-12D

**Asiento.** `GenerateFiscalYearClosingEntry(FiscalYear)` (`POST accounting-periods/fiscal-years/{year}/closing-entry`,
`accounting.periods.close`) crea un `JournalEntry` Draft con `SourceModule = Accounting`,
`SourceEventType = FiscalYearClosing` e identidad nueva por generación, fecha 31-dic del ejercicio (año calendario,
§22) y período = último período del ejercicio. Saldos acumulados del ejercicio de las cuentas Income/Cost/Expense,
solo de asientos Posted del 01-ene al 31-dic, excluidos cierres anteriores y sus reversos:
cada cuenta con saldo acreedor se debita y con saldo deudor se acredita por su saldo; la diferencia neta va a
`3.1.03.001` Resultado del ejercicio del plan de la Company (Haber si hay utilidad, Debe si hay pérdida). Importes con
`AccountingDecimals`; la contrapartida es la suma exacta de las líneas, así que el asiento balancea sin residuo y el
Domain lo valida al crearlo. Sin saldos nominales no se genera (`YEAR_END_CLOSING_NOTHING_TO_CLOSE`). Participación de
trabajadores e impuesto a la renta se registran antes como asientos manuales; no se calculan.

**Precondiciones (fail-closed, en generación y otra vez al publicar).** Períodos del ejercicio que cubren 01-ene a
31-dic; último período Open y todos los anteriores Closed/Locked; hoy (fecha Company) posterior al 31-dic; Accounting
Enabled el 31-dic; readiness único del ejercicio completo (§21: obligaciones Pending/Failed, apertura, costo pendiente,
orígenes sin contabilizar); ningún otro asiento Draft en el último período; cuenta de resultado existente, activa,
imputable y de Patrimonio; cuentas de las líneas imputables (`PostingAccountGuard`). Duplicados: no se genera otro si
existe un cierre del ejercicio Draft (`YEAR_END_CLOSING_DRAFT_EXISTS`) o Posted sin reverso
(`YEAR_END_CLOSING_ALREADY_POSTED`); la resolución explícita es descartar el Draft o reversar el Posted. Generación y
publicación se serializan con el lock compartido de Company y un advisory lock por Company + ejercicio.

**Publicación.** Con el flujo de manuales (§11): `POST manual-journal-entries/{id}/post` (`accounting.manual.post`),
versión, intentos durables, numeración, período Open y Entity Audit; descarte y reverso igual que un manual. El
cierre no se edita (`YEAR_END_CLOSING_NOT_EDITABLE`). Su reverso usa siempre la fecha original del cierre (31-dic) y
el último período del ejercicio Open: otra fecha se rechaza (`YEAR_END_CLOSING_REVERSAL_DATE`) y con el período
Closed/Locked el gate común lo rechaza (`PERIOD_NOT_OPEN`), sin escribir; así el resultado nunca se traslada al
ejercicio siguiente. Se serializa con el lock del ejercicio; la UI precarga la fecha y no permite editarla. Al publicar se recalcula el cierre bajo el lock del ejercicio:
si las líneas o la fecha difieren del cálculo vigente se rechaza `YEAR_END_CLOSING_OUTDATED` (descartar y regenerar).

**Último período.** `CloseAccountingPeriod` agrega `YEAR_END_CLOSING_MISSING` cuando el período termina el 31-dic de su
ejercicio y alguna cuenta nominal tiene saldo en el ejercicio contando el cierre Posted. Se evalúa por efecto: sin
saldos que cerrar no bloquea, con el cierre Posted vigente las nominales quedan en cero y el período puede cerrarse, y
un movimiento de resultados posterior al cierre vuelve a bloquear hasta reversar y regenerar.

**Reportes.** Estado de Resultados excluye el cierre y su reverso (muestra el resultado del ejercicio). Balance General
lo incluye: las nominales quedan en cero, la fila calculada Resultado del ejercicio vale cero y el resultado aparece una
sola vez en `3.1.03.001`; los ejercicios siguientes no lo arrastran como no cerrado. Diario, Mayor y Balance de
Comprobación lo muestran como un asiento más. Sin un segundo motor de reportes.

**Pantalla.** Contabilidad › Períodos, tarjeta «Cierre anual» (`GET accounting-periods/fiscal-year-closings`,
`accounting.view`): ejercicio, estado (Sin generar/Borrador/Contabilizado), asiento vinculado y resultado; acción
«Generar asiento de cierre anual» con `accounting.periods.close` cuando el último período está Open; el Draft se
revisa y publica en el detalle del asiento. La revisión de cierre explica `YEAR_END_CLOSING_MISSING`.

## 27. Limpieza de esquema heredado ACCOUNTING-SCHEMA-CLEANUP

Previo al squash de migraciones. Se retiró solo lo que no tenía lectores, escritores ni papel en una
instalación nueva, el bootstrap vigente o la operación:

- `InventoryCostPosting` / `inventory_cost_postings`: entidad, configuración EF y tabla. El costo de ventas
  vive solo en la obligación común (§20).
- `JournalEntry.ReverseJournalEntryId` (columna, FK e índice) y el estado `Reversed` de `JournalEntryStatus`.
  Valores persistidos: Draft = 0, Posted = 1, Discarded = 3; el 2 no se reutiliza. El par de reverso se
  resuelve solo por `OriginalJournalEntryId` (`uq_journal_entries_original_journal_entry_id`,
  `FindReversalOfAsync`). Reportes, cierre y conciliación de apertura cuentan solo Posted: original y
  reverso se netean igual que antes. El filtro de estado del listado ya no ofrece «Reversado».
- Correcciones de reglas heredadas del bootstrap (`TryCorrectLegacy*`, formas legacy y sus
  detectores), `AccountingChartBackfillService` (backfill de arranque fuera de Production y jerarquía) y
  los comandos `backfill-accounting-chart-hierarchy`, `backfill-sales-invoice-posting-rule`,
  `backfill-supplier-payment-posting-rules`, `backfill-supplier-credit-refund-posting-rules`,
  `backfill-opening-balance-posting-setup` y `remediate-purchase-return-postings`. Una empresa nueva
  recibe plan, período y reglas solo por `AccountingBootstrapStep`, que únicamente crea lo que falta.

Migración `AccountingSchemaCleanup` (fail-closed): aborta si una fila de `inventory_cost_postings` con
importe o asiento no tiene su obligación común, o si un asiento `Reversed` no tiene reverso enlazado
por `OriginalJournalEntryId`. Si pasa, los `Reversed` heredados quedan Posted (líneas, número y fechas
intactos) y se eliminan la tabla y la columna. `Down` restaura solo el esquema vacío. Histórico: consolidada
en `20261018014647_InitialProductionBaseline` (MIGRATION-SQUASH), que nace sin la tabla, sin la columna y sin
el estado `Reversed`.
