import { EmptyState, LoadingState, Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import type {
  DocumentSequenceRow,
  DocumentSequencesPageContext,
} from "../hooks/useDocumentSequencesPage";

type Props = Pick<
  DocumentSequencesPageContext,
  | "loading"
  | "error"
  | "emissionPoints"
  | "selectedEmissionPointId"
  | "setSelectedEmissionPointId"
  | "rows"
  | "canManage"
  | "openConfigure"
  | "fetchAll"
>;

const STATUS_LABEL: Record<DocumentSequenceRow["status"], string> = {
  not_configured: "Sin configurar",
  configured: "Configurada sin uso",
  used: "Usada / Bloqueada",
};

/** Secuencial SRI formateado a 9 dígitos (mismo formato D9 que el backend usa al capturar). */
function formatSequential(value: number | null): string {
  if (value === null) return "—";
  return String(value).padStart(9, "0");
}

export function DocumentSequencesListSection({
  loading,
  error,
  emissionPoints,
  selectedEmissionPointId,
  setSelectedEmissionPointId,
  rows,
  canManage,
  openConfigure,
  fetchAll,
}: Props) {
  const columns: ZHDataTableColumn<DocumentSequenceRow>[] = [
    {
      key: "docTypeName",
      header: "Tipo de documento",
      render: (row) => <span className="br-list-name">{row.docTypeName}</span>,
    },
    {
      key: "docTypeCode",
      header: "Código SRI",
      render: (row) => (
        <Badge label={row.docTypeCode} variant="neutral" size="md" code />
      ),
    },
    {
      key: "nextNumber",
      header: "Siguiente secuencial",
      cellClassName: "zh-table-cell--num",
      render: (row) => <span className="mono">{formatSequential(row.nextNumber)}</span>,
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <Badge
          label={STATUS_LABEL[row.status]}
          variant={
            row.status === "used"
              ? "gray"
              : row.status === "configured"
                ? "info"
                : "neutral"
          }
          size="md"
        />
      ),
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: "Acción",
            align: "right" as const,
            render: (row: DocumentSequenceRow) =>
              row.status === "used" ? (
                <ZHBtn type="button" variant="ghost" size="sm" disabled title="Bloqueado — la secuencia ya fue usada">
                  <span className="material-symbols-outlined">lock</span>
                </ZHBtn>
              ) : (
                <ZHBtn
                  type="button"
                  variant="secondary"
                  size="sm"
                  title={row.status === "configured" ? "Editar" : "Configurar"}
                  onClick={() => openConfigure(row)}
                >
                  <span className="material-symbols-outlined">
                    {row.status === "configured" ? "edit" : "add"}
                  </span>
                  {row.status === "configured" ? "Editar" : "Configurar"}
                </ZHBtn>
              ),
          },
        ]
      : []),
  ];

  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">
            format_list_numbered
          </span>
          <span className="pg-section-label">Secuencias documentales</span>
        </div>
        <div className="br-actions-tight">
          <ZHBtn
            variant="secondary"
            size="sm"
            type="button"
            disabled={loading}
            onClick={() => void fetchAll()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </ZHBtn>
        </div>
      </div>

      {error && (
        <div className="pg-pad-40">
          <EmptyState message={error} />
        </div>
      )}

      <div className="pg-table-controls">
        <div className="pg-table-controls-left">
          <ZhSelect
            className="zh-input"
            disabled={loading || emissionPoints.length === 0}
            value={selectedEmissionPointId ?? ""}
            onChange={(e) => setSelectedEmissionPointId(e.target.value || null)}
          >
            <option value="" disabled>
              — seleccionar punto de emisión —
            </option>
            {Object.entries(
              emissionPoints.reduce<Record<string, typeof emissionPoints>>(
                (acc, ep) => {
                  (acc[ep.establishmentName] ??= []).push(ep);
                  return acc;
                },
                {},
              ),
            ).map(([establishmentName, points]) => (
              <optgroup key={establishmentName} label={establishmentName}>
                {points.map((ep) => (
                  <option key={ep.id} value={ep.id}>
                    {ep.establishmentCode}-{ep.code} — {ep.name ?? ep.code}
                  </option>
                ))}
              </optgroup>
            ))}
          </ZhSelect>
        </div>
      </div>

      {loading ? (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      ) : emissionPoints.length === 0 ? (
        <div className="pg-pad-40">
          <EmptyState message="No hay puntos de emisión activos. Configure uno en Settings → Puntos de Emisión." />
        </div>
      ) : !selectedEmissionPointId ? (
        <div className="pg-pad-40">
          <EmptyState message="Seleccione un punto de emisión para ver sus secuencias documentales." />
        </div>
      ) : rows.length === 0 ? (
        <div className="pg-pad-40">
          <EmptyState message="No hay tipos de documento electrónicos soportados en el catálogo SRI." />
        </div>
      ) : (
        <ZHDataTable columns={columns} rows={rows} rowKey={(row) => row.docTypeCode} />
      )}
    </div>
  );
}
