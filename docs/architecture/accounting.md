# Accounting — SSOT operativa

Única especificación funcional de Accounting. Incluye el contrato aprobado, sus
extensiones aprobadas pendientes y el estado real de implementación. No requiere
consultar ADRs ni otros documentos para determinar qué regla contable aplica.
Toda decisión funcional posterior se incorpora aquí; los ADRs solo registran razones
arquitectónicas y los documentos de avance solo resumen estado.

El sistema está en preproducción. La aprobación de este contrato **no significa que
el código ya lo cumpla**. No existe compatibilidad con políticas contables anteriores.

## Estado real de implementación

| Clasificación | Disponible / pendiente |
|---|---|
| KEEP | Core, plan de cuentas, reglas, períodos, motor de partida doble, idempotencia, numeración, lectura y reportes del libro implementados |
| KEEP | Patrones durables específicos de costos de inventario y apertura implementados (convergencia pendiente, CONTINUITY-05) |
| IMPLEMENTADO | ACCOUNTING-CONTINUITY-01: política operativa versionada por Company, obligación contable durable común con historial de intentos, intento inicial posterior al commit y reproceso manual individual para Compra, Gasto (confirmación de borrador y creación directa) y Retención emitida; anulación común de esos orígenes; gate mínimo de cierre; `AccountingPostingMode` retirado |
| BUG REAL | Ventas, devoluciones/NC, pagos, cobros, créditos de proveedor y caja siguen contabilizando en línea (excepción o warning según traductor); falta convergencia a la obligación durable |
| BUG REAL | El reverso actual fuerza fecha/período originales y modifica estado/metadatos del original; no cumple el contrato de libro cerrado |
| BUG REAL | El cierre exige obligaciones resueltas y asientos completos, pero no concilia orígenes sin obligación; costos puede reintentar Failed implícitamente |
| EXTEND | Disabled operativo, reproceso en bloque, reversos como obligación, integridad completa de cierre/reportes y manuales generales pendientes |

El resto del documento define el contrato vigente aprobado, tanto lo implementado
como las extensiones pendientes. No se presenta la continuidad uniforme como una
capacidad ya entregada.

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

La obligación es un concepto de negocio durable, **no el nombre aprobado de una tabla
nueva**. `InventoryCostPosting`, `OpeningBalancePosting` y `OpeningJournalEntryPosting`
son evidencia reutilizable. La fase técnica determinará generalización o abstracción
común, preservando una sola identidad y un solo pipeline conceptual por efecto.
No mantener simultáneamente dos obligaciones autoritativas para el mismo efecto.

### Core y configuración contable

`Account` tiene código, nombre, padre, tipo, naturaleza, estado activo y AllowsPosting.
Los tipos son Asset, Liability, Equity, Income y Expense; no son catálogos tributarios
ni decisiones por tenant. Código/tipo/naturaleza se conservan tras crear. La jerarquía
no admite ciclos ni padres de otra Company. El motor exige cuentas existentes, de la
misma empresa, activas y con AllowsPosting. No se introduce aquí una regla nueva que
deduzca AllowsPosting exclusivamente de tener o no hijos. La baja es lógica y no borra
líneas ni historia; los cambios de configuración solo afectan contabilizaciones nuevas.

`PostingRule` es configuración por Company y tipo de hecho, con líneas de cuenta,
naturaleza y componente monetario. El resolver trabaja por datos; el motor genérico
no agrega ramas por documento. Los traductores convierten hechos ya resueltos en
PostingFact. Los montos, descuentos e impuestos provienen del origen y las líneas
efectivas de la regla; no se resuelven desde campos legacy de débito/crédito planos.

Un asiento debe balancear, tener líneas monetarias válidas y cuentas verificadas;
en cada línea exactamente uno de Debe/Haber es positivo. No publicar encabezados
vacíos ni asientos parciales. La precisión monetaria existente es de dos decimales.
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
automática. Marcar `IsAccountingReq = true` exige Enabled vigente hoy y ninguna versión
posterior Disabled; en otro caso el cambio fiscal se rechaza.
Una desactivación no borra el libro ni resuelve pendientes; se rechaza si hay obligaciones
Pending/Failed o hechos que aún deberían tener resolución. El cambio no exime obligaciones
ya contraídas ni permite suprimir el reverso de asientos existentes.

Disabled no exige plan/reglas/cuentas para continuar la operación comercial y no genera
obligaciones nuevas de posting automático durante su vigencia. Mientras existan integraciones
automáticas que contabilizan en línea (Ventas, Pagos, Cobros, Costos, …) seleccionar Disabled
se rechaza: un Disabled parcial dejaría el libro inconsistente. La lectura del historial
se conserva. El uso voluntario de asientos manuales requiere activar Accounting.

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

Forma física (Compra, Gasto y Retención): el traductor del hecho registra la obligación
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
de su Tenant/Company (individual implementado con el mismo ejecutor del intento inicial;
en bloque pendiente). Cada obligación tiene aislamiento transaccional: un error no
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

Para un original en Closed/Locked, el original permanece **totalmente inmutable**, incluso
su estado y metadatos persistidos. La relación inversa se consulta desde el nuevo reverso
o evidencia append-only; no actualizar el original para marcarlo Reversed/enlazarlo.
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

Períodos no solapados por Company. Posting únicamente en un período abierto válido
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
Gate mínimo implementado: el cierre se rechaza si existe una obligación Pending/Failed con
`EntryDate` dentro del período, bajo lock exclusivo por Company frente al lock compartido de
la creación de obligaciones; la conciliación completa de orígenes sigue pendiente.
Locked no puede eludir el gate de cierre. Closed/Locked no permiten nuevas escrituras
en ese período ni edición, borrado o reescritura de lo ya registrado.

