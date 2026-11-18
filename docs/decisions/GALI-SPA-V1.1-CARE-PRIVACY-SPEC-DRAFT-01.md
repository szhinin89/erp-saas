# Gali Spa V1.1 — Especificación técnica de expedientes, consentimientos, fotografías y privacidad (borrador)

> **REFERENCIA HISTÓRICA NO NORMATIVA — actualizada el 2026-11-18.** SPA-DEC-115/116 se recuperaron aprobadas por evidencia aportada por el propietario: [registro §9/10](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md). El [contrato central](GALI-SPA-V1.1-CONTRACT-SPEC-01.md) contiene el alcance funcional aprobado y O-23 cerrada en inclusión general; los detalles técnicos, legales y las fichas originales siguen pendientes según su registro. [ADR-051](ADR-051-gali-spa-v1.1-architecture.md) PROPOSED / NO APROBADO y [plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md) sin fases autorizadas. Se preserva el cuerpo previo como evidencia de su fecha: sus afirmaciones «solo título», alcance sin confirmar, alternativas y cierres pendientes describen la recuperación anterior y no el estado actual. No sustituye contrato/registro ni autoriza implementar.

**BORRADOR TÉCNICO DOCUMENTAL — NO ADR — NO CONTRATO APROBADO — NO IMPLEMENTACIÓN AUTORIZADA.**

Documento de desarrollo técnico (SPA-V1.1-DOC-03). Complementa, sin copiarlos, el registro de recuperación
[GALI-SPA-V1.1-RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) y el borrador de componentes operativos
[GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md) (en adelante «RECOVERY-01» y «OPERATIONS-SPEC»). Conserva
sus identificadores y desarrolla: expediente del cliente (§2), valoración facial (§3), valoración corporal (§4), consentimientos (§5), fotografías
(§6) y la matriz de seguridad y privacidad (§7). **Ninguna regla de este documento está aprobada por el propietario**; son propuestas hasta que se
apruebe expresamente un ADR y una especificación contractual. Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

## 0. Cómo leer este documento

### 0.1 Fuentes y su estado

- **Las fichas originales de Gali Spa** (formularios facial, corporal, consentimientos y expediente compacto) se encuentran en el proyecto ChatGPT del
  propietario. **Claude no las ha inspeccionado directamente**; su contenido llega a este documento solo a través de RECOVERY-01 y de la transmisión del
  propietario, y se etiqueta **[F] fuente externa pendiente de contraste**.
- Los contratos del ERP citados se verificaron en documentación y código del repositorio y se etiquetan **[E]**.
- No se cita ni se interpreta normativa legal de protección de datos: **no se ha verificado ninguna** [NV]. Su aplicación debe resolverla el propietario
  antes de aprobar retención, exportación o publicación.

### 0.2 Etiquetas

Las de OPERATIONS-SPEC: **[A]** y **[A, derivado]** (cita la sección de ADR-050 o de la especificación de Empleados), **[E]** existente en el ERP,
**[F]** fuente externa pendiente de contraste, **[R]** reconstruido, **[T]** propuesta técnica no aprobada, **[P]** pendiente (con alternativas, sin
elegir) y **[NV]** no verificado.

### 0.3 Reglas de lectura

1. Una alternativa no es una decisión; donde hay una laguna se enumeran alternativas con consecuencias y **no se elige ninguna**.
2. Los campos de formularios físicos **no son obligatorios en el software por sí mismos** (RECOVERY-01 §0, regla 2). En este documento «obligatoriedad»
   significa solo los mínimos de integridad necesarios para que un registro sea identificable (cliente, fecha, profesional responsable, usuario
   registrador y sucursal), y son una propuesta [T]; el resto está **pendiente [P]**.
3. Una ausencia de respuesta no equivale a una respuesta negativa en ningún antecedente clínico [T].
4. Spa no duplica BusinessPartner, Employee, Branch, usuarios, facturación ni auditoría.
5. Las pruebas de este documento usan el prefijo `SPA-CARE-T-`; las `SPA-T-*` son las de RECOVERY-01.
6. Los identificadores `SPA-CARE-*` son nuevos y no reemplazan ninguno de RECOVERY-01 ni de OPERATIONS-SPEC.

## 1. Mecanismos existentes del ERP verificados

Capacidades reales que condicionan el diseño. Que existan no las convierte en decisiones de Spa.

