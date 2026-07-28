import { useCallback, useEffect, useMemo, useState } from 'react';
import { ZHModal } from '../../../components/zh/ZHModal';
import { Badge, EmptyState, ErrorState } from '../../../components/PageShell';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { formatMoneyWithSymbol } from '../../../lib/sanitizers';
import { ProductPicker, type ProductProfile } from './ProductPicker';
import { CreateItemFromReceptionLineModal } from './CreateItemFromReceptionLineModal';
import {
  purchaseReceptionService,
  type PurchaseReceptionLineMatch,
} from '../api/purchaseReceptionService';

type Props = {
  open: boolean;
  documentId: string | null;
  supplierName: string;
  onClose: () => void;
};

const STATUS_LABEL: Record<PurchaseReceptionLineMatch['matchStatus'], string> = {
  PENDING: 'Pendiente',
  NEEDS_REVIEW: 'Sugerido',
  AUTO_MATCHED: 'Vinculado (auto)',
  MANUALLY_MATCHED: 'Vinculado',
};

const STATUS_VARIANT: Record<PurchaseReceptionLineMatch['matchStatus'], 'green' | 'gray' | 'blue'> = {
  PENDING: 'gray',
  NEEDS_REVIEW: 'blue',
  AUTO_MATCHED: 'green',
  MANUALLY_MATCHED: 'green',
};

type ChoiceLabel = { itemId: string; label: string } | null;

