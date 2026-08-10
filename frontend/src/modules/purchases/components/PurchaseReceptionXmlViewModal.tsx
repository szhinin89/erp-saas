import { ZHModal } from "../../../components/zh/ZHModal";
import { Badge } from "../../../components/PageShell";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { useI18n } from "../../../i18n/i18n";
import type {
  PurchaseReceptionXmlView,
  PurchaseReceptionXmlViewLine,
} from "../api/purchaseReceptionService";

type Props = {
  open: boolean;
  loading: boolean;
  error: string | null;
  data: PurchaseReceptionXmlView | null;
  onClose: () => void;
};

/**
 * Vista de solo lectura del XML ya guardado en recepción electrónica (FLOW-READY-02E.1). No hay
 * botones de aplicar/sobrescribir/edición/matching/crear compra/crear NC — solo lectura de lo que
 * ya está persistido en `PurchaseReceptionDocument`.
 */
export function PurchaseReceptionXmlViewModal({
  open,
  loading,
  error,
  data,
  onClose,
}: Props) {
  const { t } = useI18n();
  const qty = getDecimalConfig().quantity;
  const cost = getDecimalConfig().purchaseUnitPrice;
  const total = getDecimalConfig().totalAmount;
  const pct = getDecimalConfig().percentage;

  const lineColumns: ZHDataTableColumn<PurchaseReceptionXmlViewLine>[] = [
    {
      key: "code",
      header: t("purchases.reception.xmlView.lines.code", "Código"),
      render: (row) => row.mainCode ?? row.auxCode ?? "—",
    },
    {
      key: "description",
      header: t("purchases.reception.xmlView.lines.description", "Descripción"),
      render: (row) => row.description,
    },
    {
      key: "quantity",
      header: t("purchases.reception.xmlView.lines.quantity", "Cantidad"),
      align: "right",
      render: (row) => formatMoney(row.quantity, qty),
    },
    {
      key: "unitPrice",
      header: t("purchases.reception.xmlView.lines.unitPrice", "Precio unit."),
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.unitPrice, cost),
    },
    {
      key: "discountAmount",
      header: t("purchases.reception.xmlView.lines.discount", "Descuento"),
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.discountAmount, total),
    },
    {
      key: "taxableBase",
      header: t("purchases.reception.xmlView.lines.taxableBase", "Base imponible"),
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.taxableBase, total),
    },
    {
      key: "vatAmount",
      header: t("purchases.reception.xmlView.lines.vat", "IVA"),
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.vatAmount, total),
    },
    {
      key: "iceAmount",
      header: t("purchases.reception.xmlView.lines.ice", "ICE"),
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.iceAmount, total),
    },
    {
      key: "totalAmount",
      header: t("purchases.reception.xmlView.lines.total", "Total"),
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.totalAmount, total),
    },
  ];

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="xl"
      title={t("purchases.reception.xmlView.title", "XML del comprobante")}
      subtitle={data?.documentNumber}
    >
      {loading && (
        <p>{t("purchases.reception.xmlView.loading", "Cargando XML...")}</p>
      )}
      {!loading && error && <p className="pur-xmlview-error">{error}</p>}
      {!loading && !error && data && (
        <div className="pur-xmlview-stack">
          <p className="pur-xmlview-disclaimer">
            {t(
              "purchases.reception.xmlView.disclaimer",
              "Estos datos corresponden al XML guardado en recepción electrónica. Son solo informativos.",
            )}
          </p>

          <Section title={t("purchases.reception.xmlView.section.document", "Documento")}>
            <div className="pur-xmlview-grid">
              <Field
                label={t("purchases.reception.xmlView.field.documentNumber", "Número")}
                value={data.documentNumber}
                mono
              />
              <Field
                label={t("purchases.reception.xmlView.field.issueDate", "Fecha de emisión")}
                value={formatDate(data.issueDate)}
              />
              <Field
                label={t("purchases.reception.xmlView.field.accessKey", "Clave de acceso")}
                value={data.accessKey}
                mono
              />
              <Field
                label={t(
                  "purchases.reception.xmlView.field.authorizationNumber",
                  "Número de autorización",
                )}
                value={data.authorizationNumber ?? "—"}
                mono
              />
              <Field
                label={t(
                  "purchases.reception.xmlView.field.authorizationDate",
                  "Fecha de autorización",
                )}
                value={
                  data.authorizationDate ? formatDateTime(data.authorizationDate) : "—"
                }
              />
            </div>
          </Section>

          <Section title={t("purchases.reception.xmlView.section.supplier", "Proveedor")}>
            <div className="pur-xmlview-grid">
              <Field
                label={t("purchases.reception.xmlView.field.supplierName", "Razón social")}
                value={data.supplierName}
              />
              <Field
                label={t(
                  "purchases.reception.xmlView.field.supplierTradeName",
                  "Nombre comercial",
                )}
                value={data.supplierTradeName ?? "—"}
              />
              <Field
                label={t("purchases.reception.xmlView.field.supplierTaxId", "RUC")}
                value={data.supplierTaxId}
                mono
              />
            </div>
          </Section>

          {data.documentType === "CREDIT_NOTE" && (
            <Section
              title={t(
                "purchases.reception.xmlView.section.modifiedDocument",
                "Documento afectado",
              )}
            >
              <div className="pur-xmlview-grid">
                <Field
                  label={t(
                    "purchases.reception.xmlView.field.modifiedDocumentNumber",
                    "Número",
                  )}
                  value={data.modifiedDocumentNumber ?? "—"}
                  mono
                />
                <Field
                  label={t(
                    "purchases.reception.xmlView.field.modifiedDocumentType",
                    "Tipo (código SRI)",
                  )}
                  value={data.modifiedDocumentType ?? "—"}
                  mono
                />
                <Field
                  label={t(
                    "purchases.reception.xmlView.field.modifiedDocumentDate",
                    "Fecha de emisión",
                  )}
                  value={
                    data.modifiedDocumentDate
                      ? formatDate(data.modifiedDocumentDate)
                      : "—"
                  }
                />
                <Field
                  label={t(
                    "purchases.reception.xmlView.field.modificationReason",
                    "Motivo",
                  )}
                  value={data.modificationReason ?? "—"}
                />
              </div>
            </Section>
          )}

          <Section title={t("purchases.reception.xmlView.section.totals", "Totales")}>
            <div className="pur-xmlview-grid">
              <Field
                label={t("purchases.reception.xmlView.field.subtotal", "Subtotal")}
                value={formatMoneyWithSymbol(data.subtotal, total)}
              />
              <Field
                label={t("purchases.reception.xmlView.field.discountAmount", "Descuento")}
                value={formatMoneyWithSymbol(data.discountAmount, total)}
              />
              <Field
                label={t("purchases.reception.xmlView.field.iceAmount", "ICE")}
                value={formatMoneyWithSymbol(data.iceAmount, total)}
              />
              <Field
                label={t("purchases.reception.xmlView.field.vatAmount", "IVA")}
                value={formatMoneyWithSymbol(data.vatAmount, total)}
              />
              <Field
                label={t("purchases.reception.xmlView.field.totalAmount", "Total")}
                value={formatMoneyWithSymbol(data.totalAmount, total)}
              />
            </div>
          </Section>

          <Section title={t("purchases.reception.xmlView.section.taxes", "Impuestos")}>
            {data.taxSummaries.length === 0 ? (
              <p className="pur-xmlview-muted">
                {t(
                  "purchases.reception.xmlView.taxes.empty",
                  "Sin desglose de impuestos disponible.",
                )}
              </p>
            ) : (
              <div className="pur-xmlview-tax-list">
                {data.taxSummaries.map((tax, i) => (
                  <Badge
                    key={i}
                    variant="neutral"
                    label={`${tax.taxType}/${tax.taxCode}${
                      tax.taxRate != null ? ` (${formatMoney(tax.taxRate, pct)}%)` : ""
                    } · Base ${formatMoney(tax.taxableBase, total)} · ${formatMoney(
                      tax.taxAmount,
                      total,
                    )}`}
                  />
                ))}
              </div>
            )}
          </Section>

          <Section title={t("purchases.reception.xmlView.section.lines", "Líneas del XML")}>
            {data.lines.length === 0 ? (
              <p className="pur-xmlview-muted">
                {t(
                  "purchases.reception.xmlView.lines.empty",
                  "Sin líneas guardadas para este documento.",
                )}
              </p>
            ) : (
              <ZHDataTable
                columns={lineColumns}
                rows={data.lines}
                rowKey={(row) =>
                  `${row.mainCode ?? row.auxCode ?? "line"}-${row.description}-${row.quantity}-${row.unitPrice}`
                }
              />
            )}
          </Section>

          {data.rawXmlAvailable && data.rawXml ? (
            <details className="pur-xmlview-raw">
              <summary>
                {t("purchases.reception.xmlView.rawXml.summary", "XML crudo")}
              </summary>
              <pre className="pur-xmlview-raw-content">{data.rawXml}</pre>
            </details>
          ) : (
            <p className="pur-xmlview-muted">
              {t(
                "purchases.reception.xmlView.rawXml.unavailable",
                "XML no disponible para este documento.",
              )}
            </p>
          )}
        </div>
      )}
    </ZHModal>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="pf-card">
      <div className="pf-card__header">
        <h4 className="pf-card__title">{title}</h4>
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