| Área | Capacidad verificada [E] | Evidencia | Implicación para Spa |
|---|---|---|---|
| BusinessPartner | Identidad con `Identification` (tipo y número), `Name` (`PersonName`), `LegalEntityTypeCode`, `CountryCode`, `IsActive`; roles por BP (`RoleType`); contactos con rol de contacto (`Commercial`, `Accounting`, `Management`, `Reception`, `Dispatch`, `Billing`, `Technical`, `Purchasing`, `Legal`, `Other`), teléfono, móvil y correo (`ContactInfo`) y ubicaciones. **No hay fecha de nacimiento ni contacto de emergencia** en el dominio de MasterData (búsqueda sin resultados) | `BusinessPartner.cs`, `BusinessPartnerContact.cs`, `ContactInfo.cs`, `ContactRole.cs`; [ADR-017](ADR-017-business-partner-scope.md) (FROZEN) | La edad, la fecha de nacimiento y el contacto de emergencia de las fichas no tienen campo en BusinessPartner; añadirlos a MasterData exigiría una autorización aparte |
| BusinessPartner | BP es del tenant (sin `CompanyId`) y se administra con permisos propios de MasterData (`masterdata.businesspartners.view/create/update/disable`) | `MasterDataPermissions.cs`; ADR-017 | Spa lee la identidad y no crea ni edita BP salvo mediante los flujos y permisos oficiales de MasterData |
| Almacenamiento | `IFileStorage` (`SaveAsync`, `GetAsync`, `DeleteAsync`) con implementación `LocalFileStorage` sobre el volumen de archivos de la API; **`DeleteAsync` borra físicamente** | `IFileStorage.cs`, `LocalFileStorage.cs`; [docs/deployment/README.md](../deployment/README.md) | Existe un almacén de archivos genérico; no se encontró cifrado en reposo ni registro de accesos de lectura [NV] |
| Almacenamiento | Las imágenes de Items guardan una referencia `StorageObjectId` y no la ruta; su activación y desactivación es lógica | `ItemImage.cs`; items.md | Hay un precedente de referencia y de desactivación lógica; no se verificó el resolutor del objeto de almacenamiento [NV] |
| Secretos | `ISecretProtector` (ASP.NET Data Protection) para secretos de configuración; **no** se usa para cifrar datos de negocio | `DataProtectionSecretProtector.cs` | No hay un mecanismo existente de cifrado de campos clínicos |
| Auditoría | Entity Audit append-only (`AuditRecordBase`, `IAuditService`, `IAuditReader<T>.GetByEntityAsync`), cada dominio con su propia entidad y handler; el actor se obtiene de `AuditActor`. **No se encontró un registro de accesos de lectura exitosos**; `ForbiddenAccessLoggingMiddleware` solo registra accesos denegados (401/403) con métricas | [audit-infrastructure.md](../architecture/audit-infrastructure.md); `IAuditReader.cs` | Auditar escrituras es un patrón existente; auditar consultas de expedientes y fotografías es una necesidad específica sin mecanismo [NV] |
| Permisos | El Kernel es la fuente única; convención `modulo.recurso.accion`; los perfiles existentes no reciben permisos nuevos. **El administrador de empresa (`Role = Admin`) tiene acceso total por su función (`HasAdminBypass`)** | [naming.md](../architecture/naming.md); [security.md](../architecture/security.md) | El rol Admin accedería por defecto a datos clínicos si no se decide lo contrario [P] |
| Acceso a sucursal | `CompanyUserBranch` y `IBranchAccessGuard` autorizan a un usuario por sucursal | `IBranchAccessGuard.cs`; [frozen-infrastructure.md](../architecture/frozen-infrastructure.md) | Es un posible mecanismo para limitar expedientes por sucursal; no es disponibilidad laboral |
| Empleados | `IEmployeeDirectory` es propuesta; Empleados no está implementado; Spa registra por separado profesional responsable y usuario (DEC-E4) | ADR-050 §13; spec Empleados §19 | La identidad del profesional depende de un contrato aún inexistente |
| Estándares | Fechas de negocio `DateOnly`; instantes UTC mostrados en `Company.Timezone`; precisión numérica con una SSOT única | [data-standards.md](../architecture/data-standards.md); [ADR-034](ADR-034-temporal-contract-single-source.md) | Las mediciones y fechas del expediente deben seguir esos estándares; no se fijan escalas aquí |
| Eliminación | Regla permanente del proyecto: anular o deshabilitar, nunca borrar físicamente registros de negocio | `CLAUDE.md` (reglas globales); [enforcement.md](../architecture/enforcement.md) | Contradice cualquier borrado físico de datos o fotografías clínicas sin una decisión expresa del propietario |
| Aislamiento | Filtro fail-closed por tenant y empresa para `ICompanyOperationalEntity` | [security.md](../architecture/security.md) | Las entidades de Spa deben ser de empresa |

## 2. Expediente del cliente (SPA-CARE-EXP-*)

**Origen:** RECOVERY-01 SPA-FC-01, FC-02, FC-03, FC-07, FC-08, SPA-CL-01, SPA-AR-01, SPA-AR-03, SPA-AR-04 ([F]/[R]/[T]/[A]).

### 2.1 Objetivo, evidencia y aprobación

- **Objetivo [R]:** conservar de forma longitudinal la información específica de atención estética de una persona, sin duplicar su identidad.
- **Evidencia:** campos de identificación y antecedentes de las fichas [F]; BusinessPartner como identidad [R]; separación profesional/usuario [A, ADR-050 §3
  DEC-E4 y §13].
- **Aprobación:** ninguna regla de este apartado está aprobada; solo la separación profesional/usuario lo está.

### 2.2 Modelo conceptual [T]

| Entidad candidata | Contenido | Relación |
|---|---|---|
| Expediente (`CareRecord`) | Empresa, cliente (referencia a BusinessPartner con rol Customer), número de ficha, código del caso, estado, fecha de apertura | `BusinessPartner` por referencia; sin copiar nombre, identificación ni contacto |
| Versión de antecedentes (`MedicalHistoryVersion`) | Expediente, fecha, autor, motivo del cambio, respuestas por categoría | Versión append-only; no se sobrescribe |
| Datos propios de la persona (`CareRecordPersonalData`) | Los datos que BusinessPartner no tiene y la ficha pide (fecha de nacimiento o edad, contacto de emergencia): **solo si se decide** | Ver alternativas 2.4 |
| Valoración | Facial o corporal (§3, §4) | Expediente, profesional, sucursal |
| Sesión | Atención realizada (OPERATIONS-SPEC §4) | Expediente, cita, profesional, sucursal |
| Consentimiento y fotografía | §5 y §6 | Expediente |

### 2.3 Reglas

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-CARE-EXP-01 | Un expediente referencia a un BusinessPartner activo con rol Customer de la empresa; Spa lee su nombre, identificación y contacto por proyección autorizada y **no los copia** | SPA-AR-01, SPA-FC-07 | [T] |
| SPA-CARE-EXP-02 | Número de ficha: único por empresa. Alternativa A: secuencia propia mediante la infraestructura de secuencias documentales existente; alternativa B: número libre digitado. El formato y si se reinicia son pendientes | SPA-FC-02 | [P] |
| SPA-CARE-EXP-03 | Código del caso: aparece en la ficha compacta; su significado y si identifica un caso por tratamiento, por persona o por trabajo académico **no están recuperados** | SPA-FC-02 | [P] |
| SPA-CARE-EXP-04 | Un cliente puede tener uno o varios expedientes: alternativa A, un expediente por cliente y empresa; alternativa B, varios (por caso o por sucursal) | SPA-CL-01 | [P] |
| SPA-CARE-EXP-05 | Cada valoración y cada sesión referencian al profesional responsable (`EmployeeId`), al usuario que registró (usuario ERP) y a la sucursal; ambos identificadores son distintos y se guardan por separado | SPA-AR-03, SPA-SG-02 | [A] (ADR-050 §3 DEC-E4, §13) para la distinción; campos [T] |
| SPA-CARE-EXP-06 | Los antecedentes se guardan como versiones append-only: corregir crea una versión nueva con autor, fecha y motivo; la versión anterior se conserva y no se muestra como vigente | SPA-FC-08 | [T] |
| SPA-CARE-EXP-07 | En cada categoría de antecedentes se distingue «sin responder», «no», «sí» y, cuando corresponda, texto libre. «Sin responder» no equivale a «no» | SPA-FC-03 | [T] |
| SPA-CARE-EXP-08 | Un expediente nunca se elimina; puede inactivarse. Quién lo cierra y qué impide un cierre son pendientes | regla permanente del proyecto | [T]; política [P] |
| SPA-CARE-EXP-09 | Una atención sobre un cliente cuyo BusinessPartner se desactiva conserva el expediente; no se crean atenciones nuevas hasta reactivarlo | SPA-AR-01 | [T] |
| SPA-CARE-EXP-10 | El expediente es de empresa; la sucursal es un atributo de cada atención y no del expediente | SPA-AR-04 | [T]; acceso entre sucursales [P] (ver 7.3) |

