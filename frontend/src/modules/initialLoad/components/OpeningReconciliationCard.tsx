import { useCallback, useEffect, useState } from "react";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHLinkButton } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHNumberValue } from "../../../components/zh/ZHNumberValue";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { Badge } from "../../../components/PageShell";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { initialLoadService } from "../api/initialLoadService";
import type {
  ImportType,
  OpeningBalanceReconciliationDto,
  OpeningReconciliationBatchDto,
  OpeningReconciliationStatus,
  OpeningReconciliationTypeDto,
} from "../types/importBatch.types";

const TYPE_LABELS: Partial<Record<ImportType, string>> = {
  InitialStock: "Inventario inicial",
  InitialReceivables: "CxC inicial",
  InitialPayables: "CxP inicial",
};

const STATUS_BADGES: Record<
  OpeningReconciliationStatus,
  { label: string; variant: "success" | "error" | "warning" }
> = {
  Reconciled: { label: "Conciliado", variant: "success" },
  Difference: { label: "Con diferencia", variant: "error" },
  PendingPosting: { label: "Pendiente de posting", variant: "warning" },
};

function StatusBadge({ status }: { status: OpeningReconciliationStatus }) {
  const badge = STATUS_BADGES[status];
  return <Badge label={badge.label} variant={badge.variant} />;
}

function Amount({ value }: { value: number | null }) {
  return value === null ? <span>—</span> : <ZHNumberValue value={value} precision="accounting" />;
}

/**
 * IL-7C — Hub de Carga Inicial: conciliación de la apertura contable contra los saldos operativos
 * confirmados, al corte `Company.OpeningBalanceDate`. Todo el cálculo (submayor, mayor, diferencia,
 * estado y bloqueos de cierre) lo hace el backend; esta tarjeta solo lo muestra y permite
 * contabilizar/reintentar un lote (IL-7B) cuando el backend lo marca como `canPost`.
 */
