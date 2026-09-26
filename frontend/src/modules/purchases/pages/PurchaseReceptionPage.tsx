import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, EmptyState, ErrorState } from "../../../components/PageShell";
import { ZhFileUpload } from "../../../components/zh/ZhFileUpload";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZhSearchSelect, ZhSelect } from "../../../components/zh/inputs";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhBatchProgress } from "../../../components/zh/progress/ZhBatchProgress";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import {
  formatDate,
  formatDateTime,
} from "../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../i18n/i18n";
import {
  usePurchaseReceptionPage,
  type ReceptionDateSortOrder,
  type ReceptionSupplierOption,
} from "../hooks/usePurchaseReceptionPage";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { CreateSupplierModal } from "../components/CreateSupplierModal";
import { PurchaseReceptionProcessCell } from "../components/PurchaseReceptionProcessCell";
import { PurchaseReceptionActionsCell } from "../components/PurchaseReceptionActionsCell";
import { PurchaseReceptionDocumentCell } from "../components/PurchaseReceptionDocumentCell";
import { PurchaseReceptionXmlViewModal } from "../components/PurchaseReceptionXmlViewModal";
import "../styles/purchase-reception.css";

// Getters estables (module-level) para ZhSearchSelect: el índice de búsqueda local se memoiza por
// referencia, así 2.000+ proveedores del TXT se normalizan una sola vez por importación.
const RECEPTION_SUPPLIER_MAX_RESULTS = 30;
const getReceptionSupplierKey = (o: ReceptionSupplierOption) => o.ruc;
const getReceptionSupplierName = (o: ReceptionSupplierOption) => o.name;
const getReceptionSupplierSearchText = (o: ReceptionSupplierOption) => `${o.name} ${o.ruc}`;
const getReceptionSupplierChipLabel = (o: ReceptionSupplierOption) => `${o.name} — ${o.ruc}`;

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
          {(row.sourceDocType === "INVOICE"
            ? row.supplierExists && !!row.supplierId
            : row.supplierExists) ? (
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
                <ZHMoneyValue precision="money" value={row.subtotal} />
              </span>
            </p>
            <p className="pur-values-line">
              <span className="pur-values-label">
                {t("purchases.reception.values.vat", "IVA")}
              </span>
              <span>
                <ZHMoneyValue precision="tax" value={row.vatAmount} />
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
                  <ZHMoneyValue precision="money" value={row.total} />
                </span>
              </p>
            ) : (
              <p className="pur-values-total">
                <ZHMoneyValue precision="money" value={row.total} />
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
          onProcessCreditNote={ctx.processCreditNote}
          resolvingCreditNoteId={ctx.resolvingCreditNoteId}
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
          <ZHPageNotice
            variant="neutral"
            message={t(
              "purchases.reception.skippedUnsupported.title",
              "Comprobantes omitidos",
            )}
            detail={`${ctx.summary.skipped} ${t(
              "purchases.reception.skippedUnsupported.detail",
              "no se cargaron porque todavía no están soportados en esta fase.",
            )}`}
          />
        </div>
      )}

      {ctx.result && (
        <div className="pg-section pur-reception-batch-xml">
          <ZHBtn
            variant="secondary"
            size="xs"
            type="button"
            disabled={ctx.batchXmlRunning}
            onClick={ctx.handleDownloadPendingXml}
          >
            {t(
              "purchases.reception.actions.downloadPendingXml",
              "Descargar XML pendientes",
            )}
          </ZHBtn>
          {ctx.batchXmlProgress && (
            <ZhBatchProgress
              title={t(
                "purchases.reception.batchXml.title",
                "Descarga de XML pendientes",
              )}
              status={ctx.batchXmlProgress.status}
              total={ctx.batchXmlProgress.total}
              processed={ctx.batchXmlProgress.processed}
              succeeded={ctx.batchXmlProgress.downloaded}
              skipped={ctx.batchXmlProgress.skipped}
              failed={ctx.batchXmlProgress.failed}
            />
          )}
        </div>
      )}

      <div className="pg-section">
        {ctx.result === null && !ctx.uploading ? (
          <EmptyState message="Importe un archivo TXT para ver los comprobantes recibidos." />
        ) : (
          <>
            {/* ZH-PURCHASES-RECEPTION-FILTER-SORT-01 / ZH-SUPPLIER-SEARCH-REUSABLE-01 — filtro
                multi-proveedor con el buscador genérico del DS sobre un datasource LOCAL (RUC/nombre
                del TXT, incluye proveedores no registrados; no se usa SupplierSearchSelect porque
                ese busca solo BP registrados). Máx. 30 coincidencias visibles, chips removibles.
                Solo afectan la tabla; los KPI superiores siguen sobre el total importado. */}
            {ctx.result && (
              <ZHFilterBar
                onClear={ctx.clearFilters}
                clearLabel={t("purchases.reception.filters.clear", "Limpiar filtros")}
                disabled={ctx.uploading}
              >
                <div className="zh-filterbar__field zh-filterbar__field--grow">
                  <ZHField
                    label={t("purchases.reception.filters.supplier", "Proveedor")}
                    density="compact"
                  >
                    <ZhSearchSelect
                      mode="multiple"
                      options={ctx.supplierOptions}
                      value={ctx.selectedSupplierOptions}
                      onChange={(selected) =>
                        ctx.setSupplierFilter(selected.map((o) => o.ruc))
                      }
                      getOptionKey={getReceptionSupplierKey}
                      getOptionLabel={getReceptionSupplierName}
                      getOptionDescription={getReceptionSupplierKey}
                      getOptionSearchText={getReceptionSupplierSearchText}
                      getChipLabel={getReceptionSupplierChipLabel}
                      maxResults={RECEPTION_SUPPLIER_MAX_RESULTS}
                      disabled={ctx.uploading}
                      aria-label={t("purchases.reception.filters.supplier", "Proveedor")}
                      placeholder={t(
                        "purchases.reception.filters.supplierSearch",
                        "Buscar proveedor por nombre o RUC...",
                      )}
                      emptyText={t(
                        "purchases.reception.filters.supplierEmpty",
                        "Ningún proveedor del archivo coincide",
                      )}
                      truncatedText={t(
                        "supplierSearch.truncated",
                        "Siga escribiendo para refinar la búsqueda.",
                      )}
                      clearLabel={t("supplierSearch.clear", "Limpiar selección")}
                      removeLabel={t(
                        "purchases.reception.filters.removeSupplier",
                        "Quitar proveedor",
                      )}
                    />
                  </ZHField>
                </div>
                <div className="zh-filterbar__field">
                  <ZHField
                    label={t("purchases.reception.filters.dateSort", "Orden por emisión")}
                    density="compact"
                  >
                    <ZhSelect
                      value={ctx.dateSortOrder}
                      disabled={ctx.uploading}
                      onChange={(e) =>
                        ctx.setDateSortOrder(e.target.value as ReceptionDateSortOrder)
                      }
                    >
                      <option value="desc">
                        {t("purchases.reception.filters.sortDesc", "Más reciente primero")}
                      </option>
                      <option value="asc">
                        {t("purchases.reception.filters.sortAsc", "Más antiguo primero")}
                      </option>
                    </ZhSelect>
                  </ZHField>
                </div>
              </ZHFilterBar>
            )}
            <ZHDataTable
            columns={columns}
            rows={ctx.items}
            rowKey={(row) => row.documentId}
            loading={ctx.uploading}
            showRowNumber
            rowNumberOffset={(ctx.page - 1) * ctx.pageSize}
            emptyMessage="El archivo no contiene facturas para comparar."
            page={ctx.page}
            pageSize={ctx.pageSize}
            total={ctx.total}
            onPageChange={ctx.setPage}
          />
          </>
        )}
      </div>

      <CreateSupplierModal
        open={ctx.newSupplierRow !== null}
        supplierRuc={ctx.newSupplierRow?.ruc ?? ""}
        supplierName={ctx.newSupplierRow?.name ?? ""}
        supplierTradeName={ctx.newSupplierRow?.tradeName ?? null}
        onClose={ctx.closeCreateSupplier}
        onCreated={(supplierId) =>
          ctx.newSupplierRow &&
          ctx.handleSupplierCreated(ctx.newSupplierRow.ruc, supplierId)
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
