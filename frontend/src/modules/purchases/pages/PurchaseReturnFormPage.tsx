import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { PurchaseReturnDraftFormSection } from "../components/PurchaseReturnDraftFormSection";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import type { PurchaseReturnDto } from "../api/purchaseReturnService";

/**
 * Crea un borrador de devolución de compra contra una factura confirmada. La
 * factura origen llega siempre por `?invoiceId=` (botón "Devolución" en
 * `PurchasesPage.tsx`, detalle de la factura) — a diferencia de
 * `SalesReturnFormPage.tsx`, esta página no incluye un selector de factura
 * propio (`SalesReturnInvoicePicker` no aplica aquí, Fase 12 solo crea, la
 * edición de un Draft ya existente vive en `PurchaseReturnDetailPage.tsx`).
 *
 * FLOW-READY-02C.3R — la carga del formulario (motivo + líneas devolvibles + envío) se extrajo a
 * `PurchaseReturnDraftFormSection` para poder reutilizarla también dentro de la pantalla
 * centralizada `PurchaseCreditNoteFormPage.tsx`; esta página conserva exactamente el mismo
 * comportamiento de antes (misma ruta, mismo mensaje, misma navegación final).
 */
export function PurchaseReturnFormPage() {
  const [searchParams] = useSearchParams();
  const invoiceId = searchParams.get("invoiceId") ?? "";
  const navigate = useNavigate();

  const [invoice, setInvoice] = useState<PurchaseInvoiceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  useEffect(() => {
    if (!invoiceId) {
      setLoadError("Falta la factura de compra origen.");
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    purchaseService
      .getById(invoiceId)
      .then((inv) => {
        if (cancelled) return;
        setInvoice(inv);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setLoadError(
          formatApiRequestError(err, {
            generic: "No se pudo cargar la factura de compra.",
          }),
        );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [invoiceId]);

  const handleCreated = (dto: PurchaseReturnDto) => {
    message.success("Borrador de devolución creado correctamente.");
    navigate(`/purchases/returns/${dto.id}`, { replace: true });
  };

  if (loading) {
    return (
      <PageShell title="Nueva devolución de compra" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (loadError || !invoice) {
    return (
      <PageShell title="Nueva devolución de compra">
        <ZHPageNotice
          variant="error"
          message="No se pudo iniciar la devolución"
          detail={loadError}
        />
      </PageShell>
    );
  }

  return (
    <PageShell
      title="Nueva devolución de compra"
      subtitle={`Factura origen: ${invoice.invoiceNumber} — ${invoice.supplierName}`}
    >
      <PurchaseReturnDraftFormSection
        invoice={invoice}
        onCreated={handleCreated}
        onCancel={() => navigate("/purchases")}
      />
    </PageShell>
  );
}