### 2.4 Alternativas para las lagunas

| Tema | Alternativa A | Alternativa B | Alternativa C | Consecuencias |
|---|---|---|---|---|
| Edad, fecha de nacimiento y contacto de emergencia | Campos propios del expediente (`CareRecordPersonalData`) | Ampliar BusinessPartner en MasterData | Registrar el contacto de emergencia como contacto de BP con rol `Other` | A no toca módulos FROZEN pero guarda datos personales fuera de MasterData; B exige autorizar cambios en un módulo FROZEN (ADR-017); C reutiliza contactos, sin fecha de nacimiento |
| Cliente inexistente al registrar una atención (SPA-AR-01) | Exigir que el BP ya exista, creado por MasterData con sus permisos | Ofrecer un flujo asistido que remita a MasterData sin duplicar el alta | — | En ambas, Spa no crea BP con permisos propios; el flujo exacto no está recuperado |
| Edad | Calcularla a partir de la fecha de nacimiento si se almacena | Guardar la edad declarada en la fecha de la ficha | — | La edad calculada cambia con el tiempo; la declarada es un dato histórico |

### 2.5 Flujos, estados e invariantes [T]

- **Abrir expediente:** buscar o seleccionar el cliente (exige el permiso de lectura de BP de MasterData) → comprobar BP activo con rol Customer → crear expediente con número de ficha → registrar la versión inicial de antecedentes (opcional).
- **Consultar:** requiere el permiso de consulta de Spa (7.2); no basta `employees.view`.
- **Corregir antecedentes:** versión nueva con motivo.
- **Estados candidatos:** `Activo` e `Inactivo`. «Cerrado» no está recuperado [P].
- **Invariantes candidatos:** un número de ficha único por empresa; un expediente siempre referencia a un BP; las versiones de antecedentes nunca se modifican ni eliminan; profesional y usuario registrador se guardan siempre por separado.

### 2.6 Validaciones, permisos y auditoría [T]

Validaciones: cliente activo y de la empresa; profesional existente y activo en la fecha según `IEmployeeDirectory`; motivo de corrección acotado; longitud acotada de textos. Permisos: ver 7.2. Auditoría: Entity Audit de la creación, cambio de estado y cada versión de antecedentes (SPA-AU-01, SPA-AU-02).

### 2.7 API conceptual, frontend e integraciones [T]

API: abrir, consultar, listar por cliente, registrar y corregir antecedentes, inactivar. Frontend: ficha del cliente con secciones (general, antecedentes, valoraciones, sesiones, fotografías, consentimientos); la lista de clientes no muestra información clínica. Integraciones: BusinessPartner (identidad), Empleados (profesional), Branch, agenda y sesiones (OPERATIONS-SPEC §2 y §4).

### 2.8 Pruebas [T]

| ID | Escenario | Relación |
|---|---|---|
| SPA-CARE-T-EXP-01 | El expediente referencia un BP activo con rol Customer y no copia su identidad | SPA-T-01 |
| SPA-CARE-T-EXP-02 | El número de ficha es único por empresa bajo concurrencia | — |
| SPA-CARE-T-EXP-03 | Corregir antecedentes crea una versión nueva y conserva la anterior | SPA-INV-06 |
| SPA-CARE-T-EXP-04 | «Sin responder» no se interpreta como «no» | — |
| SPA-CARE-T-EXP-05 | Profesional y usuario registrador se guardan por separado | SPA-T-03 |
| SPA-CARE-T-EXP-06 | `employees.view` no permite consultar un expediente | SPA-T-05 |
| SPA-CARE-T-EXP-07 | El expediente de otra empresa o tenant no es visible | SPA-T-16 |

### 2.9 Riesgos y dependencias

Dependencias: BusinessPartner [E], Empleados [A]/[T] (no implementado), secuencias documentales [E], Kernel de permisos. Riesgos: datos personales sin campo en BP (2.4); número de ficha y código del caso sin definición; acceso entre sucursales sin decisión.

## 3. Valoración facial (SPA-CARE-FAC-*)

**Origen:** RECOVERY-01 SPA-FC-04, SPA-AT-01, SPA-AT-02 [F]. Contenido [F] pendiente de contraste directo con la ficha facial.

### 3.1 Objetivo, evidencia y aprobación

- **Objetivo [R]:** registrar la valoración estética inicial de rostro y su evolución.
- **Evidencia:** campos recuperados de la ficha facial [F]. **Aprobación:** ninguna; no se presenta como diagnóstico médico (SPA-FC-04) [F].

### 3.2 Campos recuperados y tratamiento propuesto

Ningún campo se declara obligatorio (regla de lectura 2); la columna «Valores» muestra los recuperados [F] y la «Naturaleza» es una propuesta [T].

| Campo [F] | Naturaleza propuesta [T] | Valores recuperados | Obligatoriedad |
|---|---|---|---|
| Motivo de consulta | Texto libre acotado | — | [P] |
| Condición de piel | Selección | Normal, seca, grasa, mixta | [P] |
| Sensibilidad | Selección o texto | Sin valores cerrados recuperados | [P] |
| Fototipo cutáneo | Selección de un valor | Clasificación de Fitzpatrick I a VI | [P] |
| Alteraciones observadas | Selección múltiple + texto | Manchas, acné, deshidratación y otras | [P] |
| Zona y tiempo de evolución | Texto o selección | — | [P] |
| Exposición solar y protección | Texto o selección | — | [P] |
| Observaciones del profesional | Texto libre acotado | — | [P] |
| Diagnóstico médico previo, si existe | Texto libre acotado | — | [P] |
| Objetivo del tratamiento | Texto libre acotado | — | [P] |
| Procedimiento, productos y equipos previstos | Referencias al procedimiento (OPERATIONS-SPEC §3) y texto | — | [P] |
| Precauciones | Texto libre acotado | — | [P] |
| Decisión | Selección de un valor | Proceder, adaptar, posponer | [P] |
| Evolución y seguimiento | Se registra por sesión (OPERATIONS-SPEC §4), no en la valoración | — | [T] |

