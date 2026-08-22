import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, EmptyState, ErrorState } from "../../../components/PageShell";
import { ZhFileUpload } from "../../../components/zh/ZhFileUpload";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import {
  formatDate,
  formatDateTime,
} from "../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../i18n/i18n";
import { usePurchaseReceptionPage } from "../hooks/usePurchaseReceptionPage";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { CreateSupplierModal } from "../components/CreateSupplierModal";
import { PurchaseReceptionProcessCell } from "../components/PurchaseReceptionProcessCell";
import { PurchaseReceptionActionsCell } from "../components/PurchaseReceptionActionsCell";
import { PurchaseReceptionDocumentCell } from "../components/PurchaseReceptionDocumentCell";
import { PurchaseReceptionXmlViewModal } from "../components/PurchaseReceptionXmlViewModal";
import "../styles/purchase-reception.css";

export function PurchaseReceptionPage() {
  const ctx = usePurchaseReceptionPage();
  const { t } = useI18n();

  const columns: ZHDataTableColumn<PurchaseReceptionItem>[] = [
    {
      key: "issueDate",
      header: t("purchases.reception.table.date", "Emisión"),
      render: (row) => formatDate(row.issueDate),
    },
    {
      key: "supplier",
      header: t("purchases.reception.table.supplier", "Proveedor"),
      render: (row) => (
        <div className="pur-supplier-cell">
          <p className="pur-supplier-name">{row.supplierName}</p>
          <p className="pur-supplier-ruc">{row.supplierRuc}</p>
          {row.supplierExists ? (
            <Badge
              variant={row.supplierIsActive === false ? "warning" : "success"}
              label={t(
                row.supplierIsActive === false
                  ? "purchases.reception.supplier.inactive"
                  : "purchases.reception.supplier.registered",
                row.supplierIsActive === false
                  ? "Proveedor inactivo"
                  : "Proveedor registrado",
              )}
            />
          ) : (
            <>
              <Badge
                variant="warning"
                label={t(
                  "purchases.reception.supplier.notRegistered",
                  "Proveedor no registrado",
                )}
              />
              {row.sourceDocType !== "CREDIT_NOTE" && (
                // Una NC afecta una compra existente — si el proveedor no está registrado, la
                // acción correcta es ubicar/ingresar primero la factura afectada (columna
                // Documento), nunca crear el proveedor desde la fila de la NC.
                <ZHBtn
                  variant="secondary"
                  size="xs"
                  type="button"
                  onClick={() =>
                    ctx.openCreateSupplier(
                      row.supplierRuc,
                      row.supplierName,
                      row.supplierTradeName,
                    )
                  }
                >
                  {t(
                    "purchases.reception.supplier.createButton",
                    "Crear proveedor",
                  )}
                </ZHBtn>
              )}
            </>
          )}
        </div>
      ),
    },
    {
      key: "document",
      header: t("purchases.reception.table.document", "Documento"),
      render: (row) => <PurchaseReceptionDocumentCell row={row} />,
    },
    {
      key: "values",
      header: t("purchases.reception.table.values", "Valores"),
      align: "right",
      render: (row) => {
        const isCreditNote = row.sourceDocType === "CREDIT_NOTE";
        return (
          <div className="pur-values-cell">
            <p className="pur-values-line">
              <span className="pur-values-label">
                {isCreditNote
                  ? t(
                      "purchases.reception.values.creditSubtotal",
                      "Subtotal crédito",
                    )
                  : t("purchases.reception.values.subtotal", "Subtotal")}
              </span>
              <span>
                <ZHMoneyValue value={row.subtotal} />
              </span>
            </p>
            <p className="pur-values-line">
              <span className="pur-values-label">
                {t("purchases.reception.values.vat", "IVA")}
              </span>
              <span>
                <ZHMoneyValue value={row.vatAmount} />
              </span>
            </p>
            {isCreditNote ? (
              <p className="pur-values-line pur-values-total">
                <span className="pur-values-label">
                  {t(
                    "purchases.reception.values.creditTotal",
                    "Total crédito",
                  )}
                </span>
                <span>
                  <ZHMoneyValue value={row.total} />
                </span>
              </p>
            ) : (
              <p className="pur-values-total">
                <ZHMoneyValue value={row.total} />
              </p>
            )}
          </div>
        );
      },
    },
    {
      key: "purchaseExists",
      header: t("purchases.reception.table.purchaseErp", "Documento"),
      align: "center",
      render: (row) =>
        row.sourceDocType === "CREDIT_NOTE" ? (
          <Badge
            variant={row.affectedPurchaseExists ? "success" : "warning"}
            label={
              row.affectedPurchaseExists
                ? t(
                    "purchases.reception.documentErp.affectedFound",
                    "Factura afectada encontrada",
                  )
                : t(
                    "purchases.reception.documentErp.affectedNotEntered",
                    "Factura afectada no ingresada",
                  )
            }
          />
        ) : (
          <Badge
            variant={row.purchaseExists ? "success" : "warning"}
            label={
              row.purchaseExists
                ? t(
                    "purchases.reception.actions.purchaseAlreadyEntered",
                    "Compra ya ingresada al sistema",
                  )
                : t(
                    "purchases.reception.purchaseErp.notEntered",
                    "Compra no ingresada",
                  )
            }
          />
        ),
    },
    {
      key: "process",
      header: t("purchases.reception.table.process", "Proceso"),
      align: "center",
      render: (row) => (
        <PurchaseReceptionProcessCell
          row={row}
          xmlState={ctx.xmlRowState[row.documentId]}
        />
      ),
    },
    {
      key: "actions",
      header: t("purchases.reception.table.actions", "Acciones"),
      align: "center",
      render: (row) => (
        <PurchaseReceptionActionsCell
          row={row}
          xmlState={ctx.xmlRowState[row.documentId]}
          onDownloadXml={(documentId) => void ctx.handleDownloadXml(documentId)}
          onViewXml={ctx.openXmlView}
        />
      ),
    },
  ];

  return (
    <ErpPageTemplate
      title={t("purchases.reception.title", "Recepción electrónica (TXT)")}
      subtitle={t(
        "purchases.reception.subtitle",
        "Importe el TXT de comprobantes recibidos del SRI. Cada factura queda guardada como documento de recepción y se compara contra los proveedores y compras ya registrados en el ERP. La vinculación de productos con el catálogo de Items se hace desde la pantalla de Compras al crear o abrir la compra.",
      )}
    >
      <div className="pg-section pur-reception-top">
        <div className="pur-reception-upload">
          <ZhFileUpload
            compact
            accept=".txt"
            onFileSelected={(file) => void ctx.handleFileSelected(file)}
            uploading={ctx.uploading}
            progress={ctx.progress}
            error={ctx.error}
            currentFile={
              ctx.fileName
                ? {
                    name: ctx.fileName,
                    sizeBytes: 0,
                    uploadedAt: formatDateTime(new Date().toISOString()),
                  }
                : null
            }
            selectLabel="Seleccione el archivo TXT de recepción"
            dropLabel="o arrástrelo aquí"
            uploadingLabel="Analizando archivo..."
            noFileLabel="Aún no se ha importado ningún archivo."
          />
        </div>

        {ctx.result && (
          <div className="pur-reception-kpis">
            <ReportKpiCard
              layout="horizontal"
              icon="check_circle"
              tone="success"
              label={t(
                "purchases.reception.summary.readyPurchases.title",
                "Compras listas",
              )}
              value={String(ctx.summary.imported)}
              valueTone="success"
              sub={
                <p className="subtle">
                  {t(
                    "purchases.reception.summary.readyPurchases.description",
                    "Compras ya ingresadas al sistema",
                  )}
                </p>
              }
            />
            <ReportKpiCard
              layout="horizontal"
              icon="hourglass_empty"
              tone="neutral"
              label={t(
                "purchases.reception.summary.toReview.title",
                "Por revisar",
              )}
              value={String(ctx.summary.pending)}
              sub={
                <p className="subtle">
                  {t(
                    "purchases.reception.summary.toReview.description",
                    "Falta registrar o ingresar compras",
                  )}
                </p>
              }
            />
            <ReportKpiCard
              layout="horizontal"
              icon="person_add"
              tone="warning"
              label={t(
                "purchases.reception.summary.suppliersToCreate.title",
                "Proveedores por crear",
              )}
              value={String(ctx.summary.newSupplier)}
              sub={
                <p className="subtle">
                  {t(
                    "purchases.reception.summary.suppliersToCreate.description",
                    "Debe crearlos para ingresar las compras",
                  )}
                </p>
              }
            />
          </div>
        )}
      </div>

      {ctx.error && !ctx.uploading && (
        <div className="pg-section">
          <ErrorState message={ctx.error} />
        </div>
      )}

      {ctx.result && ctx.summary.skipped > 0 && (
        <div className="pg-section">
          <Badge
            variant="neutral"
            label={`Omitidas (no soportadas en esta fase): ${ctx.summary.skipped}`}
          />
        </div>
      )}

      <div className="pg-section">
        {ctx.result === null && !ctx.uploading ? (
          <EmptyState message="Importe un archivo TXT para ver los comprobantes recibidos." />
        ) : (
          <ZHDataTable
            columns={columns}
            rows={ctx.items}
            rowKey={(row) => row.documentId}
            loading={ctx.uploading}
            emptyMessage="El archivo no contiene facturas para comparar."
            page={ctx.page}
            pageSize={ctx.pageSize}
            total={ctx.total}
            onPageChange={ctx.setPage}
          />
        )}
      </div>

      <CreateSupplierModal
        open={ctx.newSupplierRow !== null}
        supplierRuc={ctx.newSupplierRow?.ruc ?? ""}
        supplierName={ctx.newSupplierRow?.name ?? ""}
        supplierTradeName={ctx.newSupplierRow?.tradeName ?? null}
        onClose={ctx.closeCreateSupplier}
        onCreated={() =>
          ctx.newSupplierRow &&
          ctx.handleSupplierCreated(ctx.newSupplierRow.ruc)
        }
      />

      <PurchaseReceptionXmlViewModal
        open={ctx.xmlViewOpen}
        loading={ctx.xmlViewLoading}
        error={ctx.xmlViewError}
        data={ctx.xmlViewData}
        onClose={ctx.closeXmlView}
      />
    </ErpPageTemplate>
  );
}
