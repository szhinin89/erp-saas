# Gali Spa V1.1 — Seguridad, privacidad y cumplimiento: propuestas técnicas (borrador)

> **REFERENCIA HISTÓRICA NO NORMATIVA — actualizada el 2026-11-18.** SPA-DEC-115/116 se recuperaron aprobadas por evidencia aportada por el propietario: [registro §9/10](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md). El [contrato central](GALI-SPA-V1.1-CONTRACT-SPEC-01.md) contiene el alcance funcional aprobado y O-23 cerrada en inclusión general; los detalles técnicos, legales y las fichas originales siguen pendientes según su registro. [ADR-051](ADR-051-gali-spa-v1.1-architecture.md) PROPOSED / NO APROBADO y [plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md) sin fases autorizadas. Se preserva el cuerpo previo como evidencia de su fecha: sus afirmaciones «solo título», alcance sin confirmar, alternativas y cierres pendientes describen la recuperación anterior y no el estado actual. No sustituye contrato/registro ni autoriza implementar.

**BORRADOR DOCUMENTAL — PROPUESTAS NO APROBADAS — NO IMPLEMENTACIÓN AUTORIZADA.**

Documento de investigación y propuesta (SPA-V1.1-DOC-04). Desarrolla las alternativas para los riesgos de seguridad y privacidad identificados en
[GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md) («CARE-PRIVACY»), sin repetir su auditoría, y complementa
[RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) y [OPERATIONS-SPEC](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md). **Ninguna propuesta de este documento
está aprobada**: cada una es una alternativa con una recomendación técnica razonada que el propietario puede aceptar, modificar o rechazar. Estado de
entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

## 0. Cómo leer este documento

### 0.1 Etiquetas

