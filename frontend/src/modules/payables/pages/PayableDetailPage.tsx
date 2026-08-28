import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { EmptyState, LoadingState, NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatApiRequestError } from "../../lib/apiError";
import {
  payablesService,
  type PayableDetailDto,
  type PayableOriginType,
} from "../api/payablesService";
import { PayableInstallmentsTable } from "../components/PayableInstallmentsTable";
import { PayableOriginBadge } from "../components/PayableOriginBadge";
import { PayableStatusBadge } from "../components/PayableStatusBadge";
import "../styles/payables.css";

const PERMISSIONS = { view: "payables.view" } as const;

/**
 * Ruta segura al documento origen — solo para orígenes con una pantalla real y navegable
 * ya existente. `Manual` (y cualquier origen futuro sin ruta) no tiene link: se muestra
 * solo el identificador/origen, tal como pide el ticket.
 */
function originRoute(originType: PayableOriginType, originId: string): string | null {
  switch (originType) {
    case "PurchaseInvoice":
      return `/purchases?invoiceId=${originId}`;
    case "ExpenseDocument":
      return `/expenses/documents/${originId}`;
    default:
      return null;
  }
}

export function PayableDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const decimals = getDecimalConfig().totalAmount;

  const [payable, setPayable] = useState<PayableDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const detail = await payablesService.getById(id);
      setPayable(detail);
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo cargar la cuenta por pagar.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  if (!canView) return <NoAccessPage title="Cuenta por pagar" />;

  const originLink = payable ? originRoute(payable.originType, payable.originId) : null;

  return (
    <PageShell
      kicker="Finanzas"
      title="Cuenta por pagar"
      subtitle="Detalle de saldo y cuotas — solo lectura."
      action={
        <div className="pay-detail-actions">
          {payable && <PayableStatusBadge status={payable.status} />}
          <ZHBtn type="button" variant="ghost" onClick={() => navigate("/payables")}>
            <span className="material-symbols-outlined" aria-hidden="true">
              arrow_back
            </span>
            Volver
          </ZHBtn>
        </div>
      }
    >
      {loading && <LoadingState />}

      {!loading && error && <EmptyState message={error} />}

      {!loading && !error && !payable && (
        <EmptyState message="No se encontro la cuenta por pagar solicitada." />
      )}

      {!loading && !error && payable && (
        <>
          <ZHCard title="Datos generales">
            <div className="pay-detail-summary">
              <ZHField label="Proveedor" readOnly>
                {payable.supplierName || "—"}
              </ZHField>
              <ZHField label="Origen" readOnly>
                <PayableOriginBadge originType={payable.originType} />
              </ZHField>
              <ZHField label="Documento" readOnly>
                {payable.documentType} {payable.documentNumber}
              </ZHField>
              <ZHField label="Fecha emision" readOnly>
                {formatDate(payable.issueDate)}
              </ZHField>
              <ZHField label="Fecha contable" readOnly>
                {formatDate(payable.accountingDate)}
              </ZHField>
              <ZHField label="Documento origen" readOnly>
                {originLink ? (
                  <Link className="pay-detail-origin-link" to={originLink}>
                    Ver documento{" "}
                    <span className="material-symbols-outlined" aria-hidden="true">
                      open_in_new
                    </span>
                  </Link>
                ) : (
                  <span>{payable.originType}</span>
                )}
              </ZHField>
            </div>
          </ZHCard>

          <ZHCard title="Saldo">
            <div className="pay-detail-summary">
              <ZHField label="Total" readOnly>
                <ZHMoneyValue value={payable.totalAmount} decimals={decimals} emphasis="strong" />
              </ZHField>
              <ZHField label="Pagado" readOnly>
                <ZHMoneyValue value={payable.paidAmount} decimals={decimals} />
              </ZHField>
              <ZHField label="Saldo" readOnly>
                <ZHMoneyValue
                  value={payable.outstandingAmount}
                  decimals={decimals}
                  emphasis="total"
                />
              </ZHField>
            </div>
          </ZHCard>

          <ZHCard title="Cuotas">
            <PayableInstallmentsTable installments={payable.installments} />
          </ZHCard>
        </>
      )}
    </PageShell>
  );
}

export default PayableDetailPage;
