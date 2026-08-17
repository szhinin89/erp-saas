import { ZHModal } from "../../../components/zh/ZHModal";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { useI18n } from "../../../i18n/i18n";
import type {
  PurchaseReceptionXmlView,
  PurchaseReceptionXmlViewLine,
  PurchaseReceptionXmlViewLineTax,
  PurchaseReceptionXmlViewTaxSummary,
} from "../api/purchaseReceptionService";

type Props = {
  open: boolean;
  loading: boolean;
  error: string | null;
  data: PurchaseReceptionXmlView | null;
  onClose: () => void;
};

type TotalRow = {
  key: string;
  label: string;
  value: number;
  emphasize?: boolean;
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

  const totalRows: TotalRow[] = data
    ? [
        {
          key: "subtotal",
          label: t(
            "purchases.reception.xmlView.field.subtotal",
            "Subtotal sin impuestos",
          ),
          value: data.subtotal,
        },
        {
          key: "discount",
          label: t("purchases.reception.xmlView.field.discountAmount", "Descuento"),
          value: data.discountAmount,
        },
        {
          key: "ice",
          label: t("purchases.reception.xmlView.field.iceAmount", "ICE"),
          value: data.iceAmount,
        },
        {
          key: "irbpnr",
          label: t("purchases.reception.xmlView.field.irbpnrAmount", "IRBPNR"),
          value: data.irbpnrAmount,
        },
        {
          key: "vat",
          label: t("purchases.reception.xmlView.field.vatAmount", "IVA"),
          value: data.vatAmount,
        },
        {
          key: "tip",
          label: t("purchases.reception.xmlView.field.tipAmount", "Propina"),
          value: data.tipAmount,
        },
        {
          key: "xmlTotal",
          label: t(
            "purchases.reception.xmlView.field.totalAmount",
            "Importe total XML",
          ),
          value: data.totalAmount,
          emphasize: true,
        },
        {
          key: "lineTotal",
          label: t(
            "purchases.reception.xmlView.field.lineCalculatedTotal",
            "Suma líneas",
          ),
          value: data.lineCalculatedTotal,
        },
        {
          key: "rounding",
          label: t(
            "purchases.reception.xmlView.field.roundingDifference",
            "Diferencia/redondeo",
          ),
          value: data.roundingDifference,
          emphasize: data.roundingDifference !== 0,
        },
      ]
    : [];

  const totalsColumns: ZHDataTableColumn<TotalRow>[] = [
    {
      key: "label",
      header: t("purchases.reception.xmlView.totals.concept", "Concepto"),
      render: (row) => (
        <span className={row.emphasize ? "pur-xmlview-strong" : undefined}>
          {row.label}
        </span>
      ),
    },
    {
      key: "value",
      header: t("purchases.reception.xmlView.totals.value", "Valor"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue
          value={row.value}
          decimals={total}
          align="end"
          emphasis={row.emphasize ? "strong" : "default"}
        />
      ),
    },
  ];

  const taxColumns: ZHDataTableColumn<PurchaseReceptionXmlViewTaxSummary>[] = [
    {
      key: "name",
      header: t("purchases.reception.xmlView.taxes.name", "Impuesto"),
      render: (row) => row.taxName,
    },
    {
      key: "code",
      header: t("purchases.reception.xmlView.taxes.code", "Código"),
      render: (row) => `${row.taxCode}/${row.taxRateCode}`,
    },
    {
      key: "rate",
      header: t("purchases.reception.xmlView.taxes.rate", "Tarifa"),
      align: "right",
      render: (row) =>
        row.rate == null ? "—" : formatTaxRate(row.rate, pct),
    },
    {
      key: "base",
      header: t("purchases.reception.xmlView.taxes.base", "Base"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.taxableBase} decimals={total} align="end" />
      ),
    },
    {
      key: "amount",
      header: t("purchases.reception.xmlView.taxes.amount", "Valor"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.amount} decimals={total} align="end" />
      ),
    },
  ];

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
      render: (row) => (
        <ZHMoneyValue value={row.unitPrice} decimals={cost} align="end" />
      ),
    },
    {
      key: "discountAmount",
      header: t("purchases.reception.xmlView.lines.discount", "Descuento"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.discountAmount} decimals={total} align="end" />
      ),
    },
    {
      key: "taxableBase",
      header: t("purchases.reception.xmlView.lines.taxableBase", "Base imponible"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.taxableBase} decimals={total} align="end" />
      ),
    },
    {
      key: "vatAmount",
      header: t("purchases.reception.xmlView.lines.vat", "IVA"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.vatAmount} decimals={total} align="end" />
      ),
    },
    {
      key: "iceAmount",
      header: t("purchases.reception.xmlView.lines.ice", "ICE"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.iceAmount} decimals={total} align="end" />
      ),
    },
    {
      key: "irbpnrAmount",
      header: t("purchases.reception.xmlView.lines.irbpnr", "IRBPNR"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.irbpnrAmount} decimals={total} align="end" />
      ),
    },
    {
      key: "totalAmount",
      header: t("purchases.reception.xmlView.lines.total", "Total línea"),
      align: "right",
      render: (row) => (
        <ZHMoneyValue value={row.lineTotal} decimals={total} align="end" />
      ),
    },
    {
      key: "detail",
      header: t("purchases.reception.xmlView.lines.detail", "Detalle"),
      render: (row) => (
        <LineDetail
          row={row}
          totalDecimals={total}
          pctDecimals={pct}
          emptyText={t(
            "purchases.reception.xmlView.lines.detail.empty",
            "Sin detalle adicional.",
          )}
          summaryText={t(
            "purchases.reception.xmlView.lines.detail.summary",
            "Ver detalle",
          )}
        />
      ),
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

          <Section
            title={t(
              "purchases.reception.xmlView.section.header",
              "Cabecera del comprobante",
            )}
          >
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
              <Field
                label={t("purchases.reception.xmlView.field.referralGuide", "Guía")}
                value={data.referralGuide ?? "—"}
                mono
              />
              <Field
                label={t("purchases.reception.xmlView.field.payment", "Pago/plazo")}
                value={formatPayment(data)}
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

          <Section
            title={t("purchases.reception.xmlView.section.totals", "Totales XML")}
          >
            <ZHDataTable
              columns={totalsColumns}
              rows={totalRows}
              rowKey={(row) => row.key}
            />
          </Section>

          <Section
            title={t(
              "purchases.reception.xmlView.section.taxes",
              "Resumen de impuestos",
            )}
          >
            <ZHDataTable
              columns={taxColumns}
              rows={data.taxSummaries}
              rowKey={(row) => `${row.taxCode}-${row.taxRateCode}`}
              emptyMessage={t(
                "purchases.reception.xmlView.taxes.empty",
                "Sin desglose de impuestos disponible.",
              )}
            />
          </Section>

          <Section title={t("purchases.reception.xmlView.section.lines", "Líneas del XML")}>
            <ZHDataTable
              columns={lineColumns}
              rows={data.lines}
              rowKey={(row) =>
                `${row.mainCode ?? row.auxCode ?? "line"}-${row.description}-${row.quantity}-${row.unitPrice}`
              }
              emptyMessage={t(
                "purchases.reception.xmlView.lines.empty",
                "Sin líneas guardadas para este documento.",
              )}
            />
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

function LineDetail({
  row,
  totalDecimals,
  pctDecimals,
  emptyText,
  summaryText,
}: {
  row: PurchaseReceptionXmlViewLine;
  totalDecimals: number;
  pctDecimals: number;
  emptyText: string;
  summaryText: string;
}) {
  const hasTaxes = row.taxes.length > 0;
  const hasDetails = row.additionalDetails.length > 0;

  return (
    <details className="pur-xmlview-line-detail">
      <summary>{summaryText}</summary>
      {!hasTaxes && !hasDetails && <p className="pur-xmlview-muted">{emptyText}</p>}
      {hasTaxes && (
        <dl className="pur-xmlview-line-detail__list">
          {row.taxes.map((tax) => (
            <LineTax
              key={`${tax.taxCode}-${tax.taxRateCode}-${tax.amount}`}
              tax={tax}
              totalDecimals={totalDecimals}
              pctDecimals={pctDecimals}
            />
          ))}
        </dl>
      )}
      {hasDetails && (
        <dl className="pur-xmlview-line-detail__list">
          {row.additionalDetails.map((detail) => (
            <div key={`${detail.name}-${detail.value}`}>
              <dt>{detail.name || "—"}</dt>
              <dd>{detail.value || "—"}</dd>
            </div>
          ))}
        </dl>
      )}
    </details>
  );
}

function LineTax({
  tax,
  totalDecimals,
  pctDecimals,
}: {
  tax: PurchaseReceptionXmlViewLineTax;
  totalDecimals: number;
  pctDecimals: number;
}) {
  return (
    <div>
      <dt>
        {tax.taxName} {tax.taxCode}/{tax.taxRateCode}
      </dt>
      <dd>
        {formatTaxRate(tax.rate, pctDecimals)} ·{" "}
        {formatMoneyWithSymbol(tax.taxableBase, totalDecimals)} ·{" "}
        {formatMoneyWithSymbol(tax.amount, totalDecimals)}
      </dd>
    </div>
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

function formatPayment(data: PurchaseReceptionXmlView) {
  const method = data.paymentMethodCode ?? "—";
  if (!data.paymentTerm) return method;
  return `${method} · ${data.paymentTerm}${data.paymentTimeUnit ? ` ${data.paymentTimeUnit}` : ""}`;
}

function formatTaxRate(rate: number, decimals: number) {
  return `${formatMoney(rate, decimals)}%`;
}
