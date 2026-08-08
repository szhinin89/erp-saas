import { useCallback, useState } from "react";
import { message } from "../../../lib/messages";
import {
  rideGenerationFacade,
  type RideGenerationResultDto,
} from "../../ride/facades/rideGenerationFacade";
import { downloadBlob } from "../../ride/utils/downloadBlob";

const SOURCE_MODULE = "Sales";

// Mismo idioma de fallback de errores usado en el resto de useSalesPage.ts
// (handleAuthorize/handleCancel/handleGenerateElectronicDocument) — nunca se muestra el error
// HTTP crudo, siempre un mensaje en español con degradación ordenada.
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
  invoiceNumber: string | undefined,
  sourceEntityId: string,
): string {
  return `RIDE-${invoiceNumber ?? sourceEntityId}.pdf`;
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
        "Esta factura no tiene un documento electrónico autorizado (rechazada, anulada o inexistente) — no se puede generar el RIDE.",
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

async function openRideInNewTab(sourceEntityId: string): Promise<void> {
  const blob = await rideGenerationFacade.getContentBlob(SOURCE_MODULE, sourceEntityId);
  const url = URL.createObjectURL(blob);
  // No se revoca inmediatamente: la pestaña nueva sigue necesitando la URL viva. El navegador
  // libera la memoria al cerrar esa pestaña — mismo criterio que useAuthenticatedImage.ts, que
  // sí revoca porque el <img> vive en este mismo documento (ver limpieza en su efecto).
  window.open(url, "_blank");
}

export function useRideActions() {
  const [ridePending, setRidePending] = useState(false);

  const handleViewRide = useCallback(async (sourceEntityId: string) => {
    setRidePending(true);
    try {
      const result = await rideGenerationFacade.getOrGenerate(
        SOURCE_MODULE,
        sourceEntityId,
      );
      if (announceOutcome(result)) {
        await openRideInNewTab(sourceEntityId);
      }
    } catch (e) {
      message.error(extractErrorMessage(e, "No se pudo obtener el RIDE."));
    }
    setRidePending(false);
  }, []);

  const handleDownloadRide = useCallback(
    async (sourceEntityId: string, invoiceNumber?: string) => {
      setRidePending(true);
      try {
        const result = await rideGenerationFacade.getOrGenerate(
          SOURCE_MODULE,
          sourceEntityId,
        );
        if (announceOutcome(result)) {
          const blob = await rideGenerationFacade.getContentBlob(
            SOURCE_MODULE,
            sourceEntityId,
          );
          downloadBlob(blob, ridePdfFileName(invoiceNumber, sourceEntityId));
        }
      } catch (e) {
        message.error(extractErrorMessage(e, "No se pudo descargar el RIDE."));
      }
      setRidePending(false);
    },
    [],
  );

  const handleRegenerateRide = useCallback(async (sourceEntityId: string) => {
    setRidePending(true);
    try {
      const result = await rideGenerationFacade.regenerate(
        SOURCE_MODULE,
        sourceEntityId,
      );
      if (announceOutcome(result)) {
        message.success("RIDE regenerado correctamente.");
        await openRideInNewTab(sourceEntityId);
      }
    } catch (e) {
      message.error(extractErrorMessage(e, "No se pudo regenerar el RIDE."));
    }
    setRidePending(false);
  }, []);

  return {
    ridePending,
    handleViewRide,
    handleDownloadRide,
    handleRegenerateRide,
  };
}