### 3.3 Reglas

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-CARE-FAC-01 | La valoración facial pertenece a un expediente, fija fecha de negocio (`DateOnly`), profesional responsable, usuario registrador y sucursal | SPA-FC-04 | [T] |
| SPA-CARE-FAC-02 | Una valoración es un registro con versiones append-only: una corrección genera una versión nueva con autor, fecha y motivo | SPA-FC-08 | [T] |
| SPA-CARE-FAC-03 | Una valoración estética **no** se rotula ni exporta como diagnóstico médico; el campo «diagnóstico médico previo» es información declarada, no un diagnóstico de Spa | SPA-FC-04 | [F] |
| SPA-CARE-FAC-04 | Fototipo: un único valor entre I y VI o «sin evaluar». La clasificación es la recuperada de la ficha | SPA-FC-04 | [F]; lista de valores [T] |
| SPA-CARE-FAC-05 | La decisión (proceder, adaptar o posponer) es de un solo valor y no ejecuta ninguna acción automática (no crea citas ni consume sesiones) | SPA-FC-04 | [T] |
| SPA-CARE-FAC-06 | Los mínimos de integridad de una valoración son solo los de la regla de lectura 2; los campos clínicos restantes son opcionales hasta decidir lo contrario | SPA-FC-03 | [T]; obligatoriedad real [P] |
| SPA-CARE-FAC-07 | Si el formulario antiguo y el actualizado difieren, la versión digital no asume equivalencia; el contraste de versiones es previo al contrato | RECOVERY-01 §12 | [P] |

### 3.4 Flujos, estados, permisos, auditoría y pruebas [T]

- **Flujos:** registrar la valoración inicial (puede guardarse incompleta como borrador, alternativa [P]); registrar la decisión; corregir con motivo.
- **Estados candidatos:** `Registrada`, `Corregida` (visible como versión). Cierre y bloqueo: [P].
- **Permisos:** ver 7.2 (`registrar valoraciones`, `consultar expedientes`).
- **Auditoría:** Entity Audit por valoración y versión; sin cambios silenciosos.
- **API y frontend:** crear y consultar valoraciones por expediente; formulario con secciones plegables y campos opcionales; sin diseño aprobado.
- **Pruebas:** SPA-CARE-T-FAC-01 (una valoración puede guardarse sin campos clínicos opcionales y con los mínimos de integridad), -02 (corregir conserva la versión anterior), -03 (el rótulo no indica diagnóstico médico), -04 (fototipo admite solo I a VI o sin evaluar), -05 (la decisión no crea citas ni consume sesiones). Relación con SPA-T-06.

### 3.5 Riesgos y dependencias

Dependen de Empleados [A]/[T], el expediente (§2) y el catálogo de procedimientos (OPERATIONS-SPEC §3). Riesgo principal: contenido [F] sin contraste directo.

## 4. Valoración corporal (SPA-CARE-COR-*)

**Origen:** RECOVERY-01 SPA-FC-05, SPA-FC-06 [F] y GS-COR del documento maestro (integrado en RECOVERY-01).

### 4.1 Objetivo, evidencia y aprobación

- **Objetivo [R]:** registrar la valoración corporal inicial, sus mediciones y su comparación a lo largo del tratamiento.
- **Evidencia:** campos de la ficha corporal [F]. **Aprobación:** ninguna.

### 4.2 Campos recuperados [F]

Zona corporal a tratar; motivo de consulta; objetivos (relajación, reducción, tonificación u otros); antecedentes relevantes (ver §2); hallazgos
iniciales; procedimiento previsto; equipo o aparatología; precauciones; decisión de proceder, adaptar o posponer; mediciones (4.3); evolución.
Ningún campo se declara obligatorio.

### 4.3 Mediciones corporales

| Medición [F] | Unidad de referencia [F] | Observación |
|---|---|---|
| Peso | kg | |
| Brazo derecho e izquierdo | cm | Dos mediciones |
| Abdomen alto | cm | |
| Abdomen medio (a la altura del ombligo) | cm | |
| Abdomen bajo | cm | |
| Cadera | cm | |
| Muslo derecho e izquierdo | cm | Dos mediciones |

Las mediciones se comparan en tres momentos: inicial, de control y final [F]. Fecha de medición y observaciones o evolución acompañan a cada conjunto.

### 4.4 Reglas

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-CARE-COR-01 | La valoración corporal pertenece a un expediente y fija fecha de negocio, profesional responsable, usuario registrador y sucursal | SPA-FC-05 | [T] |
| SPA-CARE-COR-02 | Una medición es un conjunto (`BodyMeasurementSet`) con fecha de negocio (`DateOnly`) y tipo `Inicial`, `Control` o `Final`; cada medida es opcional dentro del conjunto | SPA-FC-06 | [T] |
| SPA-CARE-COR-03 | Un conjunto de mediciones no se sobrescribe: corregirlo crea un conjunto o una versión nueva con motivo, autor y fecha | SPA-FC-08 | [T] |
| SPA-CARE-COR-04 | Las diferencias entre conjuntos (inicial frente a control o final) son **derivadas en lectura** y no se almacenan | SPA-FC-06 | [T] |
| SPA-CARE-COR-05 | Los valores numéricos y las fechas respetan los estándares oficiales de decimales y fechas; **no se fijan aquí escalas ni tipos SQL** y su definición se verifica contra `data-standards.md` antes de implementar. Si una medida es «no medida» se distingue de cero | SPA-FC-06 | [T]; escala [NV] |
| SPA-CARE-COR-06 | Frecuencia de medición y asociación obligatoria de un conjunto con una sesión: no recuperadas | SPA-FC-06 | [P]; alternativa A, vínculo opcional a sesión; alternativa B, vínculo obligatorio |
| SPA-CARE-COR-07 | Cuántos controles son posibles y si el «final» es único: no recuperado | SPA-FC-06 | [P] |
| SPA-CARE-COR-08 | La decisión (proceder, adaptar o posponer) es de un solo valor y no ejecuta acciones automáticas | SPA-FC-05 | [T] |

