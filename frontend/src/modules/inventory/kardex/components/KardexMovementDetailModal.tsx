import { useNavigate } from "react-router-dom";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { Badge } from "../../../../components/PageShell";
import {
  formatDate,
  formatDateTimeSeconds,
} from "../../../../lib/formatters/dateFormatters";
import { formatMoney, formatMoneyWithSymbol } from "../../../../lib/sanitizers";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import type { KardexMovementDetailDto } from "../../stock/api/kardexService";

type Props = {
  open: boolean;
  loading: boolean;
  detail: KardexMovementDetailDto | null;
  onClose: () => void;
  onNavigate: (movementId: string) => void;
  movementTypeLabels: Record<string, string>;
};

const DOC_TYPE_ROUTE: Record<string, string> = {
  PurchaseInvoice: "/purchases",
  SalesInvoice: "/sales",
};
const DOC_TYPE_PARAM: Record<string, string> = {
  PurchaseInvoice: "invoiceId",
  SalesInvoice: "invoiceId",
};
const DOC_TYPE_LABELS: Record<string, string> = {
  PurchaseInvoice: "Factura de Compra",
  SalesInvoice: "Factura de Venta",
  StockAdjustment: "Ajuste de Inventario",
  StockTransfer: "Transferencia entre Bodegas",
};