export function ZHItemMatchingPanel({ open, documentId, supplierName, onClose }: Props) {
  const [lines, setLines] = useState<PurchaseReceptionLineMatch[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [choices, setChoices] = useState<Record<string, ChoiceLabel>>({});
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [applyingId, setApplyingId] = useState<string | null>(null);
  const [bulkApplying, setBulkApplying] = useState(false);
  const [createItemLine, setCreateItemLine] = useState<PurchaseReceptionLineMatch | null>(null);

  const load = useCallback(async () => {
    if (!documentId) return;
    setLoading(true);
    setError(null);
    try {
      const result = await purchaseReceptionService.getLines(documentId);
      setLines(result);
      const initialChoices: Record<string, ChoiceLabel> = {};
      for (const line of result) {
        const top = line.suggestions[0];
        initialChoices[line.lineId] = top
          ? { itemId: top.itemId, label: `${top.sku} — ${top.shortName}` }
          : null;
      }
      setChoices(initialChoices);
      setSelected(new Set());
    } catch {
      setError('No se pudieron cargar las líneas del comprobante.');
    } finally {
      setLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  const pendingCount = useMemo(
    () => lines.filter((l) => l.matchStatus === 'PENDING' || l.matchStatus === 'NEEDS_REVIEW').length,
    [lines],
  );

  const handlePick = (lineId: string, profile: ProductProfile) => {
    setChoices((prev) => ({ ...prev, [lineId]: { itemId: profile.id, label: `${profile.sku} — ${profile.name}` } }));
  };

  const handleApplyOne = async (lineId: string) => {
    const choice = choices[lineId];
    if (!choice) return;
    setApplyingId(lineId);
    try {
      const updated = await purchaseReceptionService.matchItem(lineId, choice.itemId);
      setLines((prev) => prev.map((l) => (l.lineId === lineId ? updated : l)));
      setSelected((prev) => {
        const next = new Set(prev);
        next.delete(lineId);
        return next;
      });
    } catch {
      setError('No se pudo vincular la línea. Intente nuevamente.');
    } finally {
      setApplyingId(null);
    }
  };

  const toggleSelected = (lineId: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(lineId)) next.delete(lineId);
      else next.add(lineId);
      return next;
    });
  };

  const handleApplySelected = async () => {
    const matches = [...selected]
      .map((lineId) => ({ purchaseReceptionLineId: lineId, itemId: choices[lineId]?.itemId }))
      .filter((m): m is { purchaseReceptionLineId: string; itemId: string } => !!m.itemId);
    if (matches.length === 0) return;

    setBulkApplying(true);
    setError(null);
    try {
      await purchaseReceptionService.bulkMatch(matches);
      await load();
    } catch {
      setError('No se pudo aplicar la vinculación masiva. Intente nuevamente.');
    } finally {
      setBulkApplying(false);
    }
  };

  const selectableCount = [...selected].filter((id) => choices[id]).length;

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="xl"
      title="Vincular productos"
      subtitle="Concilie cada línea del comprobante con un ítem existente del catálogo."
      footer={
        <ZHBtn variant="primary" type="button" disabled={selectableCount === 0 || bulkApplying}
          onClick={() => void handleApplySelected()}>
          {bulkApplying ? 'Aplicando...' : `Aplicar seleccionados (${selectableCount})`}
        </ZHBtn>
      }
    >
      {loading && <EmptyState message="Cargando líneas..." />}
      {!loading && error && <ErrorState message={error} />}
      {!loading && !error && lines.length === 0 && (
        <EmptyState message="Este comprobante no tiene líneas de detalle interpretables." />
      )}

      {!loading && lines.length > 0 && (
        <>
          <div className="pur-matching-summary">
            <Badge variant="gray" label={`Por vincular: ${pendingCount}`} />
            <Badge variant="green" label={`Vinculadas: ${lines.length - pendingCount}`} />
          </div>

          <table className="pur-matching-table">
            <thead>
              <tr>
                <th></th>
                <th>Detalle XML</th>
                <th>Sugerencia / búsqueda</th>
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line) => {
                const resolved = line.matchStatus === 'AUTO_MATCHED' || line.matchStatus === 'MANUALLY_MATCHED';
                const choice = choices[line.lineId];
                const topScore = line.suggestions[0]?.matchScore;

                return (
                  <tr key={line.lineId}>
                    <td>
                      {!resolved && (
                        <input type="checkbox" checked={selected.has(line.lineId)}
                          disabled={!choice}
                          onChange={() => toggleSelected(line.lineId)} />
                      )}
                    </td>
                    <td>
                      <div className="pur-matching-desc">{line.description}</div>
                      <div className="pur-matching-meta">
                        {line.supplierCode && <span>Cód. prov.: {line.supplierCode}</span>}
                        <span>Cant.: {line.quantity}</span>
                        <span>{formatMoneyWithSymbol(line.unitPrice)}</span>
                      </div>
                    </td>
                    <td>
                      {resolved ? (
                        <span className="pur-matching-resolved">Ítem vinculado</span>
                      ) : (
                        <div className="pur-matching-pick">
                          {choice && (
                            <div className="pur-matching-choice">
                              {choice.label}
                              {topScore !== undefined && <Badge variant="blue" label={`${Math.round(topScore)}%`} />}
                            </div>
                          )}
                          <ProductPicker onSelect={(profile) => handlePick(line.lineId, profile)} />
                        </div>
                      )}
                    </td>
                    <td>
                      <Badge variant={STATUS_VARIANT[line.matchStatus]} label={STATUS_LABEL[line.matchStatus]} />
                    </td>
                    <td>
                      {!resolved && (
                        <div className="pur-matching-actions">
                          <ZHBtn variant="secondary" size="xs" type="button"
                            disabled={!choice || applyingId === line.lineId}
                            onClick={() => void handleApplyOne(line.lineId)}>
                            {applyingId === line.lineId ? 'Aplicando...' : 'Vincular'}
                          </ZHBtn>
                          <ZHBtn variant="ghost" size="xs" type="button"
                            onClick={() => setCreateItemLine(line)}>
                            Crear Item
                          </ZHBtn>
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </>
      )}

      <CreateItemFromReceptionLineModal
        open={createItemLine !== null}
        line={createItemLine}
        supplierName={supplierName}
        onClose={() => setCreateItemLine(null)}
        onCreated={(updatedLine) => {
          setCreateItemLine(null);
          setLines((prev) => prev.map((l) => (l.lineId === updatedLine.lineId ? updatedLine : l)));
        }}
      />
    </ZHModal>
  );
}