### 4.5 Flujos, estados, permisos, auditoría y pruebas [T]

- **Flujos:** registrar valoración inicial y medición `Inicial` → registrar controles → registrar medición `Final`; consultar la comparación.
- **Estados candidatos:** `Registrada` y versiones; la comparación no es un estado.
- **Permisos y auditoría:** ver 7.2; Entity Audit de cada conjunto y su corrección.
- **API y frontend:** registrar y consultar valoraciones y mediciones; tabla comparativa de solo lectura; gráfica opcional [P]; formularios con el componente decimal oficial.
- **Pruebas:** SPA-CARE-T-COR-01 (medidas opcionales dentro de un conjunto), -02 (una corrección conserva el conjunto anterior), -03 (las diferencias no se almacenan y coinciden con los conjuntos), -04 (las medidas respetan unidades y precisión oficial, SPA-T-08), -05 («no medida» es distinto de cero), -06 (fechas como `DateOnly`).

### 4.6 Riesgos y dependencias

Dependencias: expediente (§2), estándares de datos [E] y sesiones. Riesgos: escala decimal y número de controles sin definir; ficha [F] sin contraste.

## 5. Consentimientos (SPA-CARE-CNS-*)

**Origen:** RECOVERY-01 SPA-CN-01 a CN-06 [F]/[P] y SPA-INV-05.

### 5.1 Objetivo, evidencia y aprobación

- **Objetivo [R]:** conservar la evidencia de que la persona fue informada y decidió, y distinguir cada autorización.
- **Evidencia:** formularios de consentimiento [F]. **Aprobación:** ninguna. **No se verifica validez legal** de ninguna forma de firma [NV].

### 5.2 Autorizaciones diferenciadas

| Tipo | Qué autoriza [F] | Independiente de las demás | Estado |
|---|---|---|---|
| Consentimiento para el procedimiento | Realizar el procedimiento tras información sobre riesgos, limitaciones y cuidados; aceptación o rechazo y firma | Sí | [F] |
| Fotografías de seguimiento | Capturar fotografías para el registro y seguimiento del tratamiento | Sí; no autoriza publicación | [F] |
| Publicación promocional | Publicar fotografías en redes sociales o materiales promocionales | Sí | [F] |
| Uso académico | Usar datos codificados y fotografías en una tesis o sustentación; documento particular | Sí; se distingue de la publicidad | [F]; funcionalidad **pendiente de confirmar [P]** |
| Retiro de autorización | La persona retira una autorización para sesiones futuras | Por autorización | [F] |
| Interrupción de la atención | La persona puede detener una sesión | Por sesión | [F] |

### 5.3 Modelo conceptual [T]

| Entidad candidata | Contenido |
|---|---|
| Definición de consentimiento (`ConsentDefinition`) | Tipo, versión, texto inmutable, fecha de vigencia, estado; el texto versionado nunca se edita |
| Registro de consentimiento (`ConsentRecord`) | Expediente, tipo, versión presentada, procedimiento relacionado (si aplica), decisión (aceptado o rechazado), fecha, identidad de quien consiente, evidencia, estado |
| Retiro (`ConsentWithdrawal`) | Registro retirado, fecha, motivo, autor, alcance (sesiones futuras) |
| Interrupción (`SessionInterruption`) | Sesión, instante, motivo, autor |

### 5.4 Reglas

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-CARE-CNS-01 | Cada tipo de autorización es un registro separado; aceptar uno no implica aceptar otro | SPA-CN-06, SPA-INV-05 | [F]; modelo [T] |
| SPA-CARE-CNS-02 | El texto de un consentimiento está versionado; un registro referencia la versión presentada y esa versión no se modifica | SPA-CN-05 | [T] |
| SPA-CARE-CNS-03 | Un registro de consentimiento no se edita ni se elimina; un cambio de decisión es un registro nuevo o un retiro con fecha, motivo y autor | SPA-CN-05 | [T] |
| SPA-CARE-CNS-04 | Retiro: una autorización retirada **deja de habilitar usos futuros**; no revierte las atenciones ya realizadas ni los documentos ya emitidos. Qué ocurre con material ya publicado fuera del ERP no es controlable por el ERP | SPA-CN-05 | [T]; efectos sobre sesiones y material publicado [P] |
| SPA-CARE-CNS-05 | Interrupción: una sesión interrumpida se registra con instante y motivo y no se marca como realizada por completo; su efecto sobre un paquete o su cobro es pendiente | SPA-CN-05; OPERATIONS-SPEC AGE-10 | [T]; efectos [P] |
| SPA-CARE-CNS-06 | Una sesión que requiere un consentimiento de procedimiento vigente no puede registrarse como realizada sin él | SPA-CN-01 | [T]; si se admite excepción o consentimiento verbal: [P] |
| SPA-CARE-CNS-07 | Uso académico: se documenta como autorización separada solo si el propietario confirma que pertenece al producto; hasta entonces no se implementa ni se asume | SPA-CN-04 | [P] |
| SPA-CARE-CNS-08 | Quién consiente: la persona atendida; un representante (menores u otros casos) **no está recuperado** | SPA-CN-01 | [P] |

### 5.5 Mecanismos de firma, evidencia y conservación (propuestas, ninguna aprobada)

| Aspecto | Alternativa A | Alternativa B | Alternativa C | Consecuencias |
|---|---|---|---|---|
| Firma | Documento físico firmado, escaneado y adjuntado como evidencia | Firma manuscrita capturada en pantalla y guardada como imagen con metadatos | Aceptación electrónica sin imagen de firma (datos de quien acepta, fecha y versión) | A requiere gestión de archivos y es el más cercano a las fichas; B requiere captura y almacenamiento de imágenes; C es la más simple pero la menos parecida a las fichas. La validez legal de cada una no está verificada |
| Versionado del texto | Una versión vigente por tipo y vigencias consecutivas | Varias vigentes simultáneas (por procedimiento) | — | B permite consentimientos por procedimiento; A es más simple |
| Conservación | Sin plazo definido; solo inactivación | Plazo de retención con inactivación posterior | — | Un plazo requiere decisión del propietario y normativa aplicable; **ninguna alternativa elimina físicamente** |
| Evidencia | Hash del texto presentado y metadatos del acto | Solo referencia a la versión | — | A permite verificar que el texto no cambió |

