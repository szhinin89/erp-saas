import { useEffect, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { useI18n } from "../../../i18n/i18n";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { formatMoney } from "../../../lib/sanitizers";
import type {
  PurchaseCostDistributionDto,
  PurchaseCostDistributionType,
  PurchaseInvoiceDto,
  PurchaseLineDto,
} from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { CostDistributionDraft } from "../hooks/usePurchaseCostDistributions";

type Mode = "external" | "xml";

interface Props {
  open: boolean;
  error?: string;
  busy: boolean;
  invoice: PurchaseInvoiceDto | null;
  /** Líneas del formulario: aportan la naturaleza del ítem (contexto) por `lineId`. */
  formLines: PurchaseLineFormValues[];
  onCancel: () => void;
  onSave: (draft: CostDistributionDraft) => Promise<boolean>;
  onRemove: (distribution: PurchaseCostDistributionDto) => Promise<boolean>;
}

const newDraft = (sourceLineId: string | null = null): CostDistributionDraft => ({
  id: crypto.randomUUID(), costType: "Freight", expectedRevision: 0, amount: null, targetLineIds: [], sourceLineId,
});

/**
 * PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — distribuciones registradas del borrador guardado y un
 * editor que crea o reemplaza (nunca suma) una distribución. Costo externo: importe propio, destinos
 * ítems Product o Service. Transporte del XML: importe = base neta de su línea fiscal Service (sin
 * desglose), destinos solo Product. Las asignaciones mostradas son las del servidor.
 */
export function DistributeCostModal({ open, error, busy, invoice, formLines, onCancel, onSave, onRemove }: Props) {
  const { t } = useI18n();
  const quantityDecimals = usePrecisionDecimals("quantity");
  const [draft, setDraft] = useState<CostDistributionDraft | null>(null);
  useEffect(() => { if (open) setDraft(null); }, [open]);

  const lines = invoice?.lines ?? [];
  const distributions = invoice?.costDistributions ?? [];
  const lineById = new Map(lines.map((l) => [l.id, l]));
  const participatesInInventory = (lineId: string) =>
    formLines.find((f) => f.lineId === lineId)?.context?.participatesInInventory;
  const isXmlTransportSource = (l: PurchaseLineDto) =>
    !!l.purchaseReceptionLineId && l.xmlAllocatedSubtotal == null && participatesInInventory(l.id) === false
    && !distributions.some((d) => d.sourceLineId === l.id && d.id !== draft?.id);
  const xmlSources = lines.filter(isXmlTransportSource);
  const mode: Mode = draft && draft.sourceLineId !== null ? "xml" : "external";
  const eligibleTargets = lines.filter((l) => l.quantityInBaseUom > 0 && l.id !== draft?.sourceLineId && (mode === "xml"
    ? participatesInInventory(l.id) === true
    : !!l.itemId));
  const source = draft?.sourceLineId ? lineById.get(draft.sourceLineId) : undefined;

  const typeLabel = (type: PurchaseCostDistributionType) => type === "Freight"
    ? t("purchases.distributeCost.typeFreight", "Flete")
    : t("purchases.distributeCost.typeOther", "Otro gasto");
  const originLabel = (d: PurchaseCostDistributionDto) => d.sourceLineId
    ? t("purchases.distributeCost.originXml", { line: lineById.get(d.sourceLineId)?.description ?? "" })
    : t("purchases.distributeCost.originExternal", "Costo externo");

  const patch = (change: Partial<CostDistributionDraft>) => setDraft((d) => (d ? { ...d, ...change } : d));
  const toggleTarget = (lineId: string) => patch({
    targetLineIds: draft?.targetLineIds.includes(lineId)
      ? draft.targetLineIds.filter((id) => id !== lineId)
      : [...(draft?.targetLineIds ?? []), lineId],
  });
  const startEdit = (d: PurchaseCostDistributionDto) => setDraft({
    id: d.id, costType: d.costType, expectedRevision: d.revision, amount: d.sourceLineId ? null : d.amount,
    targetLineIds: d.allocations.map((a) => a.lineId), sourceLineId: d.sourceLineId,
  });
  const changeMode = (next: Mode) => setDraft(newDraft(next === "xml" ? (xmlSources[0]?.id ?? "") : null));

  const draftIssue = !draft ? null
    : mode === "external" && !(draft.amount && draft.amount > 0)
      ? t("purchases.distributeCost.errors.amountRequired", "El valor a distribuir debe ser mayor a cero.")
      : mode === "xml" && !source
        ? t("purchases.distributeCost.errors.sourceRequired", "Seleccione la línea de transporte del XML.")
        : draft.targetLineIds.length === 0
          ? t("purchases.distributeCost.errors.noLines", "Debe incluir al menos un ítem.")
          : null;
  const isNew = !!draft && !distributions.some((d) => d.id === draft.id);

  const handleSave = async () => {
    if (!draft || draftIssue) return;
    if (await onSave(draft)) setDraft(null);
  };

  return (
    <ZHModal
      open={open}
      onClose={onCancel}
      size="2xl"
      title={t("purchases.distributeCost.title", "Distribuir flete/gasto")}
      subtitle={t("purchases.distributeCost.subtitle",
        "Cada distribución reemplaza a la anterior; los importes por línea los calcula el sistema.")}
      footer={<ZHBtn variant="ghost" size="md" onClick={onCancel} disabled={busy}>{t("common.close", "Cerrar")}</ZHBtn>}
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      <section className="pdc-section" aria-label={t("purchases.distributeCost.registered", "Distribuciones registradas")}>
        <h4 className="zh-section-title pdc-section__title">
          {t("purchases.distributeCost.registered", "Distribuciones registradas")}
        </h4>
        {distributions.length === 0 ? (
          <p className="zh-text-muted">{t("purchases.distributeCost.empty", "La compra no tiene distribuciones de costos.")}</p>
        ) : (
          <div className="table-scroll pdc-table-scroll">
            <table className="table table--compact responsive-table pdc-table">
              <thead>
                <tr>
                  <th>{t("purchases.distributeCost.type", "Tipo")}</th>
                  <th>{t("purchases.distributeCost.col.origin", "Origen")}</th>
                  <th className="zh-text-align-right">{t("purchases.distributeCost.amount", "Valor a distribuir")}</th>
                  <th>{t("purchases.distributeCost.col.allocated", "Valor asignado")}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {distributions.map((d) => (
                  <tr key={d.id}>
                    <td data-label={t("purchases.distributeCost.type", "Tipo")}>{typeLabel(d.costType)}</td>
                    <td data-label={t("purchases.distributeCost.col.origin", "Origen")}>{originLabel(d)}</td>
                    <td className="zh-table-cell--num" data-label={t("purchases.distributeCost.amount", "Valor a distribuir")}>
                      <ZHMoneyValue value={d.amount} precision="money" currencySymbol="" />
                    </td>
                    <td data-label={t("purchases.distributeCost.col.allocated", "Valor asignado")}>
                      <ul className="pdc-allocations">
                        {d.allocations.map((a) => (
                          <li key={a.lineId}>
                            <span className="pdc-table__desc">{lineById.get(a.lineId)?.description ?? a.lineId}</span>
                            <ZHMoneyValue value={a.amount} precision="costAllocation" currencySymbol="" />
                          </li>
                        ))}
                      </ul>
                    </td>
                    <td className="pdc-actions">
                      <ZHBtn variant="secondary" size="sm" disabled={busy} onClick={() => startEdit(d)}>
                        {t("common.edit", "Editar")}
                      </ZHBtn>
                      <ZHBtn variant="ghost" size="sm" disabled={busy}
                        onClick={() => { if (draft?.id === d.id) setDraft(null); void onRemove(d); }}>
                        {t("purchases.distributeCost.remove", "Retirar")}
                      </ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {!draft && (
          <ZHBtn variant="primary" size="md" disabled={busy} onClick={() => setDraft(newDraft())}>
            {t("purchases.distributeCost.add", "Nueva distribución")}
          </ZHBtn>
        )}
      </section>

      {draft && (
        <section className="pdc-section" aria-label={t("purchases.distributeCost.editor", "Distribución")}>
          <h4 className="zh-section-title pdc-section__title">
            {isNew ? t("purchases.distributeCost.add", "Nueva distribución") : t("purchases.distributeCost.editing", "Editar distribución")}
          </h4>
          <div className="pdc-toolbar">
            <ZHField density="compact" label={t("purchases.distributeCost.mode", "Origen del costo")}>
              <ZhSelect density="compact" value={mode} disabled={!isNew}
                onChange={(e) => changeMode(e.target.value as Mode)}>
                <option value="external">{t("purchases.distributeCost.originExternal", "Costo externo")}</option>
                <option value="xml" disabled={xmlSources.length === 0}>
                  {t("purchases.distributeCost.modeXml", "Transporte del XML")}
                </option>
              </ZhSelect>
            </ZHField>
            {mode === "external" ? (
              <>
                <ZHField density="compact" label={t("purchases.distributeCost.type", "Tipo")}>
                  <ZhSelect density="compact" value={draft.costType} disabled={!isNew}
                    onChange={(e) => patch({ costType: e.target.value as PurchaseCostDistributionType })}>
                    <option value="Freight">{typeLabel("Freight")}</option>
                    <option value="OtherCost">{typeLabel("OtherCost")}</option>
                  </ZhSelect>
                </ZHField>
                <ZHField density="compact" label={t("purchases.distributeCost.amount", "Valor a distribuir")}>
                  <ZhDecimalInput key={draft.id} precision="money" positiveOnly defaultValue={draft.amount ?? 0}
                    onBlur={(e) => patch({ amount: Number(e.target.value) || null })} />
                </ZHField>
              </>
            ) : (
              <>
                <ZHField density="compact" label={t("purchases.distributeCost.source", "Línea de transporte")}>
                  <ZhSelect density="compact" value={draft.sourceLineId ?? ""} disabled={!isNew}
                    onChange={(e) => patch({ sourceLineId: e.target.value, targetLineIds: [] })}>
                    {(isNew ? xmlSources : source ? [source] : []).map((l) => (
                      <option key={l.id} value={l.id}>{l.description}</option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField density="compact" label={t("purchases.distributeCost.sourceBase", "Base neta a distribuir")}>
                  <ZHMoneyValue value={source?.taxableBase ?? null} precision="money" />
                </ZHField>
              </>
            )}
          </div>
          <div className="table-scroll pdc-table-scroll">
            <table className="table table--compact responsive-table pdc-table">
              <thead>
                <tr>
                  <th className="pdc-table__item">{t("purchases.distributeCost.col.item", "Ítem")}</th>
                  <th className="zh-text-align-right">{t("purchases.distributeCost.col.quantity", "Cantidad")}</th>
                  <th className="zh-text-align-right">{t("purchases.distributeCost.col.subtotalBase", "Subtotal base")}</th>
                </tr>
              </thead>
              <tbody>
                {eligibleTargets.map((l) => (
                  <tr key={l.id} className={draft.targetLineIds.includes(l.id) ? undefined : "pdc-table__row--excluded"}>
                    <td className="pdc-table__item">
                      <label className="pdc-table__include">
                        <input type="checkbox" checked={draft.targetLineIds.includes(l.id)} onChange={() => toggleTarget(l.id)}
                          aria-label={`${t("purchases.distributeCost.col.include", "Incluir")}: ${l.description}`} />
                        <span className="pdc-table__desc" title={l.description}>{l.description}</span>
                      </label>
                    </td>
                    <td className="zh-table-cell--num" data-label={t("purchases.distributeCost.col.quantity", "Cantidad")}>
                      {formatMoney(l.quantity, quantityDecimals)}
                    </td>
                    <td className="zh-table-cell--num" data-label={t("purchases.distributeCost.col.subtotalBase", "Subtotal base")}>
                      <ZHMoneyValue value={l.taxableBase} precision="money" currencySymbol="" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {draftIssue && <ZHPageNotice variant="warning" message={draftIssue} />}
          <div className="pdc-toolbar">
            <ZHBtn variant="ghost" size="md" disabled={busy} onClick={() => setDraft(null)}>
              {t("common.cancel", "Cancelar")}
            </ZHBtn>
            <ZHBtn variant="primary" size="md" disabled={busy || !!draftIssue} onClick={() => void handleSave()}>
              {t("purchases.distributeCost.apply", "Aplicar")}
            </ZHBtn>
          </div>
        </section>
      )}
    </ZHModal>
  );
}
