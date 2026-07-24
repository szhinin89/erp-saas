export type ElectronicDocumentXmlVariant = 'Draft' | 'Signed' | 'Authorized';

/** Un evento del timeline — reconstruido desde auditoría real, nunca inventado. */
export type ElectronicDocumentTimelineEventDto = {
  action: string;
  fromState: string | null;
  toState: string;
  occurredAtUtc: string;
  userName: string;
  durationSinceLastMinutes: number | null;
};

/**
 * Un mensaje SRI real e individual — nunca resumido, traducido ni reescrito. Espejo del DTO
 * backend `ElectronicDocumentMessageDto`. `code`/`additionalInfo` son opcionales porque el SRI
 * no siempre los envía; `message` siempre existe. `messageType` refleja el literal de `<tipo>`
 * del SRI (p.ej. "ERROR", "ADVERTENCIA") tal cual — nunca normalizado a un enum cerrado.
 */
export type ElectronicDocumentMessageDto = {
  code: string | null;
  messageType: string;
  message: string;
  additionalInfo: string | null;
  occurredAtUtc: string;
};

/** Datos técnicos "solo para soporte" — espejo de `ElectronicDocumentTechnicalInfoDto`. */
export type ElectronicDocumentTechnicalInfoDto = {
  accessKey: string | null;
  authorizationNumber: string | null;
  environment: string | null;
  authorizationDate: string | null;
  retryCount: number;
  lastAttemptUtc: string | null;
  correlationId: string | null;
};

/**
 * Contrato único y reutilizable del diagnóstico de un documento electrónico — espejo de
 * `ElectronicDocumentDiagnosticDto` (backend). Es la única propiedad que
 * `ElectronicDocumentDiagnosticPanel` necesita para renderizarse desde cualquier módulo.
 */
export type ElectronicDocumentDiagnosticDto = {
  currentState: string;
  environment: string | null;
  lastAttemptUtc: string | null;
  messages: ElectronicDocumentMessageDto[];
  timeline: ElectronicDocumentTimelineEventDto[];
  technicalInfo: ElectronicDocumentTechnicalInfoDto;
  xmlDraftAvailable: boolean;
  xmlSignedAvailable: boolean;
  xmlAuthorizedAvailable: boolean;
};
