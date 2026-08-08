import { useCallback, useEffect, useState } from "react";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { LoadingState } from "../../../components/PageShell";
import { ElectronicDocumentDiagnosticPanel } from "../../../components/zh/electronicDocuments/ElectronicDocumentDiagnosticPanel";
import { electronicDocumentDiagnosticService } from "../../../components/zh/electronicDocuments/electronicDocumentDiagnosticService";
import type {
  ElectronicDocumentDiagnosticDto,
  ElectronicDocumentXmlVariant,
} from "../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes";
import { electronicDocumentAccessFacade } from "../../electronicDocuments/facades/electronicDocumentAccessFacade";
import { formatApiError } from "../../lib/formatApiError";
import { message } from "../../../lib/messages";
import { useRideActions } from "../hooks/useRideActions";

// SalesReturn.Authorize()/AuthorizeSalesReturnHandler siempre registra el documento electrónico
// bajo SourceModule="Sales" (mismo valor que usa Factura) — no es un código SRI, es el nombre del
// módulo de origen ya usado por el backend (AuthorizeSalesReturnUseCases.cs) y por
// SalesElectronicDiagnosticDrawer para Factura.
const SOURCE_MODULE = "Sales";

type Props = {
  salesReturnId: string;
  returnNumber: string;
};

/**
 * P0-01 Fase 14 — sección "Nota de Crédito Electrónica" del detalle de SalesReturn. Reutiliza
 * exactamente la misma infraestructura de diagnóstico/RIDE que ya usa Ventas para Factura, sin
 * ningún componente ni lógica nueva de polling/mapeo de estado/descarga:
 * - `electronicDocumentDiagnosticService.getDiagnosticBySource` + `ElectronicDocumentDiagnosticPanel`
 *   (mismo patrón que `SalesElectronicDiagnosticDrawer.tsx`, agnóstico de módulo desde ADR-024).
 * - `useRideActions()` (mismo hook que `SalesPage.tsx`, ya parametrizado por sourceEntityId — no
 *   requirió ningún cambio: `SOURCE_MODULE` interno ya es "Sales" para ambos documentos).
 *
 * Campos pedidos por la Fase 14 que la Fase 14 NO puede completar sin tocar Backend (fuera de
 * alcance de esta tarea, reportado en el entregable en vez de inventarse aquí):
 * - Número de Nota de Crédito (`SalesReturn.CreditNoteDocumentNumber`) — no existe en
 *   `SalesReturnDto` (ERP.Application/Modules/Sales/DTOs/SalesReturnDto.cs).
 * - Fecha de emisión del documento electrónico — `ElectronicDocumentDiagnosticDto` (el contrato
 *   que expone `GetElectronicDocumentDiagnosticBySourceQuery`) no incluye `CreatedAt`/fecha de
 *   emisión; solo `ElectronicDocumentDetailDto` (Monitor, requiere el Id interno de
 *   `ElectronicDocument`, que este flujo por-fuente nunca recibe) lo tiene.
 */
export function SalesReturnCreditNoteSection({
  salesReturnId,
  returnNumber,
}: Props) {
  const [diagnostic, setDiagnostic] =
    useState<ElectronicDocumentDiagnosticDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  const [xmlContent, setXmlContent] = useState<string | null>(null);
  const [xmlVariant, setXmlVariant] =
    useState<ElectronicDocumentXmlVariant | null>(null);
  const [xmlLoading, setXmlLoading] = useState(false);
  const [xmlError, setXmlError] = useState<string | null>(null);

  const ride = useRideActions();

  const loadDiagnostic = useCallback(async () => {
    setLoading(true);
    setError(null);
    setNotFound(false);
    setXmlContent(null);
    setXmlVariant(null);
    setXmlError(null);
    try {
      const data =
        await electronicDocumentDiagnosticService.getDiagnosticBySource(
          SOURCE_MODULE,
          salesReturnId,
        );
      setDiagnostic(data);
    } catch (e) {
      const status = (e as { response?: { status?: number } })?.response
        ?.status;
      if (status === 404) {
        // Mismo contrato que GetElectronicDocumentDiagnosticBySourceQueryHandler documenta:
        // NotFound = "todavía no hay documento electrónico registrado" — estado válido, no error.
        setNotFound(true);
        setDiagnostic(null);
      } else {
        const msg = formatApiError(e);
        setError(msg);
        setDiagnostic(null);
        message.error(msg);
      }
    } finally {
      setLoading(false);
    }
  }, [salesReturnId]);

  useEffect(() => {
    void loadDiagnostic();
  }, [loadDiagnostic]);

  const viewXml = async (variant: ElectronicDocumentXmlVariant) => {
    setXmlLoading(true);
    setXmlError(null);
    setXmlContent(null);
    setXmlVariant(variant);
    try {
      const xml = await electronicDocumentAccessFacade.getXml(
        SOURCE_MODULE,
        salesReturnId,
        variant,
      );
      setXmlContent(xml);
    } catch (e) {
      setXmlError(formatApiError(e));
    } finally {
      setXmlLoading(false);
    }
  };

  return (
    <ZHCard
      title="Nota de Crédito Electrónica"
      actions={
        <ZHBtn
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => void loadDiagnostic()}
          disabled={loading}
        >
          Refrescar estado
        </ZHBtn>
      }
    >
      {loading && <LoadingState />}

      {error && !loading && (
        <ZHPageNotice
          variant="error"
          message="No se pudo consultar el documento electrónico."
          detail={error}
        />
      )}

      {notFound && !loading && (
        <p className="edm-hint-sm">
          Todavía no se ha registrado un documento electrónico para esta
          devolución.
        </p>
      )}

      {diagnostic && !loading && (
        <ElectronicDocumentDiagnosticPanel
          diagnostic={diagnostic}
          xmlContent={xmlContent}
          xmlVariant={xmlVariant}
          xmlLoading={xmlLoading}
          xmlError={xmlError}
          downloadFileBaseName={returnNumber}
          onViewXml={(variant) => void viewXml(variant)}
          statusExtra={
            <div className="sr-credit-note-actions">
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                disabled={ride.ridePending}
                onClick={() => void ride.handleViewRide(salesReturnId)}
                title="Abre el PDF del RIDE en una pestaña nueva. Reutiliza el ya generado si no cambió nada."
              >
                Ver RIDE
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                disabled={ride.ridePending}
                onClick={() =>
                  void ride.handleDownloadRide(salesReturnId, returnNumber)
                }
              >
                Descargar RIDE
              </ZHBtn>
            </div>
          }
        />
      )}
    </ZHCard>
  );
}
