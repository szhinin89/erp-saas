import { useNavigate } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZhFileUpload } from "../../../components/zh/ZhFileUpload";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHModal } from "../../../components/zh/ZHModal";
import { Badge } from "../../../components/PageShell";
import { useImportWizard } from "./useImportWizard";
import type { ImportBatchRowPreviewDto, ImportType } from "../types/importBatch.types";
import "./initial-load.css";

const SEVERITY_FILTERS: { key: "all" | "errors" | "warnings"; label: string }[] = [
  { key: "all", label: "Todas" },
  { key: "errors", label: "Con error" },
  { key: "warnings", label: "Importables con advertencia" },
];

function buildColumns(
  primaryColumnKey: string,
  primaryColumnLabel: string,
  secondaryColumnKey: string,
  secondaryColumnLabel: string,
): ZHDataTableColumn<ImportBatchRowPreviewDto>[] {
  return [
    { key: "row", header: "Fila", render: (r) => r.rowNumber },
    {
      key: "primary",
      header: primaryColumnLabel,
      render: (r) => r.rawData[primaryColumnKey] ?? "—",
    },
    {
      key: "secondary",
      header: secondaryColumnLabel,
      render: (r) => r.rawData[secondaryColumnKey] ?? "—",
    },
    {
      key: "status",
      header: "Estado",
      render: (r) =>
        r.isImported ? (
          <Badge label="Importado" variant="success" />
        ) : r.hasBlockingIssue ? (
          <Badge label="Error" variant="error" />
        ) : (
          <Badge label="Válido" variant="neutral" />
        ),
    },
    {
      key: "issues",
      header: "Detalle",
      render: (r) => (r.issues.length === 0 ? "—" : r.issues.map((i) => i.message).join(" · ")),
    },
  ];
}

interface ImportWizardPageProps {
  importType: ImportType;
  templateFileName: string;
  title: string;
  helpText: string;
  requiredFieldsHint: string;
  resultRoute: string;
  resultRouteLabel: string;
  resultEntityLabelPlural: string;
  /** Columna cruda de "identificación"/código a mostrar en el preview — clave exacta de la plantilla. Default: Clientes/Proveedores. */
  primaryColumnKey?: string;
  primaryColumnLabel?: string;
  /** Columna cruda de "nombre" a mostrar en el preview — clave exacta de la plantilla. Default: Clientes/Proveedores. */
  secondaryColumnKey?: string;
  secondaryColumnLabel?: string;
  /** Solo Catálogo de Productos: permite crear Categoría/Marca automáticamente si no existen. */
  showAutoCreateCatalogOption?: boolean;
}

/**
 * Wizard genérico de Carga Inicial — upload → validar → preview → confirmar → resultado
 * (INITIAL-LOAD-ARCH-01, extraído a componente reusable en INITIAL-LOAD-SUPPLIERS-01, columnas de
 * preview parametrizadas en INITIAL-LOAD-ITEMS-01 porque Ítems no tiene identificación/razón
 * social). El único contenido específico de cada import type son los textos, la ruta de
 * resultado y qué dos columnas crudas mostrar en el preview.
 */
