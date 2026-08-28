import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { EmptyState, LoadingState, NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import {
  paymentMethodLookupFacade,
  type PaymentMethodDto,
} from "../../sales/facades/paymentMethodLookupFacade";
import {
  financialDestinationService,
  type CompanyFinancialDestinationDto,
} from "../../finance/api/financialDestinationService";
import { supplierPaymentService, type SupplierPaymentDto } from "../api/supplierPaymentService";
import { SupplierPaymentStatusBadge } from "../components/SupplierPaymentStatusBadge";
import "../styles/supplier-payments.css";

const PERMISSIONS = { view: "supplier-payments.view" } as const;

export function SupplierPaymentDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const decimals = getDecimalConfig().totalAmount;

  const [payment, setPayment] = useState<SupplierPaymentDto | null>(null);
  const [supplierName, setSupplierName] = useState("");
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [destinations, setDestinations] = useState<CompanyFinancialDestinationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const [detail, methodsList, destinationsList] = await Promise.all([
        supplierPaymentService.getById(id),
        paymentMethodLookupFacade.list(false),
        financialDestinationService.list(),
      ]);
      setPayment(detail);
      setMethods(methodsList);
      setDestinations(destinationsList);
      try {
        const supplier = await businessPartnerFacade.getBusinessPartner(detail.supplierId);
        setSupplierName(supplier.tradeName?.trim() || supplier.legalName);
      } catch {
        setSupplierName("");
      }
    } catch (err) {
      setError(
        formatApiRequestError(err, { generic: "No se pudo cargar el pago a proveedor." }),
      );
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  if (!canView) return <NoAccessPage title="Pago a proveedor" />;

  const methodsById = new Map(methods.map((m) => [m.id, m]));
  const destinationsById = new Map(destinations.map((d) => [d.id, d]));

  return (
    <PageShell
      kicker="Finanzas"
      title="Pago a proveedor"
      subtitle="Comprobante confirmado — sin edición posterior."
      action={
        <div className="sp-detail-actions">
          {payment && <SupplierPaymentStatusBadge status={payment.status} />}
          <ZHBtn type="button" variant="ghost" onClick={() => navigate("/supplier-payments")}>
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
      {!loading && !error && !payment && <EmptyState message="No se encontró el pago solicitado." />}

      {!loading && !error && payment && (
        <>
          <ZHCard title="Datos generales">
            <div className="sp-detail-summary">
              <ZHField label="Número" readOnly>
                {payment.displayNumber}
              </ZHField>
              <ZHField label="Número de sistema" readOnly>
                {payment.systemNumber}
              </ZHField>
              <ZHField label="Proveedor" readOnly>
                {supplierName || "—"}
              </ZHField>
              <ZHField label="Fecha de pago" readOnly>
                {formatDate(payment.paymentDate)}
              </ZHField>
              <ZHField label="Registrado" readOnly>
                {formatDateTime(payment.createdAt)}
              </ZHField>
              <ZHField label="Total" readOnly>
                <ZHMoneyValue value={payment.totalAmount} decimals={decimals} emphasis="strong" />
              </ZHField>
            </div>
          </ZHCard>

          <ZHCard title="Medios de pago">
            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>Medio</th>
                    <th>Caja / cuenta bancaria</th>
                    <th>Referencia</th>
                    <th className="zh-text-align-right">Monto</th>
                  </tr>
                </thead>
                <tbody>
                  {payment.methodLines.map((line) => (
                    <tr key={line.id}>
                      <td>{methodsById.get(line.paymentMethodId)?.name ?? "—"}</td>
                      <td>{destinationsById.get(line.financialDestinationId)?.name ?? "—"}</td>
                      <td>
                        {line.checkNumber
                          ? `Cheque ${line.checkNumber}${line.checkDate ? ` — ${formatDate(line.checkDate)}` : ""}`
                          : line.referenceNumber || "—"}
                      </td>
                      <td className="zh-text-align-right">
                        <ZHMoneyValue value={line.amount} decimals={decimals} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </ZHCard>

          <ZHCard title="Cuotas aplicadas">
            <div className="table-scroll">
              <table className="table">
                <thead>
                  <tr>
                    <th>Cuota</th>
                    <th className="zh-text-align-right">Monto aplicado</th>
                  </tr>
                </thead>
                <tbody>
                  {payment.applicationLines.map((line) => (
                    <tr key={line.id}>
                      <td>
                        <span className="sp-line-hint">{line.accountsPayableInstallmentId}</span>
                      </td>
                      <td className="zh-text-align-right">
                        <ZHMoneyValue value={line.amountApplied} decimals={decimals} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </ZHCard>
        </>
      )}
    </PageShell>
  );
}

export default SupplierPaymentDetailPage;
