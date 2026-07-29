import { apiGet } from "../../../modules/lib/apiEnvelope";
import type { ElectronicDocumentDiagnosticDto } from "./electronicDocumentDiagnosticTypes";

export const electronicDocumentDiagnosticService = {
  /** Diagnóstico agnóstico de módulo — cualquier pantalla que conoce su propio sourceModule/sourceEntityId. */
  getDiagnosticBySource: (sourceModule: string, sourceEntityId: string) =>
    apiGet<ElectronicDocumentDiagnosticDto>(
      "/api/v1/electronic-documents/by-source",
      {
        params: { sourceModule, sourceEntityId },
      },
    ),
};
