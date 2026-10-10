import { useState } from "react";
import { ZHBtn, ZHLinkButton } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHNumberValue } from "../../../components/zh/ZHNumberValue";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import type {
  OpeningBridgeAccountDto,
  OpeningJournalEntryState,
  OpeningReconciliationJournalEntryDto,
} from "../types/importBatch.types";
import { OpeningJournalPublishDrawer } from "./OpeningJournalPublishDrawer";
import { CorrectOpeningModal } from "./CorrectOpeningModal";

const STATE_BADGES: Record<OpeningJournalEntryState, { label: string; variant: BadgeVariant }> = {
  Missing: { label: "Sin publicar", variant: "warning" },
  Pending: { label: "Pendiente", variant: "warning" },
  Failed: { label: "Falló", variant: "error" },
  Posted: { label: "Publicado", variant: "success" },
  ReversedNotReplaced: { label: "Apertura incompleta", variant: "error" },
};

/** Estados con los que el backend admite publicar (o reintentar) una versión. */
const PUBLISHABLE: ReadonlySet<OpeningJournalEntryState> = new Set([
  "Missing",
  "Pending",
  "Failed",
  "ReversedNotReplaced",
]);

function publishLabel(state: OpeningJournalEntryState): string {
  if (state === "Failed" || state === "Pending") return "Reintentar publicación";
  if (state === "ReversedNotReplaced") return "Publicar nueva versión";
  return "Publicar asiento de apertura";
}

interface Props {
  journal: OpeningReconciliationJournalEntryDto;
  cutoffDate: string | null;
  bridge: OpeningBridgeAccountDto | null;
  onChanged: () => void;
}

/**
 * IL-8D — ASI de apertura dentro de la conciliación (hub de Carga Inicial). Estado, versión e
 * historial vienen tal cual del backend (IL-8C); esta sección solo los muestra y abre:
 * publicar (IL-8A, Missing/Pending/Failed/ReversedNotReplaced) y "Corregir apertura" (IL-8B, Posted).
 * Permisos = los del endpoint: confirmar Carga Inicial + crear (publicar) / eliminar (corregir) en
 * Contabilidad.
 */
export function OpeningJournalSection({ journal, cutoffDate, bridge, onChanged }: Readonly<Props>) {
  const { canShow } = usePermissionsUi();
  const canConfirm = canShow("initialload.batches.confirm");
  const canPublish = canConfirm && canShow("accounting.create");
  const canCorrect = canConfirm && canShow("accounting.delete");
  const [publishing, setPublishing] = useState(false);
  const [correcting, setCorrecting] = useState(false);

  const badge = STATE_BADGES[journal.state] ?? { label: String(journal.state), variant: "neutral" };
  const showPublish = canPublish && PUBLISHABLE.has(journal.state);
  const showCorrect = canCorrect && journal.state === "Posted" && journal.postingId !== null;

  return (
    <section aria-label="Asiento de apertura (ASI)">
      <h3>
        Asiento de apertura (ASI) <Badge label={badge.label} variant={badge.variant} />
      </h3>
      {journal.state === "ReversedNotReplaced" && (
        <ZHPageNotice
          variant="warning"
          message="Apertura incompleta"
          detail={`La versión ${journal.lastSupersededVersion ?? ""} fue reversada. Publique una nueva versión del asiento de apertura.`}
        />
      )}
      {journal.state === "Failed" && journal.errorMessage && (
        <ZHPageNotice
          variant="error"
          message="La última publicación del asiento de apertura falló."
          detail={journal.errorMessage}
        />
      )}
      {journal.version !== null && (
        <>
          <ZHInfoRow label="Versión vigente" value={journal.version} />
          <ZHInfoRow
            label="Asiento"
            value={
              journal.journalEntryId ? (
                <ZHLinkButton
                  variant="ghost"
                  size="xs"
                  to={`/accounting/journal-entries/${journal.journalEntryId}`}
                >
                  {journal.journalEntryNumber ? `N° ${journal.journalEntryNumber}` : "Ver asiento"}
                </ZHLinkButton>
              ) : (
                "—"
              )
            }
          />
          <ZHInfoRow label="Fecha" value={journal.entryDate ? formatDate(journal.entryDate) : "—"} />
          <ZHInfoRow
            label="Total"
            value={
              journal.totalAmount === null ? (
                "—"
              ) : (
                <ZHNumberValue value={journal.totalAmount} precision="accounting" />
              )
            }
          />
          <ZHInfoRow label="Líneas" value={journal.lineCount ?? "—"} />
          <ZHInfoRow
            label="Publicado el"
            value={journal.postedAt ? formatDateTime(journal.postedAt) : "—"}
          />
        </>
      )}
      <p className="zh-form-help">
        {journal.versionCount === 0
          ? "Aún no se ha publicado ninguna versión."
          : `Versiones registradas: ${journal.versionCount}.`}
        {journal.lastSupersededVersion !== null &&
          ` Última versión corregida: v${journal.lastSupersededVersion}${
            journal.lastSupersededAt ? ` el ${formatDateTime(journal.lastSupersededAt)}` : ""
          }.`}
      </p>
      {(showPublish || showCorrect) && (
        <div>
          {showPublish && (
            <ZHBtn variant="primary" onClick={() => setPublishing(true)}>
              {publishLabel(journal.state)}
            </ZHBtn>
          )}
          {showCorrect && (
            <ZHBtn variant="secondary" onClick={() => setCorrecting(true)}>
              Corregir apertura
            </ZHBtn>
          )}
        </div>
      )}
      <OpeningJournalPublishDrawer
        open={publishing}
        cutoffDate={cutoffDate}
        bridge={bridge}
        onClose={() => setPublishing(false)}
        onSettled={onChanged}
      />
      <CorrectOpeningModal
        open={correcting}
        postingId={journal.postingId}
        version={journal.version}
        cutoffDate={cutoffDate}
        onClose={() => setCorrecting(false)}
        onCorrected={onChanged}
      />
    </section>
  );
}
