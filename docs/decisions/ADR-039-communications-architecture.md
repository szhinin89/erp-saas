# ADR-039 — Communications como capacidad transversal

Estado: Accepted. Decisión arquitectónica vigente.

## Decisión y justificación

Evolucionar Communications, con Email como canal y CommunicationOutbox como autoridad
de entrega; no crear otro Mail Core ni fusionarlo con OutboxMessage, que transporta
eventos de dominio. La intención durable se registra con el hecho de negocio; composición
y transporte quedan fuera de esa transacción. Identidad central, claim/lease y fencing
protegen reintentos y varios nodos. Contributors aportan datos; no copian el handler fiscal.

System representa la instalación y Company la empresa. El scope System usa IDs nullable,
no Guid.Empty como centinela: evita excepciones en guardas tenant/company y ambigüedad
con otros usos del Guid vacío. Auth sigue siendo dueño de tokens y la entrega no los registra.
Templates versionados en repositorio y overrides Company evitan un CMS obligatorio.

## Responsabilidad y límites

El SMTP de seguridad pertenece a la instalación, no a la primera empresa del usuario.
La decisión de orquestación, lotes y cursores pertenece exclusivamente a ADR-043;
no se conserva DisableConcurrentExecution como garantía ni cursor singleton.
No añade canales ni UI futura por esta decisión.

## SSOT y dependencias

Contrato de Communications: [COMMUNICATIONS-ARCHITECTURE](../communications/COMMUNICATIONS-ARCHITECTURE.md).
Orquestación: [Backend](../architecture/backend.md#background-processing).
Auditoría: [audit-infrastructure](../architecture/audit-infrastructure.md).
Seguridad: [security](../architecture/security.md). Gates: [STATUS](../../STATUS.md).
