import { useCallback, useEffect, useState } from "react";
import { ZHDrawer } from "../../../components/zh/ZHDrawer";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { LoadingState } from "../../../components/PageShell";
import { ElectronicDocumentDiagnosticPanel } from "../../../components/zh/electronicDocuments/ElectronicDocumentDiagnosticPanel";
import { electronicDocumentDiagnosticService } from "../../../components/zh/electronicDocuments/electronicDocumentDiagnosticService";
import type {
  ElectronicDocumentDiagnosticDto,
  ElectronicDocumentXmlVariant,
} from "../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes";
import { useI18n } from "../../../i18n/i18n";
import { formatApiError } from "../../lib/formatApiError";
import { electronicDocumentAccessFacade } from "../../electronicDocuments/facades/electronicDocumentAccessFacade";

const SOURCE_MODULE = "Sales";

type Props = {
  open: boolean;
  invoiceId: string;
  invoiceNumber: string | null;
  onClose: () => void;
};

/**
 * Segundo consumidor real de `ElectronicDocumentDiagnosticPanel` (además del Monitor) — prueba
 * que la infraestructura de diagnóstico SRI es genuinamente reutilizable entre módulos, sin
 * lógica específica de Ventas dentro del panel compartido. Obtiene el diagnóstico por
 * sourceModule/sourceEntityId (la factura no conoce el Id interno de ElectronicDocument).
 */
export function SalesElectronicDiagnosticDrawer({
  open,
  invoiceId,
  invoiceNumber,
  onClose,
}: Props) {
  const { t } = useI18n();

  const [diagnostic, setDiagnostic] =
    useState<ElectronicDocumentDiagnosticDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [xmlContent, setXmlContent] = useState<string | null>(null);
  const [xmlVariant, setXmlVariant] =
    useState<ElectronicDocumentXmlVariant | null>(null);
  const [xmlLoading, setXmlLoading] = useState(false);
  const [xmlError, setXmlError] = useState<string | null>(null);

  const loadDiagnostic = useCallback(async () => {
    setLoading(true);
    setError(null);
    setXmlContent(null);
    setXmlVariant(null);
    setXmlError(null);
    try {
      const data =
        await electronicDocumentDiagnosticService.getDiagnosticBySource(
          SOURCE_MODULE,
          invoiceId,
        );
      setDiagnostic(data);
    } catch (e) {
      setError(formatApiError(e));
      setDiagnostic(null);
    } finally {
      setLoading(false);
    }
  }, [invoiceId]);

  useEffect(() => {
    if (open) {
      void loadDiagnostic();
    } else {
      setDiagnostic(null);
      setError(null);
    }
  }, [open, loadDiagnostic]);

  const viewXml = async (variant: ElectronicDocumentXmlVariant) => {
    setXmlLoading(true);
    setXmlError(null);
    setXmlContent(null);
    setXmlVariant(variant);
    try {
      const xml = await electronicDocumentAccessFacade.getXml(
        SOURCE_MODULE,
        invoiceId,
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
    <ZHDrawer
      open={open}
      onClose={onClose}
      size="lg"
      title={t("electronicDocuments.monitor.detail.title")}
      subtitle={invoiceNumber ?? undefined}
    >
      {loading && (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      )}
      {error && (
        <ZHPageNotice
          variant="error"
          message={t("electronicDocuments.monitor.detail.loadError")}
          detail={error}
        />
      )}
      {diagnostic && !loading && (
        <ElectronicDocumentDiagnosticPanel
          diagnostic={diagnostic}
          xmlContent={xmlContent}
          xmlVariant={xmlVariant}
          xmlLoading={xmlLoading}
          xmlError={xmlError}
          downloadFileBaseName={invoiceNumber ?? invoiceId}
          onViewXml={(variant) => void viewXml(variant)}
        />
      )}
    </ZHDrawer>
  );
}
