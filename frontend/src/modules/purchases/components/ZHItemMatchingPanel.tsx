import { useCallback, useEffect, useMemo, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { Badge, EmptyState, ErrorState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { itemService } from "../../items/api/itemService";
import { ProductPicker, type ProductProfile } from "./ProductPicker";
import { CreateItemFromReceptionLineModal } from "./CreateItemFromReceptionLineModal";
import {
  ItemMatchStatusBadge,
  useViewMatchedItem,
} from "./ItemMatchStatusBadge";
import {
  purchaseReceptionService,
  type PurchaseReceptionLineMatch,
} from "../api/purchaseReceptionService";
import "../../../styles/shared/items-catalog.css";
import "../styles/purchase-reception.css";

type Props = {
  open: boolean;
  documentId: string | null;
  supplierName: string;
  onClose: () => void;
};

type ChoiceValue = { itemId: string; sku: string; name: string } | null;
type ItemLabel = { sku: string; shortName: string };

type FilterId = "all" | "pending" | "review" | "linked";

const FILTER_TABS: ZHTab<FilterId>[] = [
  { id: "all", label: "Todos" },
  { id: "pending", label: "Pendientes" },
  { id: "review", label: "Revisar" },
  { id: "linked", label: "Asociados" },
];

const ITEM_LOOKUP_CONCURRENCY = 6;

export function ZHItemMatchingPanel({
  open,
  documentId,
  supplierName,
  onClose,
}: Props) {
  const viewMatchedItem = useViewMatchedItem(onClose);

  const [lines, setLines] = useState<PurchaseReceptionLineMatch[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [choices, setChoices] = useState<Record<string, ChoiceValue>>({});
  const [itemLabels, setItemLabels] = useState<Record<string, ItemLabel>>({});
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [applyingId, setApplyingId] = useState<string | null>(null);
  const [unmatchingId, setUnmatchingId] = useState<string | null>(null);
  const [bulkApplying, setBulkApplying] = useState(false);
  const [createItemLine, setCreateItemLine] =
    useState<PurchaseReceptionLineMatch | null>(null);
  const [filter, setFilter] = useState<FilterId>("all");

  const load = useCallback(async () => {
    if (!documentId) return;
    setLoading(true);
    setError(null);
    try {
      const result = await purchaseReceptionService.getLines(documentId);
      setLines(result);
      const initialChoices: Record<string, ChoiceValue> = {};
      for (const line of result) {
        const top = line.suggestions[0];
        initialChoices[line.lineId] = top
          ? { itemId: top.itemId, sku: top.sku, name: top.shortName }
          : null;
      }
      setChoices(initialChoices);
      setItemLabels({});
      setSelected(new Set());
      setFilter("all");
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudieron cargar las líneas del comprobante.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  // Los ítems que ya llegaron vinculados (AUTO_MATCHED / MANUALLY_MATCHED) al abrir el panel no traen
  // nombre/SKU en la respuesta — se resuelven en background con el mismo endpoint que ya usa ProductPicker,
  // con concurrencia acotada para no bloquear la carga con cientos de líneas.
  useEffect(() => {
    if (!open) return;
    const missing = lines.filter(
      (l) =>
        (l.matchStatus === "AUTO_MATCHED" ||
          l.matchStatus === "MANUALLY_MATCHED") &&
        l.itemId &&
        !itemLabels[l.lineId],
    );
    if (missing.length === 0) return;

    let cancelled = false;
    const cache = new Map<string, ItemLabel>();
    const queue = [...missing];

    const worker = async () => {
      while (queue.length > 0 && !cancelled) {
        const line = queue.shift();
        if (!line?.itemId) continue;
        try {
          let info = cache.get(line.itemId);
          if (!info) {
            const detail = await itemService.getById(line.itemId);
            info = { sku: detail.sku, shortName: detail.shortName };
            cache.set(line.itemId, info);
          }
          if (!cancelled) {
            const resolved = info;
            setItemLabels((prev) =>
              prev[line.lineId] ? prev : { ...prev, [line.lineId]: resolved },
            );
          }
        } catch {
          // Silencioso: la línea sigue mostrando el badge de estado sin nombre/SKU.
        }
      }
    };

    void Promise.all(
      Array.from(
        { length: Math.min(ITEM_LOOKUP_CONCURRENCY, missing.length) },
        worker,
      ),
    );
    return () => {
      cancelled = true;
    };
  }, [open, lines, itemLabels]);

  const counts = useMemo(() => {
    const c = { pending: 0, review: 0, auto: 0, manual: 0 };
    for (const line of lines) {
      if (line.matchStatus === "PENDING") c.pending += 1;
      else if (line.matchStatus === "NEEDS_REVIEW") c.review += 1;
      else if (line.matchStatus === "AUTO_MATCHED") c.auto += 1;
      else if (line.matchStatus === "MANUALLY_MATCHED") c.manual += 1;
    }
    return c;
  }, [lines]);

  const visibleLines = useMemo(() => {
    switch (filter) {
      case "pending":
        return lines.filter((l) => l.matchStatus === "PENDING");
      case "review":
        return lines.filter((l) => l.matchStatus === "NEEDS_REVIEW");
      case "linked":
        return lines.filter(
          (l) =>
            l.matchStatus === "AUTO_MATCHED" ||
            l.matchStatus === "MANUALLY_MATCHED",
        );
      default:
        return lines;
    }
  }, [lines, filter]);

  const handlePick = (lineId: string, profile: ProductProfile) => {
    setChoices((prev) => ({
      ...prev,
      [lineId]: { itemId: profile.id, sku: profile.sku, name: profile.name },
    }));
  };

  const handleApplyOne = async (lineId: string) => {
    const choice = choices[lineId];
    if (!choice) return;
    setApplyingId(lineId);
    try {
      const updated = await purchaseReceptionService.matchItem(
        lineId,
        choice.itemId,
      );
      setLines((prev) => prev.map((l) => (l.lineId === lineId ? updated : l)));
      setItemLabels((prev) => ({
        ...prev,
        [lineId]: { sku: choice.sku, shortName: choice.name },
      }));
      setSelected((prev) => {
        const next = new Set(prev);
        next.delete(lineId);
        return next;
      });
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo vincular la línea. Intente nuevamente.",
        }),
      );
    } finally {
      setApplyingId(null);
    }
  };

  const handleUnmatch = async (lineId: string) => {
    const confirmed = await message.confirm({
      title: "Desvincular Item",
      message:
        "La línea volverá a estado Pendiente y podrá buscar o crear un ítem nuevamente. Esta acción no afecta el XML ni compras ya generadas.",
      confirmLabel: "Desvincular",
      variant: "warning",
    });
    if (!confirmed) return;

    setUnmatchingId(lineId);
    try {
      const updated = await purchaseReceptionService.unmatchItem(lineId);
      setLines((prev) => prev.map((l) => (l.lineId === lineId ? updated : l)));
      setItemLabels((prev) => {
        const next = { ...prev };
        delete next[lineId];
        return next;
      });
      setChoices((prev) => ({ ...prev, [lineId]: null }));
      message.success(
        "Item desvinculado. La línea está pendiente de matching.",
      );
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo desvincular el ítem. Intente nuevamente.",
        }),
      );
    } finally {
      setUnmatchingId(null);
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
      .map((lineId) => ({
        purchaseReceptionLineId: lineId,
        itemId: choices[lineId]?.itemId,
      }))
      .filter(
        (m): m is { purchaseReceptionLineId: string; itemId: string } =>
          !!m.itemId,
      );
    if (matches.length === 0) return;

    setBulkApplying(true);
    setError(null);
    try {
      const { results } = await purchaseReceptionService.bulkMatch(matches);
      const newLabels: Record<string, ItemLabel> = {};
      for (const result of results) {
        if (!result.success) continue;
        const choice = choices[result.purchaseReceptionLineId];
        if (choice)
          newLabels[result.purchaseReceptionLineId] = {
            sku: choice.sku,
            shortName: choice.name,
          };
      }
      setItemLabels((prev) => ({ ...prev, ...newLabels }));
      await load();
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic:
            "No se pudo aplicar la vinculación masiva. Intente nuevamente.",
        }),
      );
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
        <ZHBtn
          variant="primary"
          type="button"
          disabled={selectableCount === 0 || bulkApplying}
          onClick={() => void handleApplySelected()}
        >
          {bulkApplying
            ? "Aplicando..."
            : `Aplicar seleccionados (${selectableCount})`}
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
            <Badge variant="error" label={`🔴 Pendientes: ${counts.pending}`} />
            <Badge variant="warning" label={`🟡 Revisar: ${counts.review}`} />
            <Badge variant="success" label={`🟢 Auto: ${counts.auto}`} />
            <Badge variant="info" label={`🔵 Manual: ${counts.manual}`} />
          </div>

          <div className="pur-matching-toolbar">
            <ZHTabBar
              tabs={FILTER_TABS}
              activeTab={filter}
              onChange={setFilter}
              ariaLabel="Filtrar líneas"
            />
          </div>

          <table className="table table--compact table--neutral table--align-top">
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
              {visibleLines.length === 0 && (
                <tr>
                  <td colSpan={5}>
                    <EmptyState message="No hay líneas para este filtro." />
                  </td>
                </tr>
              )}
              {visibleLines.map((line) => {
                const resolved =
                  line.matchStatus === "AUTO_MATCHED" ||
                  line.matchStatus === "MANUALLY_MATCHED";
                const choice = choices[line.lineId];
                const label = itemLabels[line.lineId];
                const topScore = line.suggestions[0]?.matchScore;

                return (
                  <tr key={line.lineId}>
                    <td>
                      {!resolved && (
                        <input
                          type="checkbox"
                          checked={selected.has(line.lineId)}
                          disabled={!choice}
                          onChange={() => toggleSelected(line.lineId)}
                        />
                      )}
                    </td>
                    <td>
                      <div className="pur-matching-desc">
                        {line.description}
                      </div>
                      <div className="pur-matching-meta">
                        {line.supplierCode && (
                          <span>Cód. prov.: {line.supplierCode}</span>
                        )}
                        <span>Cant.: {line.quantity}</span>
                        <span>{formatMoneyWithSymbol(line.unitPrice)}</span>
                      </div>
                    </td>
                    <td>
                      {resolved ? (
                        <div className="pur-matching-resolved-item">
                          <div className="pur-matching-resolved-name">
                            {label ? label.shortName : "Cargando ítem..."}
                          </div>
                          {label && (
                            <div className="pur-matching-resolved-sku">
                              SKU: {label.sku}
                            </div>
                          )}
                        </div>
                      ) : (
                        <div className="pur-matching-pick">
                          {choice && (
                            <div className="pur-matching-choice">
                              {choice.sku} — {choice.name}
                              {topScore !== undefined && (
                                <Badge
                                  variant="info"
                                  label={`${Math.round(topScore)}%`}
                                />
                              )}
                            </div>
                          )}
                          <ProductPicker
                            onSelect={(profile) =>
                              handlePick(line.lineId, profile)
                            }
                          />
                        </div>
                      )}
                    </td>
                    <td>
                      <ItemMatchStatusBadge status={line.matchStatus} />
                    </td>
                    <td>
                      {resolved ? (
                        <div className="pur-matching-actions">
                          {line.itemId && (
                            <ZHBtn
                              variant="ghost"
                              size="xs"
                              type="button"
                              onClick={() => viewMatchedItem(line.itemId!)}
                            >
                              Ver Item
                            </ZHBtn>
                          )}
                          <ZHBtn
                            variant="ghost"
                            size="xs"
                            type="button"
                            disabled={unmatchingId === line.lineId}
                            onClick={() => void handleUnmatch(line.lineId)}
                          >
                            {unmatchingId === line.lineId
                              ? "Desvinculando..."
                              : "Desvincular Item"}
                          </ZHBtn>
                        </div>
                      ) : (
                        <div className="pur-matching-actions">
                          <ZHBtn
                            variant="secondary"
                            size="xs"
                            type="button"
                            disabled={!choice || applyingId === line.lineId}
                            onClick={() => void handleApplyOne(line.lineId)}
                          >
                            {applyingId === line.lineId
                              ? "Aplicando..."
                              : "Vincular Item"}
                          </ZHBtn>
                          <ZHBtn
                            variant="ghost"
                            size="xs"
                            type="button"
                            onClick={() => setCreateItemLine(line)}
                          >
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
        onCreated={(updatedLine, item) => {
          setCreateItemLine(null);
          setLines((prev) =>
            prev.map((l) =>
              l.lineId === updatedLine.lineId ? updatedLine : l,
            ),
          );
          setItemLabels((prev) => ({
            ...prev,
            [updatedLine.lineId]: { sku: item.sku, shortName: item.shortName },
          }));
        }}
        onCreatedButNotLinked={(lineId, item) => {
          setChoices((prev) => ({
            ...prev,
            [lineId]: { itemId: item.id, sku: item.sku, name: item.shortName },
          }));
        }}
      />
    </ZHModal>
  );
}