export function KardexMovementDetailModal({
  open,
  loading,
  detail,
  onClose,
  onNavigate,
  movementTypeLabels,
}: Props) {
  const navigate = useNavigate();
  const qty = getDecimalConfig().quantity;
  const cost = getDecimalConfig().purchaseUnitPrice;
  const total = getDecimalConfig().totalAmount;

  const m = detail?.movement;
  const typeLabel = m
    ? (movementTypeLabels[m.movementTypeName] ?? m.movementTypeName)
    : "";

  const goToSourceDocument = () => {
    if (!m?.sourceDocId || !m.sourceDocType) return;
    const route = DOC_TYPE_ROUTE[m.sourceDocType];
    const param = DOC_TYPE_PARAM[m.sourceDocType];
    if (!route || !param) return;
    navigate(`${route}?${param}=${m.sourceDocId}`);
  };

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="lg"
      title={
        m
          ? `Movimiento #${m.sequenceNumber} · ${typeLabel}`
          : "Expediente del movimiento"
      }
      subtitle={
        detail?.sourceDocument?.docNumber
          ? `Documento: ${detail.sourceDocument.docNumber}`
          : undefined
      }
    >
      {loading && <p>Cargando expediente...</p>}
      {!loading && detail && m && (
        <div className="kdx-modal-stack">
          {/* ── A. Hecho de Inventario ─────────────────────────────────── */}
          <Section icon="inventory_2" title="Hecho de Inventario">
            <div
              className="pdl-line__context kdx-grid-4"
            >
              <Field label="Bodega" value={m.warehouseId} mono />
              <Field label="Movimiento" value={typeLabel} />
              <Field
                label="Cantidad"
                value={`${m.quantity > 0 ? "+" : ""}${formatMoney(m.quantity, qty)} ${m.uomCode}`}
              />
              <Field
                label="Costo Unitario"
                value={
                  m.unitCost != null
                    ? formatMoneyWithSymbol(m.unitCost, cost)
                    : "—"
                }
              />
              <Field
                label="Costo Total"
                value={
                  m.totalCost != null
                    ? formatMoneyWithSymbol(m.totalCost, total)
                    : "—"
                }
              />
              <Field
                label="Saldo Antes"
                value={formatMoney(m.previousQuantity, qty)}
              />
              <Field
                label="Saldo Después"
                value={formatMoney(m.resultQuantity, qty)}
              />
              <Field
                label="Costo Promedio Corrido"
                value={formatMoneyWithSymbol(m.runningAverageCost, cost)}
              />
              <Field
                label="Valor de Inventario Corrido"
                value={formatMoneyWithSymbol(m.runningStockValue, total)}
              />
            </div>
          </Section>

          {/* ── B. Documento Origen ─────────────────────────────────────── */}
          <Section icon="description" title="Documento Origen">
            {detail.sourceDocument ? (
              <>
                <div
                  className="pdl-line__context kdx-grid-3"
                >
                  <Field
                    label="Tipo"
                    value={
                      DOC_TYPE_LABELS[detail.sourceDocument.docType] ??
                      detail.sourceDocument.docType
                    }
                  />
                  <Field
                    label="Número"
                    value={detail.sourceDocument.docNumber ?? "—"}
                    mono
                  />
                  {detail.sourceDocument.partnerName && (
                    <Field
                      label={
                        detail.sourceDocument.docType === "SalesInvoice"
                          ? "Cliente"
                          : "Proveedor"
                      }
                      value={detail.sourceDocument.partnerName}
                    />
                  )}
                  {detail.sourceDocument.unitPrice != null && (
                    <Field
                      label="Precio Comercial"
                      value={formatMoneyWithSymbol(
                        detail.sourceDocument.unitPrice,
                        cost,
                      )}
                    />
                  )}
                  {detail.sourceDocument.discountPct != null && (
                    <Field
                      label="Descuento"
                      value={`${formatMoney(detail.sourceDocument.discountPct, getDecimalConfig().percentage)}%`}
                    />
                  )}
                  {detail.sourceDocument.vatRate != null && (
                    <Field
                      label="IVA"
                      value={`${formatMoney(detail.sourceDocument.vatRate, getDecimalConfig().percentage)}%`}
                    />
                  )}
                  {detail.sourceDocument.reason && (
                    <Field
                      label="Motivo"
                      value={detail.sourceDocument.reason}
                    />
                  )}
                  {detail.sourceDocument.notes && (
                    <Field label="Notas" value={detail.sourceDocument.notes} />
                  )}
                </div>
                {DOC_TYPE_ROUTE[detail.sourceDocument.docType] && (
                  <ZHBtn
                    type="button"
                    variant="secondary"
                    className="zh-mt-10"
                    onClick={goToSourceDocument}
                  >
                    <span className="material-symbols-outlined zh-icon-md">
                      open_in_new
                    </span>
                    Ver documento origen
                  </ZHBtn>
                )}
              </>
            ) : (
              <p className="kdx-muted-text">
                Este movimiento no tiene documento origen asociado.
              </p>
            )}
          </Section>

          {/* ── Cadena Documental ────────────────────────────────────────── */}
          <Section icon="link" title="Cadena Documental">
            {detail.documentChain.links.length === 0 ? (
              <p className="kdx-muted-text">
                Sin eslabones documentales adicionales.
              </p>
            ) : (
              <div className="kdx-links-row">
                {detail.documentChain.links.map((link, i) => (
                  <Badge
                    key={i}
                    variant={link.isCurrent ? "info" : "neutral"}
                    label={
                      <>
                        {DOC_TYPE_LABELS[link.docType] ?? link.docType}
                        {link.docNumber ? ` · ${link.docNumber}` : ""}
                        {link.isCurrent ? " (actual)" : ""}
                      </>
                    }
                  />
                ))}
              </div>
            )}
          </Section>

          {/* ── Relaciones (navegación anterior/siguiente) ──────────────── */}
          <Section icon="timeline" title="Relaciones">
            <div
              className="kdx-relations-row"
            >
              <ZHBtn
                type="button"
                variant="secondary"
                disabled={!detail.relations.previous}
                onClick={() =>
                  detail.relations.previous &&
                  onNavigate(detail.relations.previous.movementId)
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  chevron_left
                </span>
                {detail.relations.previous
                  ? `#${detail.relations.previous.sequenceNumber} — ${movementTypeLabels[detail.relations.previous.movementTypeName] ?? detail.relations.previous.movementTypeName}`
                  : "Sin movimiento anterior"}
              </ZHBtn>
              <Badge
                variant="info"
                label={`Actual: #${detail.relations.current.sequenceNumber}`}
              />
              <ZHBtn
                type="button"
                variant="secondary"
                disabled={!detail.relations.next}
                onClick={() =>
                  detail.relations.next &&
                  onNavigate(detail.relations.next.movementId)
                }
              >
                {detail.relations.next
                  ? `#${detail.relations.next.sequenceNumber} — ${movementTypeLabels[detail.relations.next.movementTypeName] ?? detail.relations.next.movementTypeName}`
                  : "Sin movimiento siguiente"}
                <span className="material-symbols-outlined zh-icon-md">
                  chevron_right
                </span>
              </ZHBtn>
            </div>
          </Section>

          {/* ── C. Auditoría ─────────────────────────────────────────────── */}
          <Section icon="verified_user" title="Auditoría">
            <div
              className="pdl-line__context pdl-line__context--audit"
            >
              <Field label="Usuario" value={detail.actor.userName} />
              <Field
                label="Fecha de Creación"
                value={formatDateTimeSeconds(m.createdAt)}
              />
              <Field
                label="Fecha Efectiva"
                value={formatDate(m.effectiveDate)}
              />
              <Field
                label="Tipo de Documento (SourceDocType)"
                value={m.sourceDocType ?? "—"}
                mono
              />
              <Field
                label="Id de Documento (SourceDocId)"
                value={m.sourceDocId ?? "—"}
                mono
              />
            </div>
          </Section>

          {/* ── D. Contabilidad ──────────────────────────────────────────── */}
          <Section icon="account_balance" title="Contabilidad">
            <div
              className="pf-mini-card__body kdx-accounting-info"
            >
              <span
                className="material-symbols-outlined zh-icon-md"
              >
                schedule
              </span>
              Integración contable preparada — aún no hay asiento generado para
              este movimiento.
            </div>
          </Section>
        </div>
      )}
    </ZHModal>
  );
}

function Section({
  icon,
  title,
  children,
}: {
  icon: string;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="pf-card">
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            {icon}
          </span>{" "}
          {title}
        </h4>
      </div>
      <div className="pf-card__body">{children}</div>
    </div>
  );
}

function Field({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="pdl-ctx-col">
      <div className="pdl-ctx-col__title">{label}</div>
      <div className={mono ? "kdx-field-value kdx-mono" : "kdx-field-value"}>
        {value}
      </div>
    </div>
  );
}