### 5.6 Flujos, estados, permisos, auditoría y pruebas [T]

- **Flujos:** presentar la versión vigente → registrar la decisión y la evidencia → habilitar la atención; retirar; registrar interrupción.
- **Estados candidatos del registro:** `Vigente`, `Retirado`. De la definición: `Vigente` y `Reemplazada`.
- **Permisos:** gestionar consentimientos y consultarlos (7.2); la gestión de plantillas de texto es un permiso aparte [T].
- **Auditoría:** Entity Audit del registro, el retiro y la interrupción; la consulta de la evidencia se registra como acceso (7.4).
- **API y frontend:** registrar, consultar, retirar; vista de consentimientos del expediente con tipo, versión y estado; sin estilos de firma definidos.
- **Pruebas:** SPA-CARE-T-CNS-01 (cada tipo es independiente, SPA-T-10 y SPA-T-11), -02 (el texto de una versión no cambia), -03 (un registro no se edita ni elimina), -04 (un retiro bloquea usos futuros sin alterar atenciones pasadas), -05 (la interrupción no marca la sesión como realizada completa), -06 (una sesión sin consentimiento de procedimiento vigente no puede registrarse como realizada), -07 (el uso académico no existe como funcionalidad hasta confirmarse).

### 5.7 Riesgos y dependencias

Validez legal de la firma [NV]; efectos de un retiro sobre lo publicado; consentimientos de menores no recuperados; almacenamiento de evidencia (6); versiones de formularios sin contraste.

## 6. Fotografías (SPA-CARE-FOT-*)

**Origen:** RECOVERY-01 SPA-AT-03, SPA-CN-02 a CN-06, SPA-FO-01 ([F]/[T]).

### 6.1 Objetivo, evidencia y aprobación

- **Objetivo [R]:** conservar fotografías de seguimiento comparables, con su finalidad y autorización, y controlar quién puede verlas.
- **Evidencia:** las fichas indican condiciones comparables (iluminación, distancia, ángulo, sin filtros) y referencias o códigos de fotografías [F].
- **Aprobación:** ninguna. **No se propone publicación automática ni acceso general a fotografías.**

### 6.2 Modelo conceptual [T]

| Entidad candidata | Contenido |
|---|---|
| Fotografía (`CarePhoto`) | Expediente, sesión o valoración de origen, referencia de almacenamiento, código de la ficha, zona (rostro o zona corporal), ángulo, instante de captura, profesional, usuario registrador, sucursal, nota de condiciones, estado |
| Grupo comparativo | Conjunto de fotografías que se comparan (antes, controles, después) |
| Acceso (`PhotoAccessLog`) | Quién consultó o exportó qué fotografía, cuándo y para qué: **necesidad específica sin mecanismo existente** |

### 6.3 Reglas

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-CARE-FOT-01 | Una fotografía pertenece a un expediente y a una atención (sesión o valoración) y guarda quién la tomó y quién la registró | SPA-AT-03 | [T] |
| SPA-CARE-FOT-02 | El archivo se almacena mediante `IFileStorage` y se referencia por identificador; la ruta física no se expone. La imagen **no se publica con una URL pública**: se sirve mediante un endpoint autorizado | SPA-FO-01 | [T] |
| SPA-CARE-FOT-03 | La finalidad de uso no es un atributo del archivo sino una verificación de autorización en el momento del uso: ver en seguimiento exige el consentimiento de fotografías de seguimiento vigente; usar en publicidad exige el de publicación promocional; el académico, el suyo | SPA-CN-06, SPA-INV-05 | [T] |
| SPA-CARE-FOT-04 | Ninguna fotografía se publica, exporta ni comparte automáticamente; toda exportación es una acción explícita con permiso propio | SPA-FO-01 | [T] |
| SPA-CARE-FOT-05 | Las fotografías no aparecen en listados generales ni en vistas de agenda; solo dentro del expediente y para quien tenga el permiso | SPA-SG-06 | [T] |
| SPA-CARE-FOT-06 | Cada consulta o exportación de una fotografía se registra en un registro de accesos (usuario, instante, fotografía y finalidad) | SPA-AU-02 | [T]; mecanismo inexistente [NV] |
| SPA-CARE-FOT-07 | Las fotografías se desactivan lógicamente; **no se invoca `DeleteAsync` sobre ellas** salvo una decisión expresa del propietario, porque la regla permanente del proyecto prohíbe el borrado físico de registros | regla permanente | [T]; retención y eliminación [P] |
| SPA-CARE-FOT-08 | Retiro de la autorización de seguimiento: la fotografía deja de poder consultarse o usarse según el alcance que se decida; conservarla, bloquearla o anonimizarla es pendiente | SPA-CN-05 | [P] |
| SPA-CARE-FOT-09 | Metadatos: el archivo puede contener metadatos del dispositivo (ubicación, hora); si se eliminan al guardar no está decidido | SPA-AT-03 | [P] |
| SPA-CARE-FOT-10 | Formatos admitidos, tamaño máximo, compresión y número de fotografías por atención: no recuperados | SPA-AT-03 | [P] |
| SPA-CARE-FOT-11 | Condiciones comparables (iluminación, distancia, ángulo y ausencia de filtros) se registran como nota de captura; no se verifican automáticamente | SPA-AT-03 | [F]; registro [T] |

### 6.4 Alternativas para las lagunas

| Tema | Alternativa A | Alternativa B | Alternativa C | Consecuencias |
|---|---|---|---|---|
| Retiro de autorización (FOT-08) | Bloquear el acceso y conservar el archivo | Anonimizar o recortar la identificación visible | Eliminación física | C contradice la regla permanente del proyecto y requiere decisión expresa; A conserva el dato bajo control |
| Registro de accesos (FOT-06) | Tabla propia `PhotoAccessLog` | Ampliar la infraestructura de auditoría | — | B modifica una infraestructura FROZEN y requiere autorización; A es una entidad nueva de Spa |
| Cifrado | Sin cifrado propio; depender del almacenamiento | Cifrado de archivos antes de guardar con el mecanismo de protección de datos | — | B es un mecanismo nuevo no verificado; el ERP hoy cifra secretos, no archivos de negocio [E] |

