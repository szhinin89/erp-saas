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
import { ZHSectionHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
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
import { receptionErpRecordView } from "../utils/receptionErpRecord";
import { PurchaseReceptionBatchActions } from "../components/PurchaseReceptionBatchActions";

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
                // Comprobante), nunca crear el proveedor desde la fila de la NC.
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
      header: t("purchases.reception.table.document", "Comprobante"),
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
      key: "erpRecord",
      header: t("purchases.reception.table.erpRecord", "Registro ERP"),
      align: "center",
      // FASE 5 RECEPTION-UX — registro ERP real del comprobante (Compra/Gasto/NC). La referencia a
      // la factura afectada de una NC vive solo en la columna "Comprobante".
      render: (row) => {
        const { badge } = receptionErpRecordView(row);
        return <Badge variant={badge.variant} label={t(badge.key, badge.fallback)} />;
      },
    },
    {
      key: "process",
      header: t("purchases.reception.table.process", "Recepción y XML"),
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
      title={t("purchases.reception.title", "Documentos electrónicos de proveedores")}
      subtitle={t(
        "purchases.reception.subtitle",
        "Importe y gestione los comprobantes recibidos del SRI.",
      )}
      action={<ZHSectionHelp helpKey={HELP_KEYS.PURCHASES_RECEPTION_OVERVIEW} />}
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
            selectLabel={t(
              "purchases.reception.upload.select",
              "Seleccione el TXT de comprobantes recibidos",
            )}
            dropLabel={t("purchases.reception.upload.drop", "o arrástrelo aquí")}
            uploadingLabel={t("purchases.reception.upload.uploading", "Analizando archivo...")}
            noFileLabel={t(
              "purchases.reception.upload.noFile",
              "Aún no se ha importado ningún archivo.",
            )}
          />
        </div>

        {ctx.result && (
          <div className="pur-reception-kpis">
            <ReportKpiCard
              layout="horizontal"
              icon="check_circle"
              tone="success"
              label={t(
                "purchases.reception.summary.registered.title",
                "Registrados en ERP",
              )}
              value={String(ctx.summary.registered)}
              valueTone="success"
              sub={
                <p className="subtle">
                  {t("purchases.reception.summary.registered.breakdown", {
                    purchases: ctx.summary.purchases,
                    expenses: ctx.summary.expenses,
                    creditNotes: ctx.summary.creditNotes,
                  })}
                </p>
              }
            />
            <ReportKpiCard
              layout="horizontal"
              icon="hourglass_empty"
              tone="neutral"
              label={t(
                "purchases.reception.summary.toProcess.title",
                "Por procesar",
              )}
              value={String(ctx.summary.toProcess)}
              sub={
                <p className="subtle">
                  {t(
                    "purchases.reception.summary.toProcess.description",
                    "Defina cómo procesar cada comprobante",
                  )}
                </p>
              }
            />
            <ReportKpiCard
              layout="horizontal"
              icon="person_add"
              tone="warning"
              label={t(
                "purchases.reception.summary.pendingSupplier.title",
                "Pendientes de proveedor",
              )}
              value={String(ctx.summary.pendingSupplier)}
              sub={
                <p className="subtle">
                  {t("purchases.reception.summary.pendingSupplier.description", {
                    count: ctx.summary.pendingSupplier,
                  })}
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

      {/* FASE 5 PURCHASE-UX — acciones del lote: fila compacta (ver PurchaseReceptionBatchActions). */}
      <PurchaseReceptionBatchActions
        importedCount={ctx.result ? ctx.result.items.length : null}
        uploading={ctx.uploading}
        downloadingXml={ctx.batchXmlRunning}
        refreshing={ctx.refreshingStatuses}
        onRefreshStatuses={() => void ctx.handleRefreshStatuses()}
        onDownloadPendingXml={() => void ctx.handleDownloadPendingXml()}
        progress={
          ctx.batchXmlProgress && (
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
          )
        }
      />

      <div className="pg-section">
        {ctx.result === null && !ctx.uploading ? (
          <EmptyState
            message={t(
              "purchases.reception.emptyBeforeImport",
              "Importe un archivo TXT para ver los comprobantes recibidos.",
            )}
          />
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
            emptyMessage={t(
              "purchases.reception.emptyTable",
              "No hay comprobantes para mostrar.",
            )}
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
