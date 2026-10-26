# Communications — SSOT de entrega

Contrato implementado de Communications. Rationale: [ADR-039](../decisions/ADR-039-communications-architecture.md).
Orquestación, lotes, fairness y recuperación únicamente en
[Backend](../architecture/backend.md#background-processing); gates/cierres en [STATUS](../../STATUS.md).

## Alcance y fuentes de verdad

Communications es capacidad transversal; Email es un canal. CommunicationOutbox es
la autoridad durable de entrega y no se fusiona con OutboxMessage (eventos de dominio).
Un registro representa canal y rol de destinatario. La identidad incluye propósito,
origen y scope; nunca depende de un email mutable. CommunicationIdentity y el índice
único con NULLS NOT DISTINCT impiden duplicados también para System.

Company exige TenantId/CompanyId válidos; System usa IDs null y un filtro centralizado,
nunca Guid.Empty como centinela. Las lecturas operativas y diagnósticas respetan su alcance.
La intención se encola en la transacción ambiente o en una propia corta cuando no existe;
la colisión de identidad devuelve la fila existente sin abortar el hecho de negocio.

## Contrato durable e idempotencia

CommunicationOutbox conserva ScopeKind, TenantId/CompanyId/BranchId, Channel/Purpose,
SourceModule/SourceType/SourceId, RecipientRole, destinatario y contenido renderizado;
Status/Priority/ScheduledAtUtc/NextAttemptAtUtc; RetryCount/MaxRetries y FailureCategory;
ClaimToken/LeaseUntilUtc; TemplateKey/Version/Source y relación de reenvío.
Campos de origen/template pueden ser null en registros que no se atribuyeron con certeza.
No se inventan contratos AttemptCount/MaxAttempts/ExpiresAtUtc como si sustituyeran
los campos persistidos. Las constantes y constraints del dominio validan tamaños y estados.

El reenvío permitido crea otra intención con ResendOfCommunicationId/ResendSequence;
no borra intentos ni cambia la identidad del hecho original. Los propósitos definen
scope, canales, sensibilidad y posibilidad de reenvío.

## Concurrencia e intentos

Claim atómico PostgreSQL, lease y fencing por token protegen la posesión de la fila.
Un worker cuyo claim expiró no puede finalizar el trabajo de su sucesor.
CommunicationDeliveryAttempt registra cada intento, su resultado y el abandono del
claim anterior. Finalizar comunicación e intento ocurre en transacción corta.
El envío SMTP no ocurre con una transacción de negocio abierta.
No se promete exactamente una entrega externa ante caídas después del envío.
Errores seguros conservan tipo/código, nunca mensajes SMTP que expongan destinatarios.
ProviderMessageId solo se registra si lo devuelve el transporte; no se inventa.

## Reintento

CommunicationRetryPolicy es la autoridad única y recibe el instante UTC del caller.
RetryCount cuenta fallos; MaxRetries es el presupuesto total copiado al encolar desde
el perfil resuelto. Permanent y Configuration terminan Failed al primer fallo.
Transient/Unknown vuelven a Pending mientras quede presupuesto, con backoff 2^n minutos,
tope 60 y sin jitter. Un intento abandonado ya contabilizado no se cuenta dos veces.
No hay otra política de reintento de configuración cada quince minutos.

## Templates

Templates por propósito, embebidos y versionados en repositorio; override Company cuando
está permitido. CommunicationTemplateRenderer es el único renderer de variables, con
escape/validación; no se introduce RazorLight ni ejecución de código de plantilla.
Subject/BodyHtml/BodyText se renderizan al encolar y la fila conserva lo que se enviará;
no se vuelve a renderizar en cada envío. Si falla, se conserva Failed con código tipado
para template ausente/inválido/render fallido. Payload para reconstrucción solo cuando
falló el render y nunca contenido sensible en claro.

## Productores y adjuntos

El handler genérico de autorización electrónica obtiene datos desde contributors por
tipo: Factura, Nota de crédito y Retención no duplican la mecánica de entrega.
Los módulos son dueños de sus datos y del destinatario; Communications es dueño de entrega.
Adjuntos se resuelven por referencia durable; RIDE usa su entrada background con scope
verificado. Se conservan PendingSource y omisión opcional; no se generan PDFs bajo la
transacción fiscal. Reconciliación y retry no vuelven a interpretar preferencias ya
filtradas por el candidato SQL del job.

## Perfiles y seguridad

ResolveEmailAsync recibe scope explícito. System usa configuración de instancia;
Company usa el perfil de su empresa y el comportamiento de resolución de instancia
ya implementado por CommunicationSettingsResolver. No usa la primera membresía del usuario.
Las credenciales se tratan mediante ISecretProtector; no se imprimen ni se copian a docs.
Auth conserva emisión, expiración, invalidación y uso único de sus tokens.
PASSWORD_RESET figura como propósito sensible reservado: su presencia no acredita un
productor ni plantilla disponible; no persistir ni registrar tokens en claro.
No introducir un remitente alternativo, una cola sensible o un nuevo transporte desde docs.

## Límites

No se expone un monitor futuro ni otros canales como capacidades entregadas.
Diseños pendientes de vencimiento, payload sensible y UI requieren fase autorizada;
no se crean columnas, contratos ni procesos por esta consolidación.
Planificación: [ROADMAP](../ROADMAP.md). Instalación: [GLOBAL](../architecture/architecture.md#global-immutable-schema-global).
