import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";

const BASE = "/api/v1/ride";

// ── Tipos ────────────────────────────────────────────────────────────────
// Espejo de ERP.Application.Modules.Ride.DTOs (RideOutcome, RidePdfMetadataDto,
// RideGenerationResultDto) — contrato público congelado de Ride (ADR-025 §7).

export type RideOutcome =
  "Generated" | "Cached" | "PendingSource" | "NotApplicable" | "Failed";

export interface RidePdfMetadataDto {
  templateId: string;
  templateVersion: string;
  brandingVersion: string;
  rendererVersion: string;
  sourceXmlHash: string;
  generatedAtUtc: string;
  wasCached: boolean;
}

export interface RideGenerationResultDto {
  outcome: RideOutcome;
  storagePath: string | null;
  metadata: RidePdfMetadataDto | null;
  reasonCode: string | null;
}

/**
 * Cliente reutilizable de Ride — cualquier módulo consumidor (Ventas hoy; Inventario, Activos,
 * POS a futuro) identifica su documento con (sourceModule, sourceEntityId), igual que los
 * contratos públicos de Ride (GetOrGenerateRideQuery/RegenerateRideCommand).
 */
export const rideService = {
  /** Obtiene el RIDE ya generado o lo genera si corresponde — siempre 200 con un Outcome de negocio. */
  getOrGenerate(
    sourceModule: string,
    sourceEntityId: string,
  ): Promise<RideGenerationResultDto> {
    return apiGet<RideGenerationResultDto>(BASE, {
      params: { sourceModule, sourceEntityId },
    });
  },

  /** Fuerza la regeneración del RIDE aunque el cache siga siendo válido. */
  regenerate(
    sourceModule: string,
    sourceEntityId: string,
  ): Promise<RideGenerationResultDto> {
    return apiPost<RideGenerationResultDto>(`${BASE}/regenerate`, {
      sourceModule,
      sourceEntityId,
    });
  },

  /**
   * Bytes del PDF ya generado. Debe llamarse únicamente después de confirmar, vía
   * `getOrGenerate`/`regenerate`, que el Outcome es `Generated` o `Cached` — de lo contrario el
   * backend responde 404 (no hay RIDE disponible todavía). Bypasea `apiEnvelope` (igual que
   * `companyProfileService.getLogoBlob`, único precedente de blobs en este repo) porque la
   * respuesta no es JSON.
   */
  async getContentBlob(
    sourceModule: string,
    sourceEntityId: string,
  ): Promise<Blob> {
    const { data } = await api.get<Blob>(`${BASE}/content`, {
      params: { sourceModule, sourceEntityId },
      responseType: "blob",
    });
    return data;
  },
};