export function ImportWizardPage({
  importType,
  templateFileName,
  title,
  helpText,
  requiredFieldsHint,
  resultRoute,
  resultRouteLabel,
  resultEntityLabelPlural,
  primaryColumnKey = "Número Identificación",
  primaryColumnLabel = "Identificación",
  secondaryColumnKey = "Razón Social",
  secondaryColumnLabel = "Razón Social",
  showAutoCreateCatalogOption = false,
}: ImportWizardPageProps) {
  const columns = buildColumns(
    primaryColumnKey,
    primaryColumnLabel,
    secondaryColumnKey,
    secondaryColumnLabel,
  );
  const navigate = useNavigate();
  const {
    batch,
    step,
    uploadProgress,
    error,
    preview,
    previewPage,
    previewLoading,
    severityFilter,
    confirmModalOpen,
    confirmResult,
    autoCreateCatalogValues,
    setAutoCreateCatalogValues,
    downloadTemplate,
    handleFileSelected,
    loadPreview,
    changeSeverityFilter,
    setConfirmModalOpen,
    confirmBatch,
    reset,
  } = useImportWizard(importType, templateFileName);
  const previewRows = preview?.items ?? [];
  const previewIssues = previewRows.flatMap((row) => row.issues);
  const categoryCreationWarnings = previewIssues.filter(
    (issue) => issue.code === "CATEGORY_WILL_BE_CREATED",
  ).length;
  const brandCreationWarnings = previewIssues.filter(
    (issue) => issue.code === "BRAND_WILL_BE_CREATED",
  ).length;
  const hasCatalogCreationWarnings =
    batch?.autoCreateCatalogValues === true &&
    (categoryCreationWarnings > 0 || brandCreationWarnings > 0);
  const catalogCreationWarningCount = categoryCreationWarnings + brandCreationWarnings;
  const catalogCreationDetail = [
    categoryCreationWarnings > 0
      ? `${categoryCreationWarnings} ${
          categoryCreationWarnings === 1 ? "categoría" : "categorías"
        }`
      : null,
    brandCreationWarnings > 0
      ? `${brandCreationWarnings} ${brandCreationWarnings === 1 ? "marca" : "marcas"}`
      : null,
  ]
    .filter(Boolean)
    .join(" y ");
  const catalogCreationNoticeDetail =
    catalogCreationWarningCount === 1
      ? `El preview muestra ${catalogCreationDetail} pendiente de creación. Se creará automáticamente porque AutoCreateCatalogValues está activo.`
      : `El preview muestra ${catalogCreationDetail} pendientes de creación. Se crearán automáticamente porque AutoCreateCatalogValues está activo.`;

  return (
    <ErpPageTemplate
      kicker="Configuración / Implementación"
      title={title}
      subtitle="Subir, validar, revisar y confirmar desde una plantilla Excel."
      action={
        <ZHBtn variant="ghost" onClick={() => navigate("/initial-load")}>
          Volver
        </ZHBtn>
      }
    >
      {error && (
        <ZHPageNotice
          variant="error"
          message="No se pudo completar la operación."
          detail={error}
        />
      )}

      {step === "done" && confirmResult ? (
        <ZHCard title="Importación confirmada">
          <ZHPageNotice
            variant={confirmResult.failedRows > 0 ? "warning" : "success"}
            message={
              confirmResult.failedRows > 0
                ? `Se importaron ${confirmResult.importedRows} ${resultEntityLabelPlural}. ${confirmResult.failedRows} fila(s) quedaron con error.`
                : `Se importaron ${confirmResult.importedRows} ${resultEntityLabelPlural} correctamente.`
            }
          />
          <div className="zh-form-actions-row">
            <ZHBtn onClick={() => navigate(resultRoute)}>{resultRouteLabel}</ZHBtn>
            <ZHBtn variant="ghost" onClick={reset}>
              Nueva importación
            </ZHBtn>
          </div>
        </ZHCard>
      ) : (
        <>
          <ZHCard title="1. Plantilla y archivo">
            <p>
              {helpText} {requiredFieldsHint}
            </p>
            <div className="zh-form-actions-row">
              <ZHBtn variant="secondary" onClick={downloadTemplate}>
                Descargar plantilla
              </ZHBtn>
            </div>
            {showAutoCreateCatalogOption && (
              <label className="il-autocreate-option">
                <input
                  type="checkbox"
                  checked={autoCreateCatalogValues}
                  disabled={step !== "idle"}
                  onChange={(e) => setAutoCreateCatalogValues(e.target.checked)}
                />{" "}
                Crear categorías/marcas nuevas si no existen en el catálogo
              </label>
            )}
            <ZhFileUpload
              accept=".xlsx"
              onFileSelected={handleFileSelected}
              disabled={step !== "idle"}
              uploading={step === "uploading" || step === "validating"}
              progress={uploadProgress}
              selectLabel="Seleccionar archivo Excel"
              dropLabel="O arrastra el archivo aquí"
              uploadingLabel={
                step === "uploading" ? "Subiendo archivo…" : "Validando filas…"
              }
              noFileLabel="Ningún archivo subido todavía."
            />
          </ZHCard>

          {batch && (batch.status === "Validated" || batch.status === "PartiallyCompleted") && (
            <ZHCard title="2. Resultado de la validación">
              <div className="zh-form-actions-row">
                <Badge label={`${batch.validRows} válidas`} variant="success" />
                <Badge label={`${batch.issueRows} con error`} variant="error" />
                <Badge label={`${batch.warningRows} con advertencia`} variant="warning" />
                <Badge label={`${batch.totalRows} total`} variant="neutral" />
              </div>
            </ZHCard>
          )}

          {batch && step === "validated" && (
            <ZHCard title="3. Vista previa">
              <div className="zh-form-actions-row">
                {SEVERITY_FILTERS.map((f) => (
                  <ZHBtn
                    key={f.key}
                    size="sm"
                    variant={severityFilter === f.key ? "primary" : "ghost"}
                    onClick={() => changeSeverityFilter(f.key)}
                  >
                    {f.label}
                  </ZHBtn>
                ))}
              </div>
              <ZHDataTable
                columns={columns}
                rows={previewRows}
                rowKey={(r) => r.id}
                loading={previewLoading}
                emptyMessage="No hay filas para este filtro."
                page={previewPage}
                pageSize={25}
                total={preview?.totalCount}
                onPageChange={(page) => loadPreview(page, severityFilter)}
              />
              <div className="zh-form-actions-row">
                <ZHBtn
                  disabled={batch.validRows === 0}
                  onClick={() => setConfirmModalOpen(true)}
                >
                  Confirmar importación
                </ZHBtn>
              </div>
            </ZHCard>
          )}

          {step === "confirming" && (
            <ZHPageNotice variant="info" message="Confirmando importación…" />
          )}
        </>
      )}

      <ZHModal
        open={confirmModalOpen}
        onClose={() => setConfirmModalOpen(false)}
        title="Confirmar importación"
        subtitle={
          batch
            ? `Se importarán ${batch.validRows} ${resultEntityLabelPlural} válidos. ${batch.issueRows} fila(s) con error serán omitidas.`
            : undefined
        }
        footer={
          <>
            <ZHBtn variant="ghost" onClick={() => setConfirmModalOpen(false)}>
              Cancelar
            </ZHBtn>
            <ZHBtn onClick={confirmBatch}>Confirmar</ZHBtn>
          </>
        }
      >
        <div className="il-confirm-summary">
          <p className="zh-form-help il-confirm-summary__intro">
            Esta acción crea los registros válidos en el sistema. No se puede deshacer.
          </p>
          {batch && (
            <>
              <div className="zh-form-actions-row">
                <Badge label={`${batch.validRows} filas válidas`} variant="success" />
                <Badge
                  label={`${batch.warningRows} con advertencias`}
                  variant={batch.warningRows > 0 ? "warning" : "neutral"}
                />
                <Badge
                  label={`${batch.issueRows} con errores`}
                  variant={batch.issueRows > 0 ? "error" : "neutral"}
                />
              </div>
              {hasCatalogCreationWarnings && (
                <ZHPageNotice
                  variant="warning"
                  message="Se crearán valores de catálogo al confirmar."
                  detail={catalogCreationNoticeDetail}
                />
              )}
            </>
          )}
        </div>
      </ZHModal>
    </ErpPageTemplate>
  );
}
