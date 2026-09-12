import { payablesService } from "../../payables/api/payablesService";

/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — cuotas pendientes de un proveedor, exclusivamente desde
 * `/api/v1/payables` (AccountsPayable/Installments) — nunca desde Compras/Gastos origen. El
 * listado (`GET /api/v1/payables`) solo trae totales por cabecera; el detalle
 * (`GET /api/v1/payables/{id}`) es la única fuente con el Id real de cada cuota, que es lo que
 * `SupplierPaymentApplicationLineRequest.accountsPayableInstallmentId` necesita.
 *
 * SUPPLIER-PAYMENT-PROVIDER-PORTFOLIO-VISIBILITY-01 — se agregan `issueDate`/`totalAmount`/
 * `paidAmount`/`status` (ya presentes en el detalle de CxP) para alimentar la cartera visible
 * de `/supplier-payments/new`; no cambia el origen de datos ni agrega un endpoint nuevo.
 */
export interface PendingInstallmentOption {
  installmentId: string;
  payableId: string;
  documentType: string;
  documentNumber: string;
  installmentNumber: number;
  issueDate: string;
  dueDate: string;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: string;
}

export const pendingPayablesFacade = {
  async listPendingInstallments(supplierId: string): Promise<PendingInstallmentOption[]> {
    const [pending, partial] = await Promise.all([
      payablesService.list({ supplierId, status: "pending" }, 1, 100),
      payablesService.list({ supplierId, status: "partiallypaid" }, 1, 100),
    ]);
    const headers = [...pending.items, ...partial.items];
    if (headers.length === 0) return [];

    const details = await Promise.all(headers.map((h) => payablesService.getById(h.id)));

    const rows = details.flatMap((d) =>
      d.installments
        .filter(
          (i) => i.status !== "paid" && i.status !== "cancelled" && i.outstandingAmount > 0,
        )
        .map((i) => ({
          installmentId: i.installmentId,
          payableId: d.id,
          documentType: d.documentType,
          documentNumber: d.documentNumber,
          installmentNumber: i.installmentNumber,
          issueDate: d.issueDate,
          dueDate: i.dueDate,
          totalAmount: i.amount,
          paidAmount: i.paidAmount,
          outstandingAmount: i.outstandingAmount,
          status: i.status,
        })),
    );

    return rows.sort(
      (a, b) =>
        a.dueDate.localeCompare(b.dueDate) ||
        a.issueDate.localeCompare(b.issueDate) ||
        a.documentNumber.localeCompare(b.documentNumber),
    );
  },
};
