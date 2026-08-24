import { useNavigate } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZhFileUpload } from "../../../components/zh/ZhFileUpload";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHModal } from "../../../components/zh/ZHModal";
import { Badge } from "../../../components/PageShell";
import { useInitialLoadCustomersPage } from "./useInitialLoadCustomersPage";
import type { ImportBatchRowPreviewDto } from "../types/importBatch.types";

const SEVERITY_FILTERS: { key: "all" | "errors" | "warnings"; label: string }[] = [
  { key: "all", label: "Todas" },
  { key: "errors", label: "Con error" },
  { key: "warnings", label: "Sin error" },
];

export function InitialLoadCustomersPage() {
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
    downloadTemplate,
    handleFileSelected,
    loadPreview,
    changeSeverityFilter,
    setConfirmModalOpen,
    confirmBatch,
    reset,
  } = useInitialLoadCustomersPage();

  const columns: ZHDataTableColumn<ImportBatchRowPreviewDto>[] = [
    { key: "row", header: "Fila", render: (r) => r.rowNumber },
    {
      key: "identification",
      header: "Identificación",
      render: (r) => r.rawData["Número Identificación"] ?? "—",
    },
    {
      key: "legalName",
      header: "Razón Social",
      render: (r) => r.rawData["Razón Social"] ?? "—",
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
      render: (r) =>
        r.issues.length === 0
          ? "—"
          : r.issues.map((i) => i.message).join(" · "),
    },
  ];

  return (
    <ErpPageTemplate
      kicker="Configuración / Implementación"
      title="Carga Inicial — Clientes"
      subtitle="Importa clientes desde una plantilla Excel: subir, validar, revisar y confirmar."
      action={
        <ZHBtn variant="ghost" onClick={() => navigate("/initial-load")}>
          Volver
        </ZHBtn>
      }
    >
      {error && <ZHPageNotice variant="error" message="No se pudo completar la operación." detail={error} />}

      {step === "done" && confirmResult ? (
        <ZHCard title="Importación confirmada">
          <ZHPageNotice
            variant={confirmResult.failedRows > 0 ? "warning" : "success"}
            message={
              confirmResult.failedRows > 0
                ? `Se importaron ${confirmResult.importedRows} clientes. ${confirmResult.failedRows} fila(s) quedaron con error.`
                : `Se importaron ${confirmResult.importedRows} clientes correctamente.`
            }
          />
          <div className="zh-form-actions-row">
            <ZHBtn onClick={() => navigate("/masterdata/customers")}>
              Ver clientes
            </ZHBtn>
            <ZHBtn variant="ghost" onClick={reset}>
              Nueva importación
            </ZHBtn>
          </div>
        </ZHCard>
      ) : (
        <>
          <ZHCard title="1. Plantilla y archivo">
            <p>
              Descarga la plantilla, complétala con tus clientes y súbela aquí. Los campos
              obligatorios son Tipo/Número de Identificación y Razón Social.
            </p>
            <div className="zh-form-actions-row">
              <ZHBtn variant="secondary" onClick={downloadTemplate}>
                Descargar plantilla
              </ZHBtn>
            </div>
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
                rows={preview?.items ?? []}
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
            ? `Se importarán ${batch.validRows} clientes válidos. ${batch.issueRows} fila(s) con error serán omitidas.`
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
        Esta acción crea los clientes válidos en el sistema. No se puede deshacer.
      </ZHModal>
    </ErpPageTemplate>
  );
}
