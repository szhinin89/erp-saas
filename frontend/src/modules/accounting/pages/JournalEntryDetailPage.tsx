import { useEffect, useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { accountingApi, type JournalEntryDetailDto } from "../api/accountingApi";
// ACCOUNTING-DS-FULL-AUDIT-10F: sin este import, `.prd-sku`/`.prd-empty-row` no tienen estilo —
// mismo root cause ya corregido en AccountingReportsPage.tsx/JournalEntriesPage.tsx.
import "../../../styles/shared/items-catalog.css";

function statusBadge(status: string): { label: string; variant: "gray" | "green" | "red" } {
  switch (status) {
    case "Posted":
      return { label: "Contabilizado", variant: "green" };
    case "Reversed":
      return { label: "Reversado", variant: "red" };
    default:
      return { label: "Borrador", variant: "gray" };
  }
}

function InfoLabel({ children }: { children: ReactNode }) {
  return <ZHFieldLabel size="sm">{children}</ZHFieldLabel>;
}

/**
 * Detalle de un asiento contable (ACCOUNTING-LEDGER-VISIBILITY-01) — cabecera, líneas Debe/Haber
 * y aviso si no cuadra. Solo lectura: sin editar ni eliminar el asiento desde esta pantalla.
 * ACCOUNTING-DS-FULL-AUDIT-10F: los pares label/valor usaban `.sr-general-grid`/
 * `.sr-general-grid__label`/`.sr-general-grid__value` — clases sin ninguna definición CSS en el
 * proyecto (grep exhaustivo en los 4 stylesheets globales) — reemplazadas por `ZHInfoRow` +
 * `ZHFieldLabel` + `ZHDataValue`, el trío real del Design System para esto (mismo patrón ya
 * probado en `StockTransferPage.tsx`). La tabla de líneas se mantiene como `<table>` nativa
 * (`table table--compact table--neutral`, clases reales de `zh-ui.css`) en vez de `ZHDataTable`
 * porque necesita un `<tfoot>` de totales — `ZHDataTable` no expone ese slot; no es un raw-HTML
 * evitable, es la única forma de tener el pie de totales dentro de la grilla.
 */
export function JournalEntryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [entry, setEntry] = useState<JournalEntryDetailDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    accountingApi
      .getJournalEntryById(id)
      .then((dto) => {
        if (!cancelled) setEntry(dto);
      })
      .catch((err: unknown) => {
        message.error(formatApiRequestError(err, { generic: "No se pudo cargar el asiento contable." }));
        navigate("/accounting/journal-entries");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id, navigate]);

  if (loading) {
    return (
      <PageShell kicker="Contabilidad" title="Asiento contable" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (!entry) {
    return (
      <PageShell kicker="Contabilidad" title="Asiento contable">
        <ZHPageNotice variant="error" message="Asiento no encontrado" />
      </PageShell>
    );
  }

  const badge = statusBadge(entry.status);

  return (
    <PageShell
      kicker="Contabilidad"
      title={`Asiento ${entry.entryNumber ?? "(sin numerar)"}`}
      subtitle={`${entry.sourceModule} — ${entry.sourceEventType}`}
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate("/accounting/journal-entries")}>
          Volver al listado
        </ZHBtn>
      }
    >
      <ZHCard title="Datos generales" actions={<Badge label={badge.label} variant={badge.variant} />}>
        <ZHInfoRow label={<InfoLabel>Fecha</InfoLabel>} value={<ZHDataValue>{formatDate(entry.entryDate)}</ZHDataValue>} />
        <ZHInfoRow label={<InfoLabel>Ejercicio fiscal</InfoLabel>} value={<ZHDataValue variant="numeric">{entry.fiscalYear}</ZHDataValue>} />
        <ZHInfoRow label={<InfoLabel>Descripción</InfoLabel>} value={<ZHDataValue>{entry.description}</ZHDataValue>} wide />
        <ZHInfoRow
          label={<InfoLabel>Publicado</InfoLabel>}
          value={<ZHDataValue>{entry.postedAtUtc ? formatDateTime(entry.postedAtUtc) : "—"}</ZHDataValue>}
        />
      </ZHCard>

      {(entry.originalJournalEntryId || entry.reverseJournalEntryId) && (
        <ZHCard
          title="Reverso contable"
          actions={
            entry.originalJournalEntryId ? (
              <Badge label="Es reverso de otro asiento" variant="blue" />
            ) : (
              <Badge label="Tiene un asiento reverso" variant="red" />
            )
          }
        >
          {entry.originalJournalEntryId && (
            <ZHInfoRow
              label={<InfoLabel>Asiento original</InfoLabel>}
              value={
                <ZHDataValue>
                  {entry.originalJournalEntryNumber ?? "—"}
                  {entry.originalJournalEntryDate ? ` — ${formatDate(entry.originalJournalEntryDate)}` : ""}
                </ZHDataValue>
              }
            />
          )}
          {entry.reverseJournalEntryId && (
            <>
              <ZHInfoRow
                label={<InfoLabel>Asiento reverso</InfoLabel>}
                value={
                  <ZHDataValue>
                    {entry.reverseJournalEntryNumber ?? "—"}
                    {entry.reverseJournalEntryDate ? ` — ${formatDate(entry.reverseJournalEntryDate)}` : ""}
                  </ZHDataValue>
                }
              />
              <ZHInfoRow
                label={<InfoLabel>Fecha del reverso</InfoLabel>}
                value={<ZHDataValue>{entry.reversedAtUtc ? formatDateTime(entry.reversedAtUtc) : "—"}</ZHDataValue>}
              />
            </>
          )}
          {entry.reverseReason && (
            <ZHInfoRow label={<InfoLabel>Motivo</InfoLabel>} value={<ZHDataValue>{entry.reverseReason}</ZHDataValue>} wide />
          )}
          <ZHBtn
            type="button"
            variant="ghost"
            size="sm"
            onClick={() =>
              navigate(
                `/accounting/journal-entries/${entry.originalJournalEntryId ?? entry.reverseJournalEntryId}`,
              )
            }
          >
            Ver asiento relacionado
          </ZHBtn>
        </ZHCard>
      )}

      <ZHCard title="Documento origen">
        {entry.sourceDocumentNumber ? (
          <>
            <ZHInfoRow label={<InfoLabel>Tipo</InfoLabel>} value={<ZHDataValue>{entry.sourceDocumentType}</ZHDataValue>} />
            <ZHInfoRow label={<InfoLabel>Número</InfoLabel>} value={<ZHDataValue variant="code">{entry.sourceDocumentNumber}</ZHDataValue>} />
            <ZHInfoRow
              label={<InfoLabel>Cliente / Proveedor</InfoLabel>}
              value={<ZHDataValue>{entry.sourcePartyName ?? "—"}</ZHDataValue>}
            />
            <ZHInfoRow label={<InfoLabel>Estado</InfoLabel>} value={<ZHDataValue>{entry.sourceStatus ?? "—"}</ZHDataValue>} />
            <ZHInfoRow
              label={<InfoLabel>Fecha</InfoLabel>}
              value={<ZHDataValue>{entry.sourceDocumentDate ? formatDate(entry.sourceDocumentDate) : "—"}</ZHDataValue>}
            />
            {entry.sourceRoute && (
              <ZHBtn type="button" variant="ghost" size="sm" onClick={() => navigate(entry.sourceRoute!)}>
                Ver documento origen
              </ZHBtn>
            )}
          </>
        ) : (
          <>
            <ZHPageNotice
              variant="info"
              message="Origen documental no disponible — el número/estado del documento no pudo resolverse."
            />
            <ZHInfoRow
              label={<InfoLabel>Módulo de origen (técnico)</InfoLabel>}
              value={<ZHDataValue>{entry.sourceModule}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<InfoLabel>Tipo de hecho (técnico)</InfoLabel>}
              value={<ZHDataValue>{entry.sourceEventType}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<InfoLabel>Id de origen (técnico)</InfoLabel>}
              value={<ZHDataValue variant="code">{entry.sourceEventId}</ZHDataValue>}
            />
          </>
        )}
      </ZHCard>

      {!entry.isBalanced && (
        <ZHPageNotice
          variant="error"
          message="Este asiento no está balanceado: el total Debe no coincide con el total Haber."
        />
      )}

      <ZHCard title="Líneas">
        <div className="table-scroll">
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th>Cuenta</th>
                <th>Descripción</th>
                <th className="zh-text-align-right">Debe</th>
                <th className="zh-text-align-right">Haber</th>
              </tr>
            </thead>
            <tbody>
              {entry.lines.map((l) => (
                <tr key={l.id}>
                  <td>
                    <strong>{l.accountCode}</strong> — {l.accountName}
                  </td>
                  <td>{l.description ?? "—"}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={l.debit > 0 ? l.debit : null} />
                  </td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={l.credit > 0 ? l.credit : null} />
                  </td>
                </tr>
              ))}
              {entry.lines.length === 0 && (
                <tr className="prd-empty-row">
                  <td colSpan={4}>Sin líneas registradas.</td>
                </tr>
              )}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={2}>
                  <strong>Totales</strong>
                </td>
                <td className="zh-table-cell--num">
                  <ZHMoneyValue value={entry.totalDebit} emphasis="total" />
                </td>
                <td className="zh-table-cell--num">
                  <ZHMoneyValue value={entry.totalCredit} emphasis="total" />
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </ZHCard>
    </PageShell>
  );
}