### 6.5 Flujos, estados, permisos, auditoría, API y pruebas [T]

- **Flujos:** subir una fotografía a una atención (con comprobación del consentimiento de seguimiento vigente) → consultar dentro del expediente → comparar un grupo → exportar con permiso → desactivar.
- **Estados candidatos:** `Activa`, `Inactiva`; bloqueada por retiro [P].
- **Permisos:** consultar fotografías, registrar fotografías, exportar (7.2).
- **Auditoría:** Entity Audit de alta y desactivación más el registro de accesos (FOT-06).
- **API conceptual y frontend:** subir, consultar metadatos, ver (flujo autorizado), comparar, exportar, desactivar; visor dentro del expediente; miniaturas solo con permiso; sin cargar imágenes en listados.
- **Pruebas:** SPA-CARE-T-FOT-01 (subir sin consentimiento de seguimiento vigente se rechaza), -02 (la finalidad promocional exige su autorización, SPA-T-11), -03 (no hay acceso sin permiso), -04 (cada consulta queda registrada), -05 (una fotografía no se elimina físicamente), -06 (las fotografías no aparecen en listados ni en la agenda), -07 (aislamiento por empresa), -08 (un retiro bloquea el uso futuro según la política aprobada, pendiente).

### 6.6 Riesgos y dependencias

Dependencias: `IFileStorage` [E], consentimientos (§5), permisos (7). Riesgos: sin registro de accesos ni cifrado de archivos [NV]; el borrado físico del almacén contradice la regla del proyecto; metadatos y formatos sin definir; el volumen de archivos es único para API y Worker (docs/deployment/README.md), por lo que su respaldo y restauración deben incluir las fotografías.

## 7. Seguridad y privacidad (SPA-CARE-SEG-*)

**Origen:** RECOVERY-01 SPA-SG-01 a SG-06, SPA-AU-01, SPA-AU-02, SPA-INV-03, INV-04, INV-05, INV-07.

### 7.1 Reglas transversales

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-CARE-SEG-01 | **Ni `employees.view` ni ningún permiso de `employees.*` o `access.*` habilita el acceso a expedientes, valoraciones, consentimientos o fotografías**; Spa define permisos propios | SPA-SG-01, SPA-SG-06 | [A, derivado] (ADR-050 §6 y §13) |
| SPA-CARE-SEG-02 | Una asignación laboral a una sucursal no concede acceso a datos clínicos de esa sucursal | SPA-SG-01 | [A] (ADR-050 §6 y §8) |
| SPA-CARE-SEG-03 | Los permisos de Spa se declaran solo en el Kernel y ningún perfil existente los recibe automáticamente | SPA-SG-04 | [T] |
| SPA-CARE-SEG-04 | El acceso total del rol Admin a datos clínicos es una decisión pendiente: alternativa A, el rol Admin conserva su acceso total por su función; alternativa B, los permisos clínicos de Spa se excluyen del acceso total y deben asignarse explícitamente | SPA-SG-03 | [P] |
| SPA-CARE-SEG-05 | Las proyecciones exponen solo lo necesario para cada operación; la agenda no muestra datos clínicos | SPA-SG-06 | [T] |
| SPA-CARE-SEG-06 | Profesional responsable y usuario registrador se guardan por separado y se auditan por separado | SPA-SG-02 | [A] (DEC-E4) |

### 7.2 Matriz de operaciones y permisos candidatos

Ninguno de estos permisos existe; son nombres candidatos que siguen `modulo.recurso.accion` y se declararían en el Kernel.

| Operación | Permiso candidato [T] | Requisitos adicionales | Auditoría |
|---|---|---|---|
| Consultar el expediente y sus listados | `spa.records.view` | Aislamiento por empresa; alcance por sucursal [P] (7.3) | Entity Audit de escritura; registro de acceso de lectura [P] |
| Abrir o inactivar expediente | `spa.records.manage` | BP activo con rol Customer | Entity Audit |
| Registrar valoraciones (facial y corporal) | `spa.assessments.create` | Profesional válido en la fecha | Entity Audit |
| Modificar valoraciones | `spa.assessments.update` | Versión con motivo | Entity Audit |
| Modificar antecedentes | `spa.history.update` | Permiso separado del de valoraciones por su sensibilidad | Entity Audit |
| Registrar sesiones | `spa.sessions.create` | Consentimiento de procedimiento vigente | Entity Audit |
| Consultar fotografías | `spa.photos.view` | Consentimiento vigente para la finalidad | Registro de acceso (FOT-06) |
| Subir fotografías | `spa.photos.create` | Consentimiento de seguimiento vigente | Entity Audit |
| Exportar expediente o fotografías | `spa.records.export` | Permiso separado; no incluido en los de consulta | Registro de acceso y Entity Audit |
| Gestionar consentimientos (registrar y retirar) | `spa.consents.manage` | Versión vigente del texto | Entity Audit |
| Administrar textos de consentimiento | `spa.consents.templates` | — | Entity Audit |
| Consultar el historial de auditoría de un expediente | `spa.audit.view` | Permiso restringido | Registro de acceso |
| Acceso a expedientes de otras sucursales | Definición pendiente (7.3) | `CompanyUserBranch` u otro mecanismo [P] | Registro de acceso |

### 7.3 Acceso entre sucursales

Alternativa A: el expediente es de empresa y todo usuario con el permiso lo ve. Alternativa B: se limita a las sucursales donde el usuario tiene
`CompanyUserBranch`, usando el patrón de `IBranchAccessGuard` [E]. Alternativa C: la sucursal de cada atención restringe su visibilidad, no la del expediente.
Consecuencias: A es más simple pero amplía el acceso; B exige resolver el caso de un cliente atendido en varias sucursales; C fragmenta la historia del cliente.
**No hay decisión recuperada [P].**

### 7.4 Mecanismos existentes frente a necesidades específicas de Spa