export function OpeningReconciliationCard() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("accounting.view");
  const canPost = canShow("initialload.batches.confirm") && canShow("accounting.create");
  const [data, setData] = useState<OpeningBalanceReconciliationDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [toPost, setToPost] = useState<OpeningReconciliationBatchDto | null>(null);
  const [postingId, setPostingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setData(await initialLoadService.getOpeningReconciliation());
      setLoadError(null);
    } catch (err: unknown) {
      setLoadError(
        formatApiRequestError(err, { generic: "No se pudo cargar la conciliación de apertura." }),
      );
    }
  }, []);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const post = async (batch: OpeningReconciliationBatchDto) => {
    setToPost(null);
    setPostingId(batch.importBatchId);
    try {
      await initialLoadService.postOpeningBalance(batch.importBatchId);
      message.success("Apertura contabilizada.");
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo contabilizar la apertura del lote." }),
      );
    } finally {
      setPostingId(null);
      await load();
    }
  };

  if (!canView) return null;

  const batchColumns: ZHDataTableColumn<OpeningReconciliationBatchDto>[] = [
    {
      key: "type",
      header: "Tipo",
      render: (row) => (
        <>
          <div>{TYPE_LABELS[row.importType] ?? row.importType}</div>
          {row.label && <div className="zh-form-help">{row.label}</div>}
        </>
      ),
    },
    {
      key: "operational",
      header: "Monto operativo",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => <Amount value={row.operationalAmount} />,
    },
    {
      key: "accounting",
      header: "Monto contable",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => <Amount value={row.accountingAmount} />,
    },
    {
      key: "difference",
      header: "Diferencia",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => <Amount value={row.difference} />,
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <>
          <StatusBadge status={row.status} />
          {row.errorMessage && <div className="zh-form-help">{row.errorMessage}</div>}
        </>
      ),
    },
    {
      key: "journal",
      header: "Asiento",
      render: (row) =>
        row.journalEntryId ? (
          <ZHLinkButton
            variant="ghost"
            size="xs"
            to={`/accounting/journal-entries/${row.journalEntryId}`}
          >
            {row.journalEntryNumber ? `N° ${row.journalEntryNumber}` : "Ver asiento"}
          </ZHLinkButton>
        ) : (
          <span>—</span>
        ),
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (row) =>
        canPost && row.canPost ? (
          <ZHBtn
            size="xs"
            variant="primary"
            disabled={postingId !== null}
            onClick={() => setToPost(row)}
          >
            {row.postingStatus === "Failed" ? "Reintentar" : "Contabilizar"}
          </ZHBtn>
        ) : null,
    },
  ];

  const typeColumns: ZHDataTableColumn<OpeningReconciliationTypeDto>[] = [
    {
      key: "type",
      header: "Saldo",
      render: (row) => TYPE_LABELS[row.importType] ?? row.importType,
    },
    {
      key: "account",
      header: "Cuenta",
      render: (row) =>
        row.accountCode ? `${row.accountCode} ${row.accountName ?? ""}` : "Sin regla contable",
    },
    {
      key: "operational",
      header: "Submayor",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => <Amount value={row.operationalAmount} />,
    },
    {
      key: "ledger",
      header: "Mayor al corte",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => <Amount value={row.ledgerBalance} />,
    },
    {
      key: "difference",
      header: "Diferencia",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => <Amount value={row.difference} />,
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => <StatusBadge status={row.status} />,
    },
  ];

  const bridge = data?.bridgeAccount ?? null;

  return (
    <ZHCard
      title="Conciliación de apertura"
      actions={data ? <StatusBadge status={data.status} /> : undefined}
    >
      <p className="zh-form-help">
        Compara los saldos iniciales confirmados (Kardex, CxC y CxP) con la contabilidad al corte{" "}
        <strong>{data?.cutoffDate ? formatDate(data.cutoffDate) : "sin definir"}</strong>.
        Diferencia = submayor − mayor.
      </p>
      {loadError && (
        <ZHPageNotice
          variant="error"
          message="No se pudo cargar la conciliación."
          detail={loadError}
        />
      )}
      {data && (
        <>
          <ZHDataTable
            columns={batchColumns}
            rows={data.batches}
            rowKey={(row) => row.importBatchId}
            emptyMessage="Aún no hay lotes de saldos iniciales confirmados."
            tableClassName="table--compact"
          />
          <ZHDataTable
            columns={typeColumns}
            rows={data.types}
            rowKey={(row) => row.importType}
            tableClassName="table--compact"
          />
          {bridge && (
            <p>
              Cuenta puente{" "}
              <strong>
                {bridge.accountCode} {bridge.accountName}
              </strong>
              : saldo al corte <Amount value={bridge.balanceAtCutoff} />, saldo actual{" "}
              <Amount value={bridge.currentBalance} />, aportado por la apertura{" "}
              <Amount value={bridge.fromOpeningPostings} />. Pendiente de reclasificación:{" "}
              <strong>
                <Amount value={bridge.pendingReclassification} />
              </strong>
            </p>
          )}
          {data.canCloseImplementation ? (
            <ZHPageNotice
              variant="success"
              message="Apertura conciliada: la implementación puede cerrarse."
            />
          ) : (
            <ZHPageNotice
              variant="warning"
              message="La implementación no puede cerrarse todavía."
              detail={data.blockers.map((b) => b.message).join(" · ")}
            />
          )}
        </>
      )}
      <ZHConfirmModal
        open={toPost !== null}
        title="Contabilizar apertura"
        message={
          toPost
            ? `Se generará el asiento de apertura del lote de ${TYPE_LABELS[toPost.importType] ?? toPost.importType} al corte. ¿Continuar?`
            : ""
        }
        confirmLabel="Contabilizar"
        onConfirm={() => toPost && void post(toPost)}
        onCancel={() => setToPost(null)}
      />
    </ZHCard>
  );
}