## 9. Reportes

Importes exclusivamente de JournalEntry/líneas con efecto contable, incluyendo el
original y el reverso según **sus respectivas fechas**. No excluir el original por
haber sido reversado ni forzar neto cero en un rango que solo contiene uno de los dos.
No reconstruir balances desde documentos operativos ni sumar las obligaciones como
si ya fueran asientos.

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
| Costos inventario | Kardex + SaleCostObligation/Allocation; costo operativo resuelto | InventoryCostPosting como patrón; reconocimiento antes de ajustes | Failed durable; sin retry implícito | Operación y costo trazables |
| Carga inicial | Lote de saldos + Inventario/CxC/CxP atómicos | Patrones OpeningBalancePosting/ASI; no reconstruir compras/ventas históricas | Failed sin deshacer lote | Apertura incompleta hasta conciliar; cierre propio protegido |
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
- Reembolso de crédito de proveedor: Debe Caja/Banco real y Haber Anticipos; reverso
  con las mismas cuentas e importes intercambiados. No depende de una cuenta genérica.
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

No confundir el ASI de apertura existente con una capacidad general de asientos manuales.
El flujo manual general es EXTEND pendiente, sin afirmar endpoints/UI inexistentes.
No habilitar desde un asiento manual cambios de stock, CxP/CxC, caja o documentos SRI.
Su corrección no altera el origen operativo. No se añade workflow de aprobación,
consolidación ni multimoneda en esta fase.

## 12. Outbox y mecanismos existentes

`ErpDbContext.SaveChangesAsync` publica Domain Events in-process y persiste el Outbox
con los datos de negocio: primera escritura, publicación in-process, segunda escritura
de efectos y commit en la misma transacción. El `OutboxProcessor` actual **solo marca procesado**; no
republica eventos hacia Accounting ni garantiza recuperación del posting. Un mensaje
procesado no demuestra que exista JournalEntry. Los jobs de SRI y Communications
tampoco son un pipeline de recuperación contable.

`InventoryCostPosting` es KEEP como evidencia durable; `InventoryCostAccounting`
actual puede recorrer Failed al procesar otro hecho de la factura. Esa recuperación
implícita no cumple el reproceso exclusivamente manual aprobado y debe corregirse
sin cambiar la valoración ni la prioridad de reconocimiento descritas en §10.
No duplicar ciegamente su implementación. Preservar las evidencias de apertura y costo
y resolver su convergencia técnica sin crear un segundo libro ni una cola paralela.

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
para sus consultas y administración; `accounting.settings.update` cambia el modo operativo,
`accounting.postings.view` consulta obligaciones e intentos y `accounting.postings.reprocess`
ejecuta el reproceso manual individual. El retry de costos usa StockManage y la apertura sus
permisos combinados existentes; aún no existe permiso de asientos manuales.

Auditar cambios de configuración y vigencia, creación/resolución/cancelación de obligación,
cada intento manual (usuario, motivo/contexto, fecha UTC, resultado, error y correlación),
asiento asociado, reverso y cierre/bloqueo. Conservar historia y ámbito; utilizar la
infraestructura de Entity Audit existente sin rediseñar contratos CLOSED. La futura
forma de registrar intentos debe ser durable, no solo telemetría ni logs.

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

## 16. Adopción pendiente y evidencia

Esta sección registra diferencias de implementación; no introduce otra política.
Rutas abreviadas desde `backend/src/`:

| Clasificación | Evidencia actual |
|---|---|
| BUG REAL | Application/Modules/Accounting/Posting/Translators: SalesInvoiceAuthorized lanza excepción de posting (Compra, Gasto y Retención emitida convergieron a la obligación durable en ACCOUNTING-CONTINUITY-01) |
| BUG REAL | PurchaseReturnAuthorized registra warning sin obligación genérica; CollectionApplied mezcla excepción con warning según destino |
| BUG REAL | La autorización de venta resuelve cuentas antes de completar el núcleo (la prevalidación IRBPNR de Compra se retiró en ACCOUNTING-CONTINUITY-01) |
| BUG REAL | Application/Modules/Accounting/UseCases/JournalEntries/JournalEntryReversal usa período/ejercicio originales; Domain/Modules/Accounting/Entities/JournalEntry.Reverse modifica el original |
| BUG REAL | El cierre consulta asientos y obligaciones Pending/Failed, pero no concilia orígenes que deberían haber generado obligación |
| BUG REAL | Application/Modules/Inventory/Costing/InventoryCostAccounting recorre Failed al procesar otros hechos de la factura |

Corte Compra/Gasto + Retención adoptado (ACCOUNTING-CONTINUITY-01). Siguientes cortes:
reversos como obligación y fecha efectiva de libro cerrado, demás integraciones automáticas,
convergencia de costos/apertura, reproceso en bloque, integridad completa de cierre/reportes
y manuales generales. Mientras el reverso general no converja, el reverso fallido de un asiento
Posted de Compra/Gasto/Retención bloquea su anulación (rollback completo). Cada corte requiere
reproducción, fix mínimo, tests focales y revisión de diff. No se autoriza un pipeline
paralelo ni reabrir reglas operativas CLOSED por esta consolidación.

PostgreSQL real es obligatorio para verificar transacciones, concurrencia, scope,
locks e invariantes de persistencia; no sustituir por SQLite, memoria o mocks.
No hay decisiones funcionales pendientes para consolidar este contrato; si la
implementación descubre una contradicción de dominio, se eleva la decisión necesaria.