Se conservan las de CARE-PRIVACY: **[A]**/**[A, derivado]** (ADR-050 o especificación de Empleados), **[E]** existente en el ERP, **[F]** fuente externa pendiente de
contraste, **[R]**, **[T]** propuesta técnica, **[P]** pendiente y **[NV]** no verificado. Para el contenido legal se usan además:

| Etiqueta legal | Significado |
|---|---|
| **[L-EXPRESO]** | Disposición cuyo texto se leyó en una copia publicada en un sitio oficial del Estado ecuatoriano; se cita artículo, fuente y fecha de consulta |
| **[L-INTERPRETACIÓN]** | Lectura del autor sobre cómo una disposición aplica a Spa; **no es asesoría jurídica** y requiere validación de un profesional legal |
| **[L-RECOMENDACIÓN]** | Medida técnica o de proceso sugerida para cumplir una disposición; no es un requisito legal |
| **[L-NV]** | No verificado: no se consultó fuente oficial o no se pudo confirmar vigencia |

### 0.2 Reglas de lectura

1. Una alternativa no es una decisión. Cada riesgo termina en una **recomendación técnica** razonada, no aprobada.
2. No se inventan requisitos legales ni plazos: lo no verificado figura como [L-NV].
3. Se separan los requisitos de seguridad (lo que el sistema debe lograr) de las políticas de acceso todavía no aprobadas (quién debe poder qué).
4. No se modifica código, almacenamiento, ADR-050 ni módulos FROZEN; las alternativas que los tocarían lo indican como dependencia que requiere autorización.
5. Los identificadores `SPA-SEC-*` son nuevos y no reemplazan ninguno de los documentos anteriores.

## 1. Evidencia de los mecanismos existentes [E]

Verificada en el código y la documentación del repositorio; cada fila indica cómo.

| Área | Hallazgo | Evidencia |
|---|---|---|
| Acceso del administrador | `OperationalCompanyContext.HasAdminBypass` es verdadero para el operador global o para quien tiene membresía activa con `Role = Admin` en esa empresa. `RuntimePermissionAuthorizer.IsAuthorizedAsync` devuelve **verdadero para el Admin antes de consultar la clave de permiso** | `ICompanyContextProvider.cs`; `RuntimePermissionAuthorizer.cs` (rama `if (context.HasAdminBypass) return true;`) |
| Alcance del bypass | Solo el rol de la membresía de la empresa operativa cuenta; ser Admin en una empresa no concede acceso en otra; el rol del JWT no autoriza por sí solo | [security.md](../architecture/security.md) (SEC-04) |
| Consumidores del bypass | `GetMyPermissions` (devuelve acceso total), contexto de sesión, navegación (el Admin ve todo sin filtro) y la regla anti-escalamiento de perfiles y membresías | `GetMyPermissionsHandler.cs`; `UpsertProfilePermissionsHandler.cs`; `UpsertCompanyUserMembershipHandler.cs`; security.md «Security & Access Contract V1 — LOCKED» |
| Contrato congelado | «Security & Access Contract V1 — LOCKED»: crear una segunda fuente de verdad para «es Admin» o bypasses fuera de `SecurityRoles` está **restringido y requiere revisión arquitectónica explícita**; agregar claves de permiso al catálogo del Kernel está permitido | security.md, sección LOCKED |
| Perfiles | Los perfiles (`ProfileId`) otorgan claves de permiso por empresa; un usuario no Admin sin perfil no tiene permisos | `RuntimePermissionAuthorizer.cs`; security.md |
| Acceso por sucursal | `CompanyUserBranch` y `IBranchAccessGuard` autorizan a un usuario por sucursal y distinguen «inexistente» (404) de «deshabilitada o sin acceso» (403) | `IBranchAccessGuard.cs`; [error-handling.md](../architecture/error-handling.md) |
| Auditoría de escritura | Entity Audit append-only: `AuditRecordBase` (`Id`, `TenantId`, `EntityId`, `Action`, `UserId`, `UserName`, `OccurredAtUtc`, `CorrelationId`, `RequestId`, `Source`, `Reason`), `AuditActor` como snapshot inmutable del actor, `IAuditService.RecordAsync`. La infraestructura es FROZEN y cada dominio añade su entidad | [audit-infrastructure.md](../architecture/audit-infrastructure.md) §2, §3 y §8 |
| Lectura de auditoría | `IAuditReader<T>.GetByEntityAsync(tenantId, entityId, take)` consulta solo por entidad; no filtra por usuario ni por rango de fechas | `IAuditReader.cs` |
| Lecturas exitosas | **No se encontró** ningún registro de accesos de lectura exitosos. `ForbiddenAccessLoggingMiddleware` registra solo accesos denegados (401/403) con métricas, no en una tabla de negocio | `ForbiddenAccessLoggingMiddleware.cs` |
| Feed de actividad | `UserActivity` está reservada para el feed liviano de actividad y **no** debe usarse como auditoría de negocio | audit-infrastructure.md §2 |
| Almacenamiento | `IFileStorage` (`SaveAsync`, `GetAsync`, `DeleteAsync`) y `LocalFileStorage`: guarda en el sistema de archivos bajo `FileStorage:BasePath`; `SaveAsync` abre con `FileMode.Create` (**sobrescribe sin aviso**); `DeleteAsync` borra físicamente; `FullPath` es `Path.GetFullPath(Path.Combine(base, relativePath))` **sin comprobar que el resultado siga dentro de la base**; no cifra ni calcula hash | `IFileStorage.cs`; `LocalFileStorage.cs` |
| Volumen único | API y Worker montan el mismo volumen de archivos, que contiene certificados, XML, RIDE y el keyring de Data Protection | [docs/deployment/README.md](../deployment/README.md) |
| Cifrado existente | `ISecretProtector` (ASP.NET Data Protection) para secretos de configuración; **no** hay cifrado de archivos ni de campos de negocio | `DataProtectionSecretProtector.cs` |
| Respaldos | `scripts/backup-restore.ps1` genera `postgres.dump` y `filestorage.tar.gz` (incluye el keyring) con SHA-256 solo como integridad; los respaldos **no están cifrados** y se guardan en `backups/` (ignorada por Git); excluye `.env.app`; permite probar la restauración (`RestoreCheck`) | [BACKUP_RESTORE.md](../operations/BACKUP_RESTORE.md) |
| TLS | El runbook indica que para producción falta dominio registrado y terminación SSL | docs/deployment/README.md |
| Exportación | Existen reportes y exportaciones en otros módulos con permisos propios; no hay un contrato de exportación clínica | CARE-PRIVACY §1 |
| Eliminación | Regla permanente del propietario: anular o deshabilitar, nunca borrar físicamente registros de negocio en ningún módulo | memoria del proyecto; CARE-PRIVACY §1 |

## 2. Matriz de riesgos

| ID | Riesgo | Gravedad candidata | Origen | Alternativas | Dependencia mayor |
|---|---|---|---|---|---|
| SPA-SEC-R-01 | El administrador de empresa accede a todos los datos clínicos por defecto | Alta | CARE-PRIVACY SEG-04 | §3 | Access / Kernel (si se excluye al Admin) |
| SPA-SEC-R-02 | No existe registro de lecturas exitosas de datos sensibles | Alta | CARE-PRIVACY §7.4 | §4 | Entidad nueva de Spa |
| SPA-SEC-R-03 | No hay cifrado de archivos ni de campos clínicos; el respaldo no está cifrado y contiene el keyring | Alta | CARE-PRIVACY §1 | §5 | Almacenamiento, claves y respaldos |
| SPA-SEC-R-04 | `LocalFileStorage` sobrescribe, borra físicamente y no valida la contención de la ruta | Media-Alta | CARE-PRIVACY FOT-07 | §5.2 | Adaptador de Spa o cambio de infraestructura compartida |
| SPA-SEC-R-05 | Conflicto entre la regla de no borrado físico y el derecho de eliminación | Alta | CARE-PRIVACY FOT-08 | §6 | Decisión del propietario |
| SPA-SEC-R-06 | Requisitos legales de consentimiento, firma, datos de salud y menores sin validar por un profesional | Alta | CARE-PRIVACY §10 | §7 | Asesoría legal |
| SPA-SEC-R-07 | Fecha de nacimiento y contacto de emergencia sin campo en BusinessPartner | Media | CARE-PRIVACY §10 | §8 | MasterData (FROZEN) |
| SPA-SEC-R-08 | Acceso entre sucursales sin política | Media | CARE-PRIVACY §7.3 | §3.4 | `CompanyUserBranch` |
| SPA-SEC-R-09 | Empleados no implementado: la identidad del profesional depende de un contrato propuesto | Media | ADR-050 | OPERATIONS-SPEC §2 | EMP-E1 a E6b |
| SPA-SEC-R-10 | Respaldos y réplicas conservan datos que un titular pidió eliminar | Media | derecho de eliminación | §6.4 | Operación |

## 3. Acceso a expedientes sensibles (SPA-SEC-R-01, R-08)

### 3.1 Comportamiento real

Un usuario con membresía `Admin` en una empresa pasa **cualquier** política `perm:*` en esa empresa, porque el autorizador resuelve el bypass antes de evaluar la
clave. Esto vale para cualquier permiso de Spa que se declare en el Kernel. Un perfil puede restringir a un usuario no Admin, pero no al Admin. El contrato que
lo gobierna está marcado LOCKED, y la creación de otra noción de «es Admin» o de una excepción al bypass es una acción «restringida» que requiere revisión explícita.

### 3.2 Requisito de seguridad frente a política no aprobada

| Concepto | Contenido | Estado |
|---|---|---|
| **Requisito de seguridad** [L-RECOMENDACIÓN] | Los datos de salud y las fotografías de un expediente solo deben ser accesibles a personas con una necesidad de atención demostrable, y todo acceso debe quedar registrado | [T] |
| **Política de acceso** | Si el Admin puede ver expedientes, bajo qué condiciones, y quién designa a los usuarios autorizados | **[P] no aprobada** |

### 3.3 Alternativas

| ID | Alternativa | Cómo funciona | Impacto | Consecuencias |
|---|---|---|---|---|
| A | Aceptar el bypass del Admin | Spa se comporta como el resto del ERP; el Admin ve todo | Ninguno | El control depende de la confianza en los administradores; el único mitigante es la auditoría de lecturas (§4) |
| B | Permisos «sellados» sin bypass | El Kernel marca ciertas claves de Spa como excluidas del bypass; `RuntimePermissionAuthorizer`, `GetMyPermissions`, la navegación, el contexto de sesión y la regla anti-escalamiento las tratan como permisos de perfil incluso para el Admin; el frontend deja de mostrar todo al Admin para esas claves | **Kernel** (metadatos de clave), **Access** (autorizador, anti-escalamiento, perfiles), sesión, navegación, frontend y pruebas | Cumple la exclusión estricta, pero **modifica el contrato LOCKED** y puede interpretarse como segunda fuente de verdad de «Admin»; exige revisión arquitectónica expresa y pruebas de regresión extensas; un Admin aún podría asignarse el perfil, salvo reglas adicionales de separación de funciones |
| C | Concesión de acceso clínico propia de Spa | Una entidad `CareAccessGrant` (usuario, alcance: empresa, sucursal o expediente, vigencia, motivo) que los handlers de Spa exigen **además** del permiso `perm:*`; el bypass del Admin pasa el permiso pero no la concesión | Solo Spa; sin cambios en Access ni Kernel (más allá de declarar las claves) | Reduce el acceso accidental o curioso del Admin, pero **no impide** que un Admin se otorgue una concesión salvo que la gestión de concesiones requiera una segunda persona o un permiso fuera del bypass (no existe); la auditoría pasa a ser el control principal |
| D | Acceso excepcional con motivo («break-glass») | Variante de C: el Admin sin concesión puede acceder con un motivo obligatorio, un registro reforzado y notificación a un responsable designado | Solo Spa | Acepta que el Admin puede acceder, pero lo hace visible y justificado |

### 3.4 Acceso entre sucursales (SPA-SEC-R-08)

Alternativa 1: el expediente es de empresa y lo ve cualquier usuario con el permiso (y la concesión de C, si se adopta). Alternativa 2: se limita a las sucursales
donde el usuario tiene `CompanyUserBranch`, con el patrón de `IBranchAccessGuard`. Alternativa 3: la sucursal de cada atención limita la visibilidad de esa atención y no del
expediente completo. Una consulta de la historia completa de un cliente atendido en varias sucursales exige que la política lo permita expresamente [P].

### 3.5 Recomendación técnica (no aprobada)

Adoptar **C + D** como primera etapa, junto con la auditoría de lecturas (§4): no toca el contrato LOCKED, es implementable en Spa y hace visible el uso del
bypass. Documentar **B** como cambio arquitectónico futuro si el propietario exige la exclusión estricta del Admin, porque requiere una decisión explícita sobre un
contrato congelado. En sucursales, la alternativa 2 (limitar por `CompanyUserBranch`) es la que reutiliza un mecanismo existente. Todo esto está sujeto a la decisión del
propietario sobre quién debe poder ver qué.

### 3.6 Impactos por módulo

| Módulo | A | B | C / D |
|---|---|---|---|
| Identity | — | — | — |
| Access | — | Cambios en el autorizador, perfiles y anti-escalamiento | — |
| Kernel | Claves nuevas | Claves nuevas + metadato «sellado» | Claves nuevas |
| Sesión y navegación | — | Cambios | — |
| Frontend compartido | — | `usePermissionsUi` y guardas | — |
| Spa | Auditoría | Auditoría | Entidad de concesión, política y auditoría |
| Contrato LOCKED | Sin cambio | **Requiere revisión explícita** | Sin cambio |

## 4. Auditoría de consultas (SPA-SEC-R-02)

### 4.1 Requisito [T]

Registrar quién accedió a información sensible de Spa, de qué recurso, con qué operación, cuándo y con qué resultado, **sin almacenar el contenido consultado**.

### 4.2 Qué puede reutilizarse y qué no

| Elemento | Reutilizable | Observación |
|---|---|---|
| `AuditRecordBase` y su configuración (`ConfigureAuditBase`) | Sí | Aporta tenant, entidad, acción, usuario, nombre, instante, correlación, solicitud, origen y motivo |
| `AuditActor` (snapshot inmutable) | Sí | Es la forma oficial de guardar al actor |
| `IAuditService.RecordAsync` | Parcialmente | Está pensado para el flujo de eventos de dominio dentro de una transacción de escritura; una lectura no ejecuta `SaveChanges` |
| `IAuditReader<T>` | No suficiente | Consulta solo por entidad; la consulta por usuario o fechas es propia de Spa |
| `ForbiddenAccessLoggingMiddleware` | Para denegaciones | Registra 401/403 con métricas; no persiste en una tabla de negocio |
| `UserActivity` | **No** | Reservada para el feed de actividad |

### 4.3 Diseño propuesto [T]

Entidad `CareAccessLog` de Spa, append-only, heredando de `AuditRecordBase` y con su propia configuración y migración (sin modificar la infraestructura FROZEN):

| Campo | Contenido |
|---|---|
| Heredados | `TenantId`, `EntityId` (= identificador del recurso), `Action` (= operación), `UserId`, `UserName`, `OccurredAtUtc`, `CorrelationId`, `RequestId`, `Source`, `Reason` |
| Propios | `CompanyId`; `ResourceType` (expediente, valoración, antecedentes, fotografía, consentimiento, exportación); `BranchId` (sucursal de la atención, si aplica); `Operation` (`View`, `List`, `Download`, `Export`, `Print`, `Denied`); `Result` (`Allowed`, `Denied`, `Failed`); `Purpose` (finalidad declarada, p. ej. seguimiento, acceso excepcional); `ExpedienteId` |
| **No se guarda** | El contenido consultado, los textos clínicos, el nombre del cliente ni el nombre de archivo de la fotografía; solo identificadores |

### 4.4 Alternativas de escritura

| Alternativa | Descripción | Consecuencias |
|---|---|---|
| A. Síncrona y fail-closed | El registro se escribe antes de devolver el dato; si falla, la lectura se deniega | Garantiza que no hay lectura sin rastro; añade una escritura a cada consulta sensible y una dependencia de disponibilidad |
| B. Asíncrona con cola | El evento se encola (patrón Outbox, [ADR-008](ADR-008-outbox-pattern-foundation.md)) y se persiste después | Menor latencia; riesgo de perder el registro si el proceso falla antes de encolar |
| C. Mixta | A para fotografías, exportaciones y apertura del expediente completo; B para listados | Equilibrio; dos caminos que mantener |

### 4.5 Reglas candidatas

| ID | Regla | Estado |
|---|---|---|
| SPA-SEC-AUD-01 | Toda consulta del detalle de un expediente, valoración, antecedentes, consentimiento o fotografía, toda descarga y toda exportación se registran | [T] |
| SPA-SEC-AUD-02 | Un listado registra una entrada por solicitud con el filtro resumido, no una por fila | [T] |
| SPA-SEC-AUD-03 | Los accesos denegados a recursos de Spa se registran con el resultado `Denied` | [T] |
| SPA-SEC-AUD-04 | El registro de accesos es append-only; nadie lo edita ni lo elimina; su consulta exige un permiso propio y se registra a su vez | [T] |
| SPA-SEC-AUD-05 | El acceso excepcional (alternativa D de §3.3) exige un motivo y se marca para revisión | [T] |
| SPA-SEC-AUD-06 | El registro no contiene contenido clínico ni nombres de archivo | [T] |
| SPA-SEC-AUD-07 | Plazo de conservación del registro de accesos: no recuperado | [P] |

### 4.6 Recomendación técnica

Alternativa **C** (mixta), con una entidad propia de Spa y sin extender la infraestructura FROZEN. Las extensiones necesarias son **nuevas** (entidad, configuración,
migración, consulta por usuario y fecha), no cambios al código compartido. La retención se decide con el propietario y la asesoría legal.

## 5. Cifrado, fotografías y respaldos (SPA-SEC-R-03, R-04)

### 5.1 Alcance

Antecedentes de salud, valoraciones, fotografías, consentimientos y evidencia de firma, archivos adjuntos y respaldos.

### 5.2 Limitaciones reales de `IFileStorage` [E]

| Limitación | Efecto en Spa | Mitigación propuesta |
|---|---|---|
| Sobrescribe sin aviso (`FileMode.Create`) | Una colisión de nombre destruiría una fotografía | Rutas generadas por el servidor a partir de identificadores únicos (nunca del usuario) |
| Sin validación de contención de ruta | Una ruta influida por el usuario podría salir del directorio base | Rutas solo generadas por el servidor; validación de contención en el adaptador de Spa |
| `DeleteAsync` borra físicamente | Contradice la regla de no borrado | El adaptador de Spa no expone el borrado; la eliminación legal sigue §6 |
| Sin cifrado ni hash | Los archivos quedan en claro en disco y en los respaldos | §5.3 |
| Sin control de acceso propio | Cualquier código con la ruta puede leer | Acceso solo mediante un endpoint autorizado; ninguna URL pública |
| Volumen compartido con certificados y keyring | Un respaldo del volumen mezcla fotografías, certificados y claves | Almacén o prefijo propio para Spa y respaldos cifrados |

### 5.3 Alternativas de protección

| Capa | Alternativa A | Alternativa B | Alternativa C |
|---|---|---|---|
| Transporte | TLS en la terminación del despliegue (hoy pendiente en el runbook) | — | — |
| Archivos en reposo | Cifrado del disco o volumen en la infraestructura (fuera del ERP; no verificado) | Cifrado de aplicación por archivo con **clave de datos por archivo** protegida por una clave maestra (cifrado de sobre) | Almacén de objetos con cifrado del lado del servidor (futuro) |
| Campos clínicos en base de datos | Sin cifrado de campo; control por permisos y auditoría | Cifrado de columnas con conversor de EF (pierde búsqueda y ordenamiento sobre esos campos) | Cifrado a nivel de disco del servidor de base de datos (infraestructura) |
| Claves | Data Protection de ASP.NET con su keyring en el mismo volumen | Clave maestra en un gestor de secretos o KMS externo | Clave por expediente (permite borrado criptográfico, §6) |
| Respaldos | Cifrado del archivo de respaldo con una clave guardada aparte | Respaldo sin cifrar (hoy) | — |

### 5.4 Análisis

- **Keyring junto a los archivos:** el keyring de Data Protection está en el mismo volumen que los archivos y entra en el mismo `filestorage.tar.gz`. Si Spa cifrara con Data Protection, un respaldo
  contendría datos y claves juntos y el cifrado no protegería el respaldo. La clave maestra debe vivir **separada** de los datos y de sus respaldos.
- **Pérdida de claves = pérdida de datos:** exige un procedimiento de custodia, rotación e identificador de versión de clave guardado con cada objeto cifrado.
- **Cifrado de campos** impide buscar por esos campos; los datos buscables (nombre, identificación) viven en BusinessPartner y no se cifran en Spa.
- **Referencias privadas:** la base guarda un identificador y no una URL; la lectura pasa por la API, que autoriza, registra el acceso (§4) y transmite el contenido; no se sirve el directorio de archivos como contenido estático [NV: no se verificó
  si el directorio base se publica].
- **Integridad:** guardar un hash SHA-256 del contenido permite detectar alteraciones.

### 5.5 Recomendación técnica (no aprobada)

1. **Obligatorio antes de datos reales** [L-RECOMENDACIÓN]: referencias privadas, lectura autorizada y auditada, rutas generadas por el servidor, hash de contenido, ausencia de borrado físico y **respaldos cifrados con la clave separada**.
2. **Muy recomendable antes de datos reales de salud:** cifrado de aplicación por archivo para fotografías y evidencia de firma con **cifrado de sobre** y clave maestra externa al volumen.
3. **Segunda etapa:** cifrado de columnas de antecedentes si el análisis de riesgo lo exige, aceptando la pérdida de búsqueda.
4. **Verificar con infraestructura:** cifrado de disco y publicación del directorio de archivos [NV].
5. **No modificar** `IFileStorage` ni `LocalFileStorage`: Spa usaría un adaptador propio (`ICareFileStore`) sobre `IFileStorage` o un almacén nuevo; cambiar la infraestructura compartida exigiría su propia autorización.

## 6. Conservación, corrección y eliminación (SPA-SEC-R-05, R-10)

### 6.1 Conceptos que deben distinguirse

| Concepto | Qué es | Efecto |
|---|---|---|
| Corrección | Rectificar información inexacta | Versión nueva append-only con autor, fecha y motivo (CARE-PRIVACY EXP-06) |
| Retiro de consentimiento | La persona revoca una autorización | Cesa el tratamiento futuro; el anterior sigue siendo lícito |
| Restricción de acceso | Bloquear el uso sin eliminar | Datos visibles solo para una función autorizada |
| Conservación histórica | Mantener el registro por un plazo | Definida por finalidad y por norma aplicable |
| Eliminación | Borrado seguro o anonimización | Solo por una causa prevista y con un procedimiento controlado |
| Borrado arbitrario | Eliminar sin causa ni control | **Prohibido**: regla permanente del propietario |

### 6.2 Tensión entre «append-only» y los derechos del titular

Una regla absoluta de no borrado impediría cumplir una solicitud de eliminación legítima. Por eso este documento **no declara el append-only como prohibición absoluta de ejercer derechos
legales**: propone un **procedimiento de excepción controlado** (§6.3) que el propietario debe aprobar, porque contradice la letra de la regla permanente. Qué causales y plazos aplican se
resuelve con asesoría legal ([L-NV], §7).

### 6.3 Alternativas para la eliminación o anonimización

| Alternativa | Descripción | Consecuencias |
|---|---|---|
| A. Borrado criptográfico | Cada expediente (o archivo) se cifra con una clave propia; eliminar la clave vuelve ilegible el contenido, también en los respaldos | Requiere cifrado por registro (§5.3 B/C) y gestión de claves; no deja datos recuperables |
| B. Anonimización en el lugar | Se sustituyen los campos personales por valores neutros conservando la estructura (identificadores, fechas, existencia) | Mantiene la integridad referencial; la anonimización debe ser irreversible |
| C. Borrado físico con lápida | Se elimina el contenido y queda un registro mínimo del hecho (sin datos personales) | Cumple el borrado pero contradice la regla y rompe referencias si no se diseña |
| D. No eliminar nunca | Solo inactivar | Incompatible con una obligación legal de eliminación si aplica |

### 6.4 Reglas candidatas

| ID | Regla | Estado |
|---|---|---|
| SPA-SEC-RET-01 | Ningún borrado físico fuera de un procedimiento de eliminación legal aprobado | [T] |
| SPA-SEC-RET-02 | El procedimiento de eliminación exige un permiso propio, una solicitud registrada con fecha y titular, una aprobación y deja un registro del hecho sin contenido personal | [T] |
| SPA-SEC-RET-03 | Los antecedentes corregidos conservan sus versiones anteriores con acceso restringido, sin mostrarse como vigentes | [T]; si una rectificación obliga a ocultar la versión inexacta es [L-NV] |
| SPA-SEC-RET-04 | Un retiro de consentimiento suspende el uso futuro; las versiones y los registros de atención ya realizados siguen su plazo de conservación | [T] |
| SPA-SEC-RET-05 | Los respaldos con datos eliminados se tratan con un ciclo de rotación documentado; con borrado criptográfico (A) la clave eliminada cubre también los respaldos | [T] |
| SPA-SEC-RET-06 | Plazos de conservación por tipo de dato: no recuperados | [P]; [L-NV] |

### 6.5 Recomendación técnica

**A + B**: cifrado por expediente con borrado criptográfico para archivos y fotografías, y anonimización en el lugar para los datos en base, ambas bajo un procedimiento aprobado de eliminación legal. No
se recomienda C. Requiere aprobar una excepción a la regla de no borrado y definir las causales con asesoría legal.

## 7. Requisitos legales de Ecuador

### 7.1 Fuentes y fecha de consulta

**Alcance de esta sección:** reúne las disposiciones consultadas, las interpretaciones del autor y los puntos no verificados. **No constituye una certificación de cumplimiento ni una
revisión jurídica definitiva**, y no sustituye la validación de un profesional legal.

Fecha de consulta: **18 de noviembre de 2026**. Los textos se leyeron extrayendo el texto de PDF publicados en el portal oficial del Estado (gob.ec) y de un sitio gubernamental; la extracción contiene algunos errores de reconocimiento de caracteres, por lo que
**cada cita debe confirmarse contra el Registro Oficial antes de usarse formalmente**.

| Norma | Datos de publicación | Fuente consultada | Estado indicado en la fuente |
|---|---|---|---|
| Ley Orgánica de Protección de Datos Personales (LOPDP) | Quinto Suplemento del Registro Oficial 459, 26 de mayo de 2021 | [gob.ec (PDF)](https://www.gob.ec/sites/default/files/regulations/2025-01/01%20Ley%20Org%C3%A1nica%20de%20Protecci%C3%B3n%20de%20Datos%20Personales.pdf) y copia del [Consejo de Comunicación](https://consejodecomunicacion.gob.ec/wp-content/uploads/downloads/2021/07/lotaip/Ley%20Org%C3%A1nica%20de%20Protecci%C3%B3n%20de%20Datos%20Personales.pdf) | Vigente |
| Reglamento General a la LOPDP | Decreto Ejecutivo 904, Suplemento del Registro Oficial 435, 13 de noviembre de 2023 | [gob.ec (PDF)](https://www.gob.ec/sites/default/files/regulations/2025-01/02%20Reglamento%20General%20a%20la%20Ley%20Org%C3%A1nica%20de%20Protecci%C3%B3n%20de%20Datos%20Personales_0.pdf) | Vigente; «Fecha de última modificación: No aplica» |
| Ley de Comercio Electrónico, Firmas Electrónicas y Mensajes de Datos | Ley 67, Suplemento del Registro Oficial 557, 17 de abril de 2002 | [gob.ec (PDF, 2025-11)](https://www.gob.ec/sites/default/files/regulations/2025-11/Ley%20de%20Comercio%20Electr%C3%B3nico,%20Firmas%20y%20Mensaje%20de%20Datos.pdf) | Reformado; última reforma indicada: 7 de febrero de 2023 |

**Vigencia de los textos [L-NV]:** las copias de la LOPDP y del Reglamento en gob.ec se publicaron en enero de 2025, y esta consulta no verificó reformas posteriores. Un resultado de búsqueda aislado mencionó una posible reforma de 2026 al artículo 43 de la LOPDP;
**no se confirmó en ninguna fuente oficial** y no se toma como hecho.

### 7.2 Obligaciones expresas verificadas [L-EXPRESO]

| ID | Disposición | Contenido (resumido; verificar el texto) | Aplicación a Spa |
|---|---|---|---|
| L-01 | LOPDP, Art. 4 | Define **datos relativos a la salud** (datos sobre la salud física o mental, incluida la prestación de servicios de atención sanitaria, que revelen información sobre el estado de salud) y **datos sensibles** (incluye salud y datos biométricos) | Ver L-INT-01 |
| L-02 | LOPDP, Art. 8 | El consentimiento debe ser libre, específico, informado e inequívoco; revocable en cualquier momento sin justificación, con mecanismos ágiles, gratuitos y sencillos, similares al de la obtención; el tratamiento previo a la revocación es lícito; si hay varias finalidades, el consentimiento debe constar para todas | CARE-PRIVACY §5 |
| L-03 | Reglamento, Art. 5 | El responsable debe informar previa y detalladamente tipos de tratamiento, finalidades, tiempo de conservación, medidas de protección y consecuencias; el consentimiento debe ser demostrable cuando la autoridad lo requiera; el silencio o la inacción no lo presumen; el de niñas, niños y adolescentes se obtiene por sus representantes legales | Evidencia del consentimiento (§7.4) |
| L-04 | Reglamento, Art. 6 | Tras la revocatoria, el responsable debe suspender el tratamiento | SPA-SEC-RET-04 |
| L-05 | LOPDP, Art. 12 | Derecho a la información: fines, base legal, tipos de tratamiento, **tiempo de conservación**, existencia de la base de datos, transferencias, posibilidad de revocar, forma de ejercer los derechos, dónde reclamar | Aviso de privacidad |
| L-06 | LOPDP, Art. 14 | Rectificación y actualización de datos inexactos o incompletos; atención en un plazo de **quince (15) días** | SPA-SEC-RET-03 |
| L-07 | LOPDP, Art. 15 | Derecho de eliminación en los supuestos que enumera (incluye la revocación del consentimiento y el vencimiento del plazo de conservación); el responsable aplicará métodos para eliminar, hacer ilegible o dejar irreconocibles los datos de forma definitiva y segura, en **quince (15) días**, gratis | §6 |
| L-08 | LOPDP, Art. 18 | Excepciones a rectificación, eliminación, oposición y portabilidad, entre ellas obligación legal o contractual, defensa de reclamos y otros casos que enumera | §6 |
| L-09 | LOPDP, Art. 10 (i) | Los datos se conservan por un tiempo no mayor al necesario; el responsable establecerá plazos para su supresión o revisión periódica | SPA-SEC-RET-06 |
| L-10 | Reglamento, Arts. 8 a 11 | Plazos de conservación acotados a la finalidad; cumplida esta, eliminación, bloqueo o anonimización; el fichero de registro debe contener el plazo de conservación; la eliminación no aplica, entre otros supuestos, por razones de interés público en el ámbito de la salud pública y privada o para formular, ejercer o defender reclamaciones | §6 |
| L-11 | LOPDP, Arts. 25 y 26 | Son categorías especiales los datos sensibles, de niñas, niños y adolescentes, de salud y de personas con discapacidad; el tratamiento de datos sensibles está prohibido salvo, entre otros casos, el **consentimiento explícito** con fines claramente especificados | Consentimientos |
| L-12 | LOPDP, Arts. 30 y 31 | Datos de salud: los profesionales de la salud y las instituciones del Sistema Nacional de Salud pueden tratarlos según la ley; deber de **confidencialidad** que subsiste al terminar la relación; el tratamiento de datos de salud requiere consentimiento previo salvo excepciones; **siempre que sea posible, se anonimizarán o seudonimizarán** | Ver L-INT-01 |
| L-13 | LOPDP, Art. 21; Reglamento, Arts. 19 y 20 | No se tratan datos sensibles ni de niñas, niños y adolescentes sin la autorización expresa del titular o de su representante legal; para menores de 15 años se requiere el consentimiento del representante legal; los adolescentes desde los 15 años pueden otorgar su consentimiento explícito si se les informa en lenguaje sencillo; el consentimiento no puede menoscabar el interés superior del menor | CNS-08 |
| L-14 | LOPDP, Art. 37 | Medidas de seguridad según categorías y volumen de datos y riesgos; entre otras, anonimización, seudonimización o **cifrado**, medidas de confidencialidad, integridad y disponibilidad, y de resiliencia; evaluación continua de su eficacia | §5 |
| L-15 | LOPDP, Art. 39; Reglamento, Arts. 59 y 60 | Protección de datos desde el diseño y por defecto: solo se tratan los datos necesarios para cada finalidad | §8 |
| L-16 | LOPDP, Art. 43; Reglamento, Arts. 24 a 28 | Notificación de vulneraciones de seguridad a la autoridad en un término de **cinco (5) días**; el encargado notifica al responsable en **dos (2) días** | Plan de incidentes |
| L-17 | LOPDP, Art. 47 | Obligaciones del responsable: políticas de protección, análisis y gestión de riesgos, medidas, notificación de violaciones, protección desde el diseño y por defecto, contratos de confidencialidad con el personal y encargados, registro nacional, delegado cuando corresponda y permitir auditorías | Gobierno del proyecto |
| L-18 | LOPDP, Art. 48; Reglamento, Arts. 53 y 54 | Se designa un delegado de protección de datos, entre otros casos, en el tratamiento **a gran escala de categorías especiales de datos**, según los factores del Reglamento | Ver L-INT-03 |
| L-19 | Reglamento, Art. 31 | La evaluación de impacto es obligatoria en los casos que la ley establece, entre ellos el tratamiento a gran escala de categorías especiales | Ver L-INT-03 |
| L-20 | LOPDP, Disposición Transitoria Primera | El régimen de medidas correctivas y sancionatorio entra en vigencia dos años después de la publicación (26 de mayo de 2021) | Cómputo: L-INT-04 |
| L-21 | Ley 67, Arts. 2, 13, 14, 15 | Los mensajes de datos tienen igual valor jurídico que los documentos escritos; la **firma electrónica** son datos electrónicos asociados a un mensaje que identifican al titular e indican que aprueba el contenido; tiene la misma validez que la manuscrita y se admite como prueba; para su validez debe ser individual y vinculada exclusivamente a su titular, permitir verificar inequívocamente la autoría e identidad, tener un método de creación y verificación confiable, seguro e inalterable, y estar bajo control exclusivo del signatario | CNS firma |
| L-22 | Ley 67, Art. 5 | Principios de confidencialidad y reserva de los mensajes de datos, incluido el secreto profesional | Evidencia electrónica |
| L-23 | Ley 67, Arts. 52 a 55 | Los mensajes de datos y firmas electrónicas se admiten como prueba; existe una presunción para la firma electrónica certificada (Art. 53, solo se leyó el título) | CNS firma |

### 7.3 Interpretaciones (no verificadas por un profesional legal)

| ID | Interpretación | Estado |
|---|---|---|
| L-INT-01 | Los antecedentes que declara la persona (enfermedades, medicamentos, alergias, embarazo, reacciones) **revelan información sobre su estado de salud** y probablemente son datos de salud y sensibles (L-01). Si Spa es un «profesional de la salud» o parte del Sistema Nacional de Salud (L-12) **no se verificó** | [L-INTERPRETACIÓN]; [L-NV] |
| L-INT-02 | Las fotografías del rostro o del cuerpo son datos personales; si constituyen **datos biométricos** depende de que se traten para identificar a la persona. El texto de la ley extraído no contiene las palabras «imagen» ni «fotografía». El derecho a la propia imagen en otras normas **no se consultó** | [L-INTERPRETACIÓN]; [L-NV] |
| L-INT-03 | Si el volumen de Spa constituye «gran escala» (delegado, evaluación de impacto, registro) depende de los factores del Reglamento y de la Autoridad; **no se evaluó** | [L-NV] |
| L-INT-04 | Con la publicación del 26 de mayo de 2021 y la transitoria primera, el régimen sancionatorio estaría vigente desde el 26 de mayo de 2023; es un cómputo del texto, no una confirmación oficial | [L-INTERPRETACIÓN] |
| L-INT-05 | Una firma manuscrita capturada en pantalla o una aceptación electrónica pueden o no cumplir los requisitos del Art. 15 (L-21) y su valor probatorio depende del caso; si el consentimiento de un tratamiento exige firma electrónica certificada **no se verificó** | [L-INTERPRETACIÓN]; [L-NV] |
| L-INT-06 | Un menor de 15 años exige consentimiento del representante legal (L-13); cómo se acredita la representación **no se verificó** | [L-NV] |
| L-INT-07 | Que Spa sea el «responsable» y el titular del ERP o un tercero el «encargado» depende de la relación contractual; **no se determinó** | [L-NV] |

### 7.4 Recomendaciones técnicas derivadas [L-RECOMENDACIÓN]

| ID | Recomendación | Relación |
|---|---|---|
| SPA-SEC-LEG-01 | Mostrar al titular antes de recabar el consentimiento los puntos del Art. 12 aplicables y registrar la versión mostrada | L-02, L-03, L-05 |
| SPA-SEC-LEG-02 | Conservar la evidencia del consentimiento de forma que pueda **demostrarse** (versión del texto, instante, quién consintió y, si aplica, su representante) | L-03 |
| SPA-SEC-LEG-03 | Registrar las solicitudes de ejercicio de derechos con fecha de recepción y plazo de 15 días | L-06, L-07 |
| SPA-SEC-LEG-04 | Mantener un plan de respuesta a incidentes con los términos de 5 y 2 días | L-16 |
| SPA-SEC-LEG-05 | Minimizar los datos: no almacenar más de lo necesario para cada finalidad | L-15 |
| SPA-SEC-LEG-06 | Anonimizar o seudonimizar cuando se usen datos de seguimiento para fines distintos de la atención | L-12 |
| SPA-SEC-LEG-07 | Obtener asesoría legal para L-INT-01 a L-INT-07 antes de aprobar retención, exportación, publicación, firma y atención a menores | §7.3 |

### 7.5 No consultado

Ley Orgánica de Salud (historia clínica y secreto profesional), normativa de la autoridad sanitaria sobre establecimientos de estética, Código de la Niñez y Adolescencia, normativa de protección al consumidor, el derecho constitucional a la propia imagen, resoluciones de la Superintendencia de
Protección de Datos Personales y plazos de conservación de historias clínicas: **[L-NV]**, sin ninguna afirmación.

## 8. Fecha de nacimiento y contacto de emergencia (SPA-SEC-R-07)

BusinessPartner no tiene estos campos [E, CARE-PRIVACY §1]. MasterData es FROZEN (ADR-017).

### 8.1 Alternativas

| ID | Alternativa | Ventajas | Riesgos y duplicación |
|---|---|---|---|
| A | Campos propios de Spa (`CareRecordPersonalData`), opcionales y de acceso restringido | No toca MasterData; datos mínimos y bajo permisos clínicos; se pueden cifrar (§5) | Datos personales fuera de MasterData; si BusinessPartner gana después estos campos habría dos fuentes: se mitiga declarando que Spa es la fuente de estos campos y nunca copia identidad de BP |
| B | Ampliar BusinessPartner con atributos opcionales de persona | Una sola fuente para todos los módulos | Modifica un módulo FROZEN y exige un ADR nuevo; la fecha de nacimiento queda visible para quien lea BP (más exposición) y para todos los módulos |
| C | Registrar el contacto de emergencia como contacto de BP (rol `Other`) | Reutiliza contactos existentes | Cualquiera con acceso al BP lo ve; el rol no distingue «emergencia»; no cubre la fecha de nacimiento; es dato de un tercero |
| D | No guardar la fecha de nacimiento; guardar solo la **categoría de edad** (adulto, adolescente de 15 a 17, menor de 15) verificada en la fecha del consentimiento | Minimización (L-15); cubre lo que el consentimiento necesita (L-13) | No permite recalcular la edad después; no sirve si el negocio necesita la edad exacta |
| E | Edad declarada como dato histórico en la ficha | Replica la ficha física | Envejece; no permite determinar umbrales de forma fiable |

### 8.2 Tratamiento de duplicación

- Spa guarda solo lo que BusinessPartner no tiene y **nunca** copia nombre, identificación ni contacto de BP.
- Si en el futuro MasterData adopta estos campos mediante un ADR propio, se define una migración y una regla de precedencia; hasta entonces la fuente es Spa.
- El contacto de emergencia es un dato de un tercero: mínimo necesario (nombre, vínculo, teléfono) y la información al tercero es un tema legal pendiente [L-NV].

### 8.3 Recomendación técnica (no aprobada)

Para el contacto de emergencia: **A** con campos mínimos. Para la edad: **D** si lo único que se necesita es determinar la validez del consentimiento (menor de 15 o no); **A con fecha de nacimiento cifrada** solo si el propietario confirma que se necesita la edad
exacta. **B** solo mediante un ADR de MasterData. Decisión del propietario.

## 9. Dependencias y posibles cambios de arquitectura

| Cambio | Módulos afectados | Autorización especial | Alternativa que lo origina |
|---|---|---|---|
| Permisos «sellados» sin bypass del Admin | Kernel, Access, sesión, navegación, frontend | **Sí**: revisión arquitectónica explícita del contrato LOCKED | §3.3 B |
| Entidad de concesión de acceso clínico | Spa | No (nueva en Spa) | §3.3 C, D |
| Entidad `CareAccessLog` | Spa | No (nueva en Spa); sin tocar la infraestructura FROZEN | §4 |
| Adaptador `ICareFileStore` sobre `IFileStorage` | Spa, Infrastructure | No si es nuevo; sí si cambia `LocalFileStorage` | §5 |
| Cifrado de aplicación y gestión de claves | Spa, Infrastructure, operación | Posible (claves externas, despliegue) | §5.3 |
| Respaldos cifrados | `scripts/backup-restore.ps1`, operación | **Sí**: cambiar scripts de operación | §5 |
| Procedimiento de eliminación legal | Spa, política | **Decisión del propietario**: excepción a la regla de no borrado | §6 |
| Campos propios de nacimiento y emergencia | Spa | No | §8 A |
| Ampliar BusinessPartner | MasterData | **Sí**: ADR nuevo, módulo FROZEN | §8 B |
| Empleados implementado | Empleados | Autorizaciones EMP-E1 a E6b | SPA-SEC-R-09 |

## 10. Matriz de permisos candidata

Ningún permiso existe. Amplía la matriz de CARE-PRIVACY §7.2 con los permisos que introducen las alternativas.

| Operación | Permiso candidato [T] | Condiciones adicionales | Registro de acceso |
|---|---|---|---|
| Consultar expediente | `spa.records.view` | Concesión de acceso (si se adopta C); alcance por sucursal [P] | Sí |
| Consultar antecedentes | `spa.history.view` | Separado de la consulta general por su sensibilidad | Sí |
| Modificar antecedentes | `spa.history.update` | Versión con motivo | Sí (escritura y lectura) |
| Consultar fotografías | `spa.photos.view` | Consentimiento vigente para la finalidad | Sí, síncrono |
| Subir fotografías | `spa.photos.create` | Consentimiento de seguimiento vigente | Escritura |
| Exportar | `spa.records.export` | Permiso separado; motivo obligatorio | Sí, síncrono |
| Gestionar consentimientos | `spa.consents.manage` | Versión vigente del texto | Escritura |
| Gestionar concesiones de acceso | `spa.access-grants.manage` | Un permiso distinto del de consulta; el bypass del Admin lo atraviesa (§3.1) | Sí |
| Acceso excepcional | `spa.records.emergency-access` | Motivo obligatorio; revisión posterior | Sí, reforzado |
| Consultar el registro de accesos | `spa.access-log.view` | Restringido; su consulta se registra | Sí |
| Solicitar o ejecutar eliminación legal | `spa.erasure.request`, `spa.erasure.execute` | Aprobación separada; solo tras decisión del propietario | Sí |
| Administrar claves | Fuera del permiso de usuario | Operación de infraestructura | Registro de operación |

Los permisos se declararían solo en el Kernel; ningún perfil existente los recibe. `employees.view` no los sustituye (SPA-SG-06).

## 11. Invariantes de seguridad candidatos

| ID | Invariante | Evidencia |
|---|---|---|
| SPA-SEC-INV-01 | Toda lectura exitosa de un recurso sensible deja un registro sin contenido clínico | §4; [T] |
| SPA-SEC-INV-02 | Ninguna fotografía se sirve sin autorización, sin consentimiento vigente para la finalidad y sin registro de acceso | CARE-PRIVACY FOT-03, FOT-06; [T] |
| SPA-SEC-INV-03 | Las rutas de archivos las genera el servidor; ninguna ruta proviene del usuario | §5.2; [T] |
| SPA-SEC-INV-04 | Los archivos clínicos no se borran físicamente salvo por el procedimiento de eliminación legal aprobado | §6; [T] |
| SPA-SEC-INV-05 | La clave maestra de cifrado no reside en el volumen de datos ni en sus respaldos | §5.4; [T] |
| SPA-SEC-INV-06 | El registro de accesos es append-only y no contiene contenido | §4; [T] |
| SPA-SEC-INV-07 | Ni `employees.*` ni `access.*` conceden acceso a datos clínicos | [A, derivado] (ADR-050 §6 y §13) |
| SPA-SEC-INV-08 | El profesional y el usuario registrador se guardan y se auditan por separado | [A] (ADR-050 §3 DEC-E4, §13) |
| SPA-SEC-INV-09 | El acceso excepcional exige motivo y queda marcado para revisión | §3.3 D; [T] |
| SPA-SEC-INV-10 | Una autorización retirada impide usos futuros sin alterar los hechos ya registrados | CARE-PRIVACY CNS-04; [T] |
| SPA-SEC-INV-11 | Los datos personales que no están en BusinessPartner viven en Spa y nunca duplican la identidad de BP | §8.2; [T] |
| SPA-SEC-INV-12 | Los datos de aislamiento por tenant y empresa no se omiten ni en lecturas, exportaciones ni registros de acceso | security.md; [E] |

## 12. Criterios de aceptación y pruebas futuras

| ID | Escenario | Relación |
|---|---|---|
| SPA-SEC-T-01 | Un Admin sin concesión no obtiene el detalle de un expediente (si se adopta C) y un acceso excepcional exige motivo y queda registrado | R-01; INV-09 |
| SPA-SEC-T-02 | Un usuario con perfil sin el permiso de Spa recibe 403 aunque tenga `employees.view` | INV-07 |
| SPA-SEC-T-03 | Cada consulta de detalle, descarga y exportación crea un registro de acceso con usuario, empresa, recurso, operación, instante y resultado, sin contenido | INV-01, INV-06 |
| SPA-SEC-T-04 | Un acceso denegado a un recurso de Spa queda registrado con el resultado `Denied` | AUD-03 |
| SPA-SEC-T-05 | Si el registro síncrono falla, la lectura de una fotografía se deniega | AUD alternativa A |
| SPA-SEC-T-06 | Una ruta con `..` o absoluta no puede resolverse fuera del directorio base | INV-03 |
| SPA-SEC-T-07 | Dos subidas simultáneas no sobrescriben un archivo existente | R-04 |
| SPA-SEC-T-08 | Un archivo cifrado no se lee sin su clave; el contenido en disco no es legible | R-03 |
| SPA-SEC-T-09 | La restauración de un respaldo cifrado recupera archivos y base y verifica sus hashes | R-03 |
| SPA-SEC-T-10 | La eliminación legal deja el registro del hecho sin datos personales y los archivos ilegibles | RET-02 |
| SPA-SEC-T-11 | Una corrección crea una versión nueva y la anterior queda con acceso restringido | RET-03 |
| SPA-SEC-T-12 | Un retiro de consentimiento bloquea el uso futuro y no altera atenciones previas | INV-10 |
| SPA-SEC-T-13 | La fecha de nacimiento y el contacto de emergencia no se copian a BusinessPartner | INV-11 |
| SPA-SEC-T-14 | El aislamiento por tenant y empresa se mantiene en lecturas, exportaciones y registros de acceso | INV-12 |
| SPA-SEC-T-15 | Un usuario limitado a una sucursal no accede a expedientes de otra (si se adopta la alternativa 2) | R-08 |

Las pruebas SPA-SEC-T-01, T-05, T-08 a T-10 dependen de decisiones **no aprobadas**. Las de concurrencia, aislamiento y cifrado requieren PostgreSQL real y almacenamiento aislado.

## 13. Decisiones del propietario necesarias (no son decisiones tomadas)

1. Si el Admin puede acceder a expedientes clínicos y con qué mecanismo (§3).
2. Alcance de acceso entre sucursales (§3.4).
3. Modo de registro de lecturas y su retención (§4).
4. Nivel de cifrado antes de datos reales y gestión de claves (§5).
5. Excepción a la regla de no borrado y procedimiento de eliminación (§6).
6. Tratamiento de fecha de nacimiento, edad y contacto de emergencia (§8).
7. Asesoría legal sobre L-INT-01 a L-INT-07 (§7.3).

## 14. Decisiones históricas que permanecen sin recuperar

- **SPA-DEC-116 — Revisión y consolidación del alcance de Gali Spa V1.1:** aprobada y confirmada por el propietario; solo el título está confirmado y su texto íntegro y alcance literal **no están recuperados**. Este documento no deduce su alcance.
- No se declaran existentes ni aprobadas las decisiones `SPA-DEC-001` a `SPA-DEC-115`.
- Ninguna decisión histórica sobre seguridad, privacidad, retención, firma o consentimiento de Spa fue recuperada; las propuestas de este documento **no sustituyen** esas decisiones si aparecen.
- Las fichas originales de Gali Spa siguen sin inspección directa por Claude.

## 15. Trazabilidad

| Sección de este borrador | CARE-PRIVACY | RECOVERY-01 |
|---|---|---|
| §3 | SEG-04, §7.3 | SPA-SG-01, SG-04 a SG-06 |
| §4 | FOT-06, §7.4 | SPA-AU-01, AU-02 |
| §5 | FOT-02, FOT-07, §6.4 | SPA-FO-01, SPA-INV-07 |
| §6 | FOT-08, CNS-04 | SPA-CN-05, SPA-INV-06 |
| §7 | §5, §10 | SPA-CN-01 a CN-06 |
| §8 | §2.4 | SPA-FC-02, FC-07 |
| §10 y §11 | §7.2 | SPA-SG-04 |

## 16. Verificación

No se modifica código, almacenamiento, ADR-050 ni módulos FROZEN. Ninguna propuesta se presenta como decisión aprobada; el contenido legal distingue lo expreso, la interpretación y la recomendación, y marca como no verificado
todo lo que no se pudo confirmar en una fuente oficial. Este documento no autoriza implementación de Spa ni de Empleados.