| Necesidad | Mecanismo existente [E] | Brecha [NV] |
|---|---|---|
| Autorización por operación | Permisos del Kernel y políticas `perm:` | Catálogo de Spa inexistente; el rol Admin tiene acceso total |
| Aislamiento por tenant y empresa | Filtros fail-closed | Ninguna si las entidades son de empresa |
| Acceso por sucursal | `CompanyUserBranch`, `IBranchAccessGuard` | Decidir alcance (7.3) |
| Auditoría de escritura | Entity Audit | Entidades y handlers de Spa por crear |
| Auditoría de lectura | No se encontró | Mecanismo específico por definir |
| Almacenamiento de archivos | `IFileStorage` local | Sin cifrado en reposo ni control de acceso propio; `DeleteAsync` físico |
| Protección de datos sensibles | `ISecretProtector` para secretos | Sin cifrado de campos clínicos |
| Retención y eliminación | Regla de no borrado físico | Política de retención inexistente |
| Exportación | Patrones de reportes de otros módulos | Contrato propio de exportación clínica inexistente |

### 7.5 Estados, API y frontend [T]

Sin estados propios. API: cada operación declara permiso, alcance y registro de acceso (SPA-API-01). Frontend: ocultar acciones sin permiso (la autoridad es el backend);
indicar con claridad cuándo un dato es sensible; no mostrar datos clínicos en listados, notificaciones ni la agenda; sin diseño aprobado (SPA-UX-01).

### 7.6 Pruebas [T]

| ID | Escenario | Relación |
|---|---|---|
| SPA-CARE-T-SEG-01 | Un usuario con `employees.view` y sin permiso de Spa no accede a un expediente | SPA-T-05 |
| SPA-CARE-T-SEG-02 | Una asignación laboral a una sucursal no concede acceso a sus expedientes | SPA-T-04 |
| SPA-CARE-T-SEG-03 | Modificar antecedentes exige un permiso distinto del de consulta y del de valoraciones | SPA-T-05 |
| SPA-CARE-T-SEG-04 | Exportar exige un permiso propio y deja registro | SPA-T-17 |
| SPA-CARE-T-SEG-05 | Un perfil existente no recibe los permisos nuevos de Spa | SPA-SG-04 |
| SPA-CARE-T-SEG-06 | Profesional y usuario registrador son distintos y ambos auditados | SPA-T-03 |
| SPA-CARE-T-SEG-07 | La agenda y los listados no exponen datos clínicos | SPA-SG-06 |
| SPA-CARE-T-SEG-08 | Un usuario de otra empresa o tenant no accede | SPA-T-16 |
| SPA-CARE-T-SEG-09 | El comportamiento del rol Admin sigue la decisión aprobada (pendiente) | SPA-CARE-SEG-04 |

### 7.7 Riesgos y dependencias

Sin mecanismo de auditoría de lectura ni cifrado de archivos [NV]; acceso total del rol Admin; acceso entre sucursales sin decisión; política de retención y
exportación sin definir; normativa de protección de datos no verificada; Empleados no implementado.

## 8. Decisiones históricas conservadas

- **SPA-DEC-116 — Revisión y consolidación del alcance de Gali Spa V1.1:** aprobada y confirmada por el propietario; solo el título está confirmado y su
  texto íntegro y alcance literal **no están recuperados**. Este borrador no deduce su alcance del título.
- No se declaran existentes ni aprobadas las decisiones `SPA-DEC-001` a `SPA-DEC-115`.
- Ningún identificador de RECOVERY-01 ni de OPERATIONS-SPEC se ha reemplazado.

## 9. Trazabilidad

| Regla de este borrador | RECOVERY-01 | OPERATIONS-SPEC |
|---|---|---|
| SPA-CARE-EXP-01 a EXP-10 | SPA-AR-01, AR-03, AR-04, FC-01, FC-02, FC-03, FC-07, FC-08, CL-01 | §2.4 (AGE-13), §4 (sesiones) |
| SPA-CARE-FAC-01 a FAC-07 | SPA-FC-04, FC-08, AT-01, AT-02 | §3 (procedimientos) |
| SPA-CARE-COR-01 a COR-08 | SPA-FC-05, FC-06, FC-08 | §3, §4 |
| SPA-CARE-CNS-01 a CNS-08 | SPA-CN-01 a CN-06, INV-05 | AGE-10 |
| SPA-CARE-FOT-01 a FOT-11 | SPA-AT-03, CN-02 a CN-06, FO-01, AU-02 | — |
| SPA-CARE-SEG-01 a SEG-06 | SPA-SG-01 a SG-06, AU-01, AU-02, INV-03, INV-04, INV-05, INV-07 | §2.8 |

## 10. Vacíos de evidencia y riesgos

| # | Vacío o riesgo | Efecto |
|---|---|---|
| 1 | Las fichas originales no han sido inspeccionadas por Claude | Todo el contenido [F] sigue sin contraste directo; los campos y valores pueden diferir |
| 2 | Decisiones históricas `SPA-DEC-*` no recuperadas, y texto de SPA-DEC-116 | No se puede afirmar qué se aprobó sobre expedientes, consentimientos y privacidad |
| 3 | BusinessPartner no tiene fecha de nacimiento ni contacto de emergencia | Datos de la ficha sin campo; cualquier extensión de MasterData exige autorización |
| 4 | No hay auditoría de lectura ni cifrado de archivos o campos clínicos | Necesidades específicas de Spa sin mecanismo |
| 5 | El almacén de archivos permite borrado físico | Contradice la regla de no eliminar si se usa con fotografías |
| 6 | El rol Admin tiene acceso total por su función | Acceso a datos clínicos por defecto si no se decide lo contrario |
| 7 | Normativa de protección de datos personales y validez legal de la firma no verificadas | Retención, exportación, publicación y firma sin respaldo normativo |
| 8 | Consentimiento de menores y representantes no recuperado | Casos no cubiertos |
| 9 | Empleados no implementado | La identidad del profesional depende de `IEmployeeDirectory`, que es propuesta |
| 10 | Número de ficha, código del caso, obligatoriedad real y escalas numéricas sin definir | Contrato incompleto |

## 11. Verificación

Coherencia con ADR-050 y la especificación de Empleados (identidad del profesional, `employees.view`, asignaciones); ninguna regla se atribuye al propietario salvo
el título de SPA-DEC-116; sin duplicación de entidades oficiales; trazabilidad en §9. Este documento no autoriza implementación de Spa ni de Empleados,
migraciones, cambios en módulos FROZEN ni su indexación.
