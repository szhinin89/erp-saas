import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, EmptyState, ErrorState } from "../../../components/PageShell";
import { ZhFileUpload } from "../../../components/zh/ZhFileUpload";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { ZHBtn } from "../../../components/zh/ZHForm";
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
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import {
  selectElectronicEmissionPaused,
  useElectronicInvoicingStatusStore,
} from "../../../store/electronicInvoicingStatusStore";
import { usePurchaseReceptionPage } from "../hooks/usePurchaseReceptionPage";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { CreateSupplierModal } from "../components/CreateSupplierModal";
import { PurchaseReceptionProcessCell } from "../components/PurchaseReceptionProcessCell";
import { PurchaseReceptionActionsCell } from "../components/PurchaseReceptionActionsCell";
import { PurchaseReceptionDocumentCell } from "../components/PurchaseReceptionDocumentCell";
import { PurchaseReceptionXmlViewModal } from "../components/PurchaseReceptionXmlViewModal";
import "../styles/purchase-reception.css";
import { receptionErpRecordView } from "../utils/receptionErpRecord";
import { PurchaseReceptionBatchActions } from "../components/PurchaseReceptionBatchActions";
import { PurchaseReceptionFilters } from "../components/PurchaseReceptionFilters";
import { receptionSupplierState } from "../utils/receptionFilters";

export function PurchaseReceptionPage() {
  const ctx = usePurchaseReceptionPage();
  // SRI-TOGGLE-07 — pausa de la emisión de la empresa: sin consultas de XML al SRI (registro manual sigue).
  const sriPaused = useElectronicInvoicingStatusStore(selectElectronicEmissionPaused);
  const { t } = useI18n();
  // PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3) — mismas claves que exige la API: escribir en
  // recepción (importar, XML, compra, NC) = purchases.create; preparar un gasto = expenses.documents.create.
  const { has } = usePermissionsUi();
  const canProcess = has("purchases.create");
  const canCreateExpense = has("expenses.documents.create");

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
          {receptionSupplierState(row) !== "notRegistered" ? (
            <Badge
              variant={receptionSupplierState(row) === "inactive" ? "warning" : "success"}
              label={
                receptionSupplierState(row) === "inactive"
                  ? t("purchases.reception.supplier.inactive", "Proveedor inactivo")
                  : t("purchases.reception.supplier.registered", "Proveedor registrado")
              }
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
      // PURCHASES-RECEPTION-FILTERS-01 — documento repetido con datos distintos: "En revisión"
      // (fuera de las tarjetas) sin ocultar el estado propio de cada fila para investigarlo.
      render: (row) => {
        const { badge } = receptionErpRecordView(row);
        const own = <Badge variant={badge.variant} label={t(badge.key, badge.fallback)} />;
        if (ctx.documentBucketOf(row) !== "review") return own;
        return (
          <>
            <Badge
              variant="warning"
              label={t("purchases.reception.erpRecord.inReview", "En revisión")}
              title={t(
                "purchases.reception.erpRecord.inReviewHint",
                "Comprobante repetido en el archivo con datos distintos: no se cuenta en las tarjetas hasta resolverlo.",
              )}
            />{" "}
            {own}
          </>
        );
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
          sriPaused={sriPaused}
          canProcess={canProcess}
          canCreateExpense={canCreateExpense}
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
          {canProcess ? (
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
          ) : (
            <ZHPageNotice
              variant="neutral"
              icon="lock"
              message={t(
                "purchases.reception.permissions.readOnly",
                "Su perfil solo permite consultar la recepción electrónica.",
              )}
              detail={t(
                "purchases.reception.permissions.readOnlyDetail",
                "Importar el TXT, consultar XML y registrar comprobantes requieren el permiso de crear compras.",
              )}
            />
          )}
        </div>

        {ctx.result && (
          <div className="pur-reception-kpis">
            <ReportKpiCard
              layout="horizontal"
              icon="check_circle"
              tone="success"
              onClick={() => ctx.toggleSummaryBucket("registered")}
              active={ctx.filters.erpSituation === "registered"}
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
              onClick={() => ctx.toggleSummaryBucket("toProcess")}
              active={ctx.filters.erpSituation === "toProcess"}
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
              onClick={() => ctx.toggleSummaryBucket("pendingSupplier")}
              active={ctx.filters.erpSituation === "pendingSupplier"}
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

      {ctx.result && ctx.summary.inReview > 0 && (
        <div className="pg-section">
          <ZHPageNotice
            variant="warning"
            message={t("purchases.reception.review.title", { count: ctx.summary.inReview })}
            detail={t(
              "purchases.reception.review.detail",
              "Aparecen repetidos en el archivo con datos distintos. Se muestran todas sus filas y no se cuentan en las tarjetas hasta resolverlo. Localícelos con Situación ERP → En revisión.",
            )}
          />
        </div>
      )}

      {ctx.result && ctx.normalization && (ctx.normalization.duplicatesRemoved > 0 ||
        ctx.normalization.invalidIdCount > 0) && (
        <div className="pg-section">
          <ZHPageNotice
            variant="warning"
            message={t("purchases.reception.normalization.title", "Comprobantes repetidos en el archivo")}
            detail={t("purchases.reception.normalization.detail", {
              removed: ctx.normalization.duplicatesRemoved,
              invalid: ctx.normalization.invalidIdCount,
            })}
          />
        </div>
      )}

      {sriPaused && ctx.result && (
        <ZHPageNotice
          variant="neutral"
          icon="pause_circle"
          message={t("purchases.reception.sriPaused.message", "Emisión electrónica pausada para esta empresa")}
          detail={t(
            "purchases.reception.sriPaused.detail",
            "No se consulta ni descarga XML en el SRI. Los comprobantes importados se conservan y pueden registrarse manualmente.",
          )}
        />
      )}
      {/* FASE 5 PURCHASE-UX — acciones del lote: fila compacta (ver PurchaseReceptionBatchActions). */}
      <PurchaseReceptionBatchActions
        sriPaused={sriPaused}
        canDownloadXml={canProcess}
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
            {/* PURCHASES-RECEPTION-FILTERS-01 — filtros combinables sobre el lote en memoria; los KPI
                superiores siguen sobre el total importado (pulsarlos filtra su partición). */}
            {ctx.result && (
              <PurchaseReceptionFilters
                filters={ctx.filters}
                setFilter={ctx.setFilter}
                advancedFilterCount={ctx.advancedFilterCount}
                supplierOptions={ctx.supplierOptions}
                selectedSupplierOptions={ctx.selectedSupplierOptions}
                setSupplierFilter={ctx.setSupplierFilter}
                dateSortOrder={ctx.dateSortOrder}
                setDateSortOrder={ctx.setDateSortOrder}
                clearFilters={ctx.clearFilters}
                shownCount={ctx.shownDocuments}
                totalCount={ctx.allDocuments}
                disabled={ctx.uploading}
              />
            )}
            <ZHDataTable
            columns={columns}
            rows={ctx.items}
            rowKey={(row) => ctx.rowKeys.get(row) ?? row.documentId}
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
