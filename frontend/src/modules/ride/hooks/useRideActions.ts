import { useCallback, useState } from "react";
import { message } from "../../../lib/messages";
import { rideService, type RideGenerationResultDto } from "../api/rideService";
import { downloadBlob } from "../../../lib/download";

// Mismo idioma de fallback de errores usado en Ventas (useSalesPage.ts: handleAuthorize/
// handleCancel/handleGenerateElectronicDocument) — nunca se muestra el error HTTP crudo, siempre
// un mensaje en español con degradación ordenada.
function extractErrorMessage(e: unknown, fallback: string): string {
  const err = e as {
    response?: {
      data?: { message?: { user?: string }; data?: { errors?: string[] } };
    };
    message?: string;
  };
  return (
    err?.response?.data?.message?.user ??
    err?.response?.data?.data?.errors?.[0] ??
    err?.message ??
    fallback
  );
}

function ridePdfFileName(
  documentNumber: string | undefined,
  sourceEntityId: string,
): string {
  return `RIDE-${documentNumber ?? sourceEntityId}.pdf`;
}

/**
 * Interpreta el Outcome de negocio de Ride (nunca un error HTTP — GetOrGenerateRideQuery /
 * RegenerateRideCommand siempre responden 200) y muestra el mensaje correspondiente.
 * Devuelve `true` únicamente cuando hay un PDF real disponible para ver/descargar.
 */
function announceOutcome(result: RideGenerationResultDto): boolean {
  switch (result.outcome) {
    case "Generated":
    case "Cached":
      return true;
    case "PendingSource":
      message.warning(
        "El documento electrónico aún no está autorizado por el SRI. Intente nuevamente en unos minutos.",
      );
      return false;
    case "NotApplicable":
      message.warning(
        "Este documento no tiene un comprobante electrónico autorizado (rechazado, anulado o inexistente) — no se puede generar el RIDE.",
      );
      return false;
    case "Failed":
      message.error(
        "No se pudo generar el RIDE. Intente nuevamente o contacte soporte.",
      );
      return false;
    default:
      return false;
  }
}

async function openRideInNewTab(
  sourceModule: string,
  sourceEntityId: string,
): Promise<void> {
  const blob = await rideService.getContentBlob(sourceModule, sourceEntityId);
  const url = URL.createObjectURL(blob);
  // No se revoca inmediatamente: la pestaña nueva sigue necesitando la URL viva. El navegador
  // libera la memoria al cerrar esa pestaña — mismo criterio que useAuthenticatedImage.ts, que
  // sí revoca porque el <img> vive en este mismo documento (ver limpieza en su efecto).
  window.open(url, "_blank");
}

/**
 * Acciones RIDE (Ver / Descargar / Regenerar) de cualquier consumidor — Ventas (Factura, Nota de
 * Crédito de devolución) y el Monitor electrónico. El consumidor identifica su documento con el
 * mismo par (sourceModule, sourceEntityId) que los contratos públicos de Ride; este hook es la
 * única interpretación de `RideOutcome` en el frontend.
 */
export function useRideActions(sourceModule: string) {
  const [ridePending, setRidePending] = useState(false);

  const handleViewRide = useCallback(async (sourceEntityId: string) => {
    setRidePending(true);
    try {
      const result = await rideService.getOrGenerate(
        sourceModule,
        sourceEntityId,
      );
      if (announceOutcome(result)) {
        await openRideInNewTab(sourceModule, sourceEntityId);
      }
    } catch (e) {
      message.error(extractErrorMessage(e, "No se pudo obtener el RIDE."));
    }
    setRidePending(false);
  }, [sourceModule]);

  const handleDownloadRide = useCallback(
    async (sourceEntityId: string, documentNumber?: string) => {
      setRidePending(true);
      try {
        const result = await rideService.getOrGenerate(
          sourceModule,
          sourceEntityId,
        );
        if (announceOutcome(result)) {
          const blob = await rideService.getContentBlob(
            sourceModule,
            sourceEntityId,
          );
          downloadBlob(blob, ridePdfFileName(documentNumber, sourceEntityId));
        }
      } catch (e) {
        message.error(extractErrorMessage(e, "No se pudo descargar el RIDE."));
      }
      setRidePending(false);
    },
    [sourceModule],
  );

  const handleRegenerateRide = useCallback(async (sourceEntityId: string) => {
    setRidePending(true);
    try {
      const result = await rideService.regenerate(
        sourceModule,
        sourceEntityId,
      );
      if (announceOutcome(result)) {
        message.success("RIDE regenerado correctamente.");
        await openRideInNewTab(sourceModule, sourceEntityId);
      }
    } catch (e) {
      message.error(extractErrorMessage(e, "No se pudo regenerar el RIDE."));
    }
    setRidePending(false);
  }, [sourceModule]);

  return {
    ridePending,
    handleViewRide,
    handleDownloadRide,
    handleRegenerateRide,
  };
}
