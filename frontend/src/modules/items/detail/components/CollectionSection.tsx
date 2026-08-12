import { useState } from "react";
import { EmptyState } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../../components/zh/inputs/ZhTextInput";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import type {
  ItemImageDto,
  ItemUnitConversionDto,
  ItemSubstituteDto,
  ItemPackagingLevelDto,
  ItemSupplierCodeDto,
} from "../../../../types/items";

type T = (key: string, fallback?: string) => string;

export type PackagingLevelInput = {
  id?: string | null;
  name: string;
  level: number;
  baseQuantity: number;
  uomCode: string;
  barcode?: string | null;
  weight?: number | null;
  isBaseUnit?: boolean;
  isPurchaseDefault?: boolean;
  isSaleDefault?: boolean;
};

type PackagingUomOption = { code: string; name: string; abbrev: string | null };

interface BaseSectionProps {
  title: string;
  icon: string;
  emptyMessage: string;
  children: React.ReactNode;
  count: number;
}

function SectionWrapper({
  title,
  icon,
  emptyMessage,
  children,
  count,
}: BaseSectionProps) {
  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">
            {icon}
          </span>
          <span className="pg-section-label">
            {title} ({count})
          </span>
        </div>
      </div>
      {count === 0 ? (
        <div className="pg-pad-40">
          <EmptyState message={emptyMessage} />
        </div>
      ) : (
        children
      )}
    </div>
  );
}

export function ImagesSection({
  t,
  images,
  disabled = false,
  onDisable,
}: {
  t: T;
  images: ItemImageDto[];
  disabled?: boolean;
  onDisable: (id: string) => Promise<void>;
}) {
  const active = images.filter((i) => i.isActive);
  return (
    <SectionWrapper
      title={t("items.images.sectionTitle", "Imágenes")}
      icon="image"
      emptyMessage={t("items.images.empty", "No hay imágenes.")}
      count={active.length}
    >
      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>{t("items.images.col.storageObjectId", "ID Objeto")}</th>
              <th>{t("items.images.col.altText", "Alt Text")}</th>
              <th>{t("items.images.col.isMain", "Principal")}</th>
              <th>{t("items.images.col.isEcommerce", "eCommerce")}</th>
              <th>{t("items.images.col.sortOrder", "Orden")}</th>
              <th className="pg-th-right">{t("common.actions", "Acciones")}</th>
            </tr>
          </thead>
          <tbody>
            {active.map((img) => (
              <tr key={img.id}>
                <td>
                  <code>{img.storageObjectId.slice(0, 12)}…</code>
                </td>
                <td>{img.altText || "—"}</td>
                <td>
                  {img.isMain ? t("common.yes", "Sí") : t("common.no", "No")}
                </td>
                <td>
                  {img.isEcommerce
                    ? t("common.yes", "Sí")
                    : t("common.no", "No")}
                </td>
                <td>{img.sortOrder}</td>
                <td className="pg-td-right">
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    size="sm"
                    title={t("common.deactivate", "Desactivar")}
                    onClick={() => void onDisable(img.id)}
                    disabled={disabled}
                  >
                    <span className="material-symbols-outlined">block</span>
                  </ZHBtn>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}

export function UnitConversionsSection({
  t,
  conversions,
}: {
  t: T;
  conversions: ItemUnitConversionDto[];
}) {
  const active = conversions.filter((c) => c.isActive);
  return (
    <SectionWrapper
      title={t("items.conversions.sectionTitle", "Conversiones de Unidad")}
      icon="swap_horiz"
      emptyMessage={t(
        "items.conversions.empty",
        "No hay conversiones configuradas.",
      )}
      count={active.length}
    >
      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>{t("items.conversions.col.from", "Desde")}</th>
              <th>{t("items.conversions.col.to", "Hacia")}</th>
              <th>{t("items.conversions.col.factor", "Factor")}</th>
            </tr>
          </thead>
          <tbody>
            {active.map((c) => (
              <tr key={c.id}>
                <td>
                  <code title={c.fromUomCode}>{c.fromUomAbbrev}</code>
                </td>
                <td>
                  <code title={c.toUomCode}>{c.toUomAbbrev}</code>
                </td>
                <td>{c.factor}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}

export function SubstitutesSection({
  t,
  substitutes,
}: {
  t: T;
  substitutes: ItemSubstituteDto[];
}) {
  const active = substitutes.filter((s) => s.isActive);
  return (
    <SectionWrapper
      title={t("items.substitutes.sectionTitle", "Sustitutos")}
      icon="swap_calls"
      emptyMessage={t(
        "items.substitutes.empty",
        "No hay sustitutos configurados.",
      )}
      count={active.length}
    >
      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>{t("items.substitutes.col.itemId", "Item Sustituto ID")}</th>
              <th>{t("items.substitutes.col.priority", "Prioridad")}</th>
              <th>{t("items.substitutes.col.note", "Nota")}</th>
            </tr>
          </thead>
          <tbody>
            {active.map((s) => (
              <tr key={s.id}>
                <td>
                  <code>{s.substituteItemId.slice(0, 12)}…</code>
                </td>
                <td>{s.priority}</td>
                <td>{s.note || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}

type PackagingLevelDraft = {
  name: string;
  baseQuantity: string;
  uomCode: string;
  barcode: string;
  weight: string;
  isBaseUnit: boolean;
  isPurchaseDefault: boolean;
  isSaleDefault: boolean;
};

const emptyPackagingDraft: PackagingLevelDraft = {
  name: "",
  baseQuantity: "",
  uomCode: "",
  barcode: "",
  weight: "",
  isBaseUnit: false,
  isPurchaseDefault: false,
  isSaleDefault: false,
};

function toPackagingDraft(level: ItemPackagingLevelDto): PackagingLevelDraft {
  return {
    name: level.name,
    baseQuantity: String(level.baseQuantity),
    uomCode: level.uomCode,
    barcode: level.barcode ?? "",
    weight: level.weight != null ? String(level.weight) : "",
    isBaseUnit: level.isBaseUnit,
    isPurchaseDefault: level.isPurchaseDefault,
    isSaleDefault: level.isSaleDefault,
  };
}

function packagingNameSuggestsMultiplier(name: string, baseQuantity: string) {
  const qty = Number(baseQuantity);
  return qty === 1 && /\b[xX]\s*(?:[2-9]|\d{2,})\b/.test(name);
}

function buildPackagingPayload(
  rows: { id: string | null; draft: PackagingLevelDraft }[],
): PackagingLevelInput[] {
  return rows.map((r, index) => ({
    id: r.id && !r.id.startsWith("__") ? r.id : null,
    name: r.draft.name.trim(),
    level: index + 1,
    baseQuantity: Number(r.draft.baseQuantity),
    uomCode: r.draft.uomCode,
    barcode: r.draft.barcode.trim() ? r.draft.barcode.trim() : null,
    weight: r.draft.weight.trim() ? Number(r.draft.weight) : null,
    isBaseUnit: r.draft.isBaseUnit,
    isPurchaseDefault: r.draft.isPurchaseDefault,
    isSaleDefault: r.draft.isSaleDefault,
  }));
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim()
    ? error.message.trim()
    : fallback;
}

/** Fuerza que a lo sumo una fila quede marcada como unidad base, igual que exige el backend. */
function withBaseUnitExclusivity(
  rows: { id: string | null; draft: PackagingLevelDraft }[],
  keepId: string | null,
): { id: string | null; draft: PackagingLevelDraft }[] {
  const keep = rows.find((r) => r.id === keepId);
  if (!keep?.draft.isBaseUnit) return rows;
  return rows.map((r) =>
    r.id === keepId ? r : { ...r, draft: { ...r.draft, isBaseUnit: false } },
  );
}

export function PackagingLevelsSection({
  t,
  levels,
  uomOptions,
  baseUomCode,
  usedPackagingLevelIds,
  tracksStock,
  disabled = false,
  onSave,
}: {
  t: T;
  levels: ItemPackagingLevelDto[];
  uomOptions: PackagingUomOption[];
  baseUomCode: string;
  usedPackagingLevelIds: Set<string>;
  tracksStock: boolean;
  disabled?: boolean;
  onSave: (levels: PackagingLevelInput[]) => Promise<void>;
}) {
  const active = levels.filter((l) => l.isActive);
  const [adding, setAdding] = useState(false);
  const [addDraft, setAddDraft] = useState<PackagingLevelDraft>(
    emptyPackagingDraft,
  );
  const [addError, setAddError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<PackagingLevelDraft>(
    emptyPackagingDraft,
  );
  const [editError, setEditError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const onlyBaseUnitId =
    active.filter((level) => level.isBaseUnit).length === 1
      ? active.find((level) => level.isBaseUnit)?.id
      : null;

  const buildBaseUnitDraft = (): PackagingLevelDraft => {
    const baseUom = uomOptions.find((u) => u.code === baseUomCode);
    return {
      ...emptyPackagingDraft,
      name: `${(baseUom?.name ?? baseUom?.abbrev ?? "UNIDAD").toUpperCase()} X1`,
      baseQuantity: "1",
      uomCode: baseUomCode,
      isBaseUnit: true,
    };
  };

  const validate = (
    draft: PackagingLevelDraft,
    excludeId: string | null,
  ): string | null => {
    if (!draft.name.trim())
      return t("items.packaging.errors.nameRequired", "El nombre es obligatorio.");
    if (!draft.uomCode)
      return t(
        "items.packaging.errors.uomRequired",
        "Selecciona una unidad de empaque.",
      );
    const qty = Number(draft.baseQuantity);
    if (!draft.baseQuantity.trim() || Number.isNaN(qty) || qty <= 0)
      return t(
        "items.packaging.errors.baseQuantityPositive",
        "La cantidad base debe ser mayor a 0.",
      );
    if (draft.isBaseUnit && qty !== 1)
      return t(
        "items.packaging.errors.baseUnitMustBeOne",
        "La unidad base debe tener cantidad 1.",
      );
    const duplicate = active
      .filter((l) => l.id !== excludeId)
      .some((l) => l.uomCode === draft.uomCode && l.baseQuantity === qty);
    if (duplicate)
      return t(
        "items.packaging.errors.duplicateUomQuantity",
        "Ya existe una presentación con esa unidad y cantidad.",
      );
    return null;
  };

  const validateRows = (
    rows: { id: string | null; draft: PackagingLevelDraft }[],
  ): string | null => {
    const baseCount = rows.filter((r) => r.draft.isBaseUnit).length;
    if (tracksStock && baseCount !== 1)
      return t(
        "items.packaging.errors.baseUnitRequired",
        "Debe existir una presentación base, por ejemplo UNIDAD X1 con cantidad base 1.",
      );
    if (!tracksStock && baseCount > 1)
      return t(
        "items.packaging.errors.baseUnitMaxOne",
        "No puede existir más de una presentación marcada como unidad base.",
      );
    return null;
  };

  const handleAdd = async () => {
    const error = validate(addDraft, null);
    if (error) {
      setAddError(error);
      return;
    }
    const rows = withBaseUnitExclusivity(
      [
        ...active.map((l) => ({ id: l.id, draft: toPackagingDraft(l) })),
        { id: "__new__", draft: addDraft },
      ],
      "__new__",
    );
    const rowsError = validateRows(rows);
    if (rowsError) {
      setAddError(rowsError);
      return;
    }
    setBusy(true);
    setSaveError(null);
    try {
      await onSave(buildPackagingPayload(rows));
      setAdding(false);
      setAddDraft(emptyPackagingDraft);
      setAddError(null);
    } catch (err) {
      setAddError(
        getErrorMessage(
          err,
          t("items.packaging.errors.saveFailed", "No se pudo guardar el empaque."),
        ),
      );
    } finally {
      setBusy(false);
    }
  };

  const startEdit = (level: ItemPackagingLevelDto) => {
    setEditingId(level.id);
    setEditDraft(toPackagingDraft(level));
    setEditError(null);
  };

  const handleEditSave = async (levelId: string) => {
    const error = validate(editDraft, levelId);
    if (error) {
      setEditError(error);
      return;
    }
    const rows = withBaseUnitExclusivity(
      active.map((l) => ({
        id: l.id,
        draft: l.id === levelId ? editDraft : toPackagingDraft(l),
      })),
      levelId,
    );
    const rowsError = validateRows(rows);
    if (rowsError) {
      setEditError(rowsError);
      return;
    }
    setBusy(true);
    setSaveError(null);
    try {
      await onSave(buildPackagingPayload(rows));
      setEditingId(null);
      setEditError(null);
    } catch (err) {
      setEditError(
        getErrorMessage(
          err,
          t("items.packaging.errors.saveFailed", "No se pudo guardar el empaque."),
        ),
      );
    } finally {
      setBusy(false);
    }
  };

  const handleRemove = async (levelId: string) => {
    const rows = active
      .filter((l) => l.id !== levelId)
      .map((l) => ({ id: l.id, draft: toPackagingDraft(l) }));
    const rowsError = validateRows(rows);
    if (rowsError) {
      setSaveError(rowsError);
      return;
    }
    setBusy(true);
    setSaveError(null);
    try {
      await onSave(buildPackagingPayload(rows));
    } catch (err) {
      setSaveError(
        getErrorMessage(
          err,
          t("items.packaging.errors.saveFailed", "No se pudo guardar el empaque."),
        ),
      );
    } finally {
      setBusy(false);
    }
  };

  const actionsDisabled = disabled || busy;
  const addWarning = packagingNameSuggestsMultiplier(
    addDraft.name,
    addDraft.baseQuantity,
  )
    ? t(
        "items.packaging.warnings.nameSuggestsMultiplier",
        "El nombre sugiere una presentación múltiple, pero la cantidad base es 1. Revise el factor; no se infiere automáticamente.",
      )
    : null;
  const editWarning = packagingNameSuggestsMultiplier(
    editDraft.name,
    editDraft.baseQuantity,
  )
    ? t(
        "items.packaging.warnings.nameSuggestsMultiplier",
        "El nombre sugiere una presentación múltiple, pero la cantidad base es 1. Revise el factor; no se infiere automáticamente.",
      )
    : null;

  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">
            inventory_2
          </span>
          <span className="pg-section-label">
            {t("items.packaging.sectionTitle", "Niveles de Empaque")} (
            {active.length})
          </span>
        </div>
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={actionsDisabled || adding || editingId !== null}
          onClick={() => {
            setAdding(true);
            setAddDraft(emptyPackagingDraft);
            setAddError(null);
            setSaveError(null);
          }}
        >
          {t("items.packaging.add", "Agregar empaque")}
        </ZHBtn>
      </div>

      <p className="zh-field-hint">
        {t(
          "items.packaging.hint",
          "El inventario vive en la unidad base. Las presentaciones convierten compras/ventas a esa unidad. Ejemplo: 1 PACA x12 = 12 UNIDADES.",
        )}
      </p>

      {active.length === 0 && !adding && (
        <div className="pg-pad-40">
          <EmptyState
            message={t(
              "items.packaging.empty",
              "No hay niveles de empaque configurados. Cree primero una presentación base UNIDAD X1 y luego agregue PACA x12 o CAJA x24.",
            )}
          />
          <ZHBtn
            type="button"
            variant="secondary"
            size="sm"
            disabled={actionsDisabled || !baseUomCode}
            onClick={() => {
              setAdding(true);
              setAddDraft(buildBaseUnitDraft());
              setAddError(null);
              setSaveError(null);
            }}
          >
            {t("items.packaging.createBaseUnit", "Crear UNIDAD X1")}
          </ZHBtn>
        </div>
      )}

      {saveError && (
        <p className="zh-field-hint zh-field-hint--error">{saveError}</p>
      )}

      {(active.length > 0 || adding) && (
        <div className="table-scroll">
          <table className="table">
            <thead>
              <tr>
                <th>{t("items.packaging.col.name", "Nombre")}</th>
                <th>{t("items.packaging.col.uom", "UOM")}</th>
                <th>{t("items.packaging.col.baseQuantity", "Cantidad Base")}</th>
                <th>{t("items.packaging.col.barcode", "Barcode")}</th>
                <th>{t("items.packaging.col.weight", "Peso")}</th>
                <th>{t("items.packaging.col.isBaseUnit", "Unidad Base")}</th>
                <th>{t("items.packaging.col.isPurchaseDefault", "Compra")}</th>
                <th>{t("items.packaging.col.isSaleDefault", "Venta")}</th>
                <th className="pg-th-right">
                  {t("common.actions", "Acciones")}
                </th>
              </tr>
            </thead>
            <tbody>
              {active.map((l) =>
                editingId === l.id ? (
                  <tr key={l.id}>
                    <td>
                      <ZhTextInput
                        density="compact"
                        value={editDraft.name}
                        onChange={(e) =>
                          setEditDraft((d) => ({ ...d, name: e.target.value }))
                        }
                        disabled={busy}
                      />
                    </td>
                    <td>
                      <ZhSelect
                        value={editDraft.uomCode}
                        disabled={busy}
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            uomCode: e.target.value,
                          }))
                        }
                      >
                        <option value="">
                          {t("common.selectOption", "— Seleccionar —")}
                        </option>
                        {uomOptions.map((u) => (
                          <option key={u.code} value={u.code}>
                            {u.code} — {u.name}
                          </option>
                        ))}
                      </ZhSelect>
                    </td>
                    <td>
                      <ZhDecimalInput
                        density="compact"
                        decimals={4}
                        positiveOnly
                        value={editDraft.baseQuantity}
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            baseQuantity: e.target.value,
                          }))
                        }
                        disabled={busy}
                      />
                    </td>
                    <td>
                      <ZhTextInput
                        density="compact"
                        value={editDraft.barcode}
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            barcode: e.target.value,
                          }))
                        }
                        disabled={busy}
                      />
                    </td>
                    <td>
                      <ZhDecimalInput
                        density="compact"
                        decimals={3}
                        positiveOnly
                        value={editDraft.weight}
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            weight: e.target.value,
                          }))
                        }
                        disabled={busy}
                      />
                    </td>
                    <td>
                      <input
                        type="checkbox"
                        checked={editDraft.isBaseUnit}
                        disabled={
                          busy || (onlyBaseUnitId === l.id && editDraft.isBaseUnit)
                        }
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            isBaseUnit: e.target.checked,
                          }))
                        }
                      />
                    </td>
                    <td>
                      <input
                        type="checkbox"
                        checked={editDraft.isPurchaseDefault}
                        disabled={busy}
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            isPurchaseDefault: e.target.checked,
                          }))
                        }
                      />
                    </td>
                    <td>
                      <input
                        type="checkbox"
                        checked={editDraft.isSaleDefault}
                        disabled={busy}
                        onChange={(e) =>
                          setEditDraft((d) => ({
                            ...d,
                            isSaleDefault: e.target.checked,
                          }))
                        }
                      />
                    </td>
                    <td className="pg-td-right">
                      <div className="items-row-actions">
                        <ZHBtn
                          type="button"
                          variant="primary"
                          size="sm"
                          disabled={busy}
                          onClick={() => void handleEditSave(l.id)}
                        >
                          {t("common.save", "Guardar")}
                        </ZHBtn>
                        <ZHBtn
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={busy}
                          onClick={() => setEditingId(null)}
                        >
                          {t("common.cancel", "Cancelar")}
                        </ZHBtn>
                      </div>
                      {editError && (
                        <p className="zh-field-hint zh-field-hint--error">
                          {editError}
                        </p>
                      )}
                      {!editError && editWarning && (
                        <p className="zh-field-hint zh-field-hint--warning">
                          {editWarning}
                        </p>
                      )}
                    </td>
                  </tr>
                ) : (
                  <tr key={l.id}>
                    <td>
                      <strong>{l.name}</strong>
                      {packagingNameSuggestsMultiplier(l.name, String(l.baseQuantity)) && (
                        <p className="zh-field-hint zh-field-hint--warning">
                          {t(
                            "items.packaging.warnings.nameSuggestsMultiplier",
                            "El nombre sugiere una presentación múltiple, pero la cantidad base es 1. Revise el factor; no se infiere automáticamente.",
                          )}
                        </p>
                      )}
                    </td>
                    <td>
                      <code title={l.uomCode}>{l.uomAbbrev}</code>
                    </td>
                    <td>{l.baseQuantity}</td>
                    <td>{l.barcode ? <code>{l.barcode}</code> : "—"}</td>
                    <td>{l.weight ?? "—"}</td>
                    <td>
                      {l.isBaseUnit
                        ? t("common.yes", "Sí")
                        : t("common.no", "No")}
                    </td>
                    <td>
                      {l.isPurchaseDefault
                        ? t("common.yes", "Sí")
                        : t("common.no", "No")}
                    </td>
                    <td>
                      {l.isSaleDefault
                        ? t("common.yes", "Sí")
                        : t("common.no", "No")}
                    </td>
                    <td className="pg-td-right">
                      <div className="items-row-actions">
                        <ZHBtn
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={actionsDisabled || editingId !== null || adding}
                          onClick={() => startEdit(l)}
                        >
                          {t("common.edit", "Editar")}
                        </ZHBtn>
                        <ZHBtn
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={
                            actionsDisabled ||
                            editingId !== null ||
                            adding ||
                            usedPackagingLevelIds.has(l.id)
                          }
                          title={
                            usedPackagingLevelIds.has(l.id)
                              ? t(
                                  "items.packaging.inUse",
                                  "En uso por un código de proveedor; no se puede eliminar.",
                                )
                              : undefined
                          }
                          onClick={() => void handleRemove(l.id)}
                        >
                          {t("common.remove", "Quitar")}
                        </ZHBtn>
                      </div>
                    </td>
                  </tr>
                ),
              )}
              {adding && (
                <tr>
                  <td>
                    <ZhTextInput
                      density="compact"
                      placeholder={t(
                        "items.packaging.namePlaceholder",
                        "PACA x12",
                      )}
                      value={addDraft.name}
                      onChange={(e) =>
                        setAddDraft((d) => ({ ...d, name: e.target.value }))
                      }
                      disabled={busy}
                    />
                  </td>
                  <td>
                    <ZhSelect
                      value={addDraft.uomCode}
                      disabled={busy}
                      onChange={(e) =>
                        setAddDraft((d) => ({ ...d, uomCode: e.target.value }))
                      }
                    >
                      <option value="">
                        {t("common.selectOption", "— Seleccionar —")}
                      </option>
                      {uomOptions.map((u) => (
                        <option key={u.code} value={u.code}>
                          {u.code} — {u.name}
                        </option>
                      ))}
                    </ZhSelect>
                  </td>
                  <td>
                    <ZhDecimalInput
                      density="compact"
                      decimals={4}
                      positiveOnly
                      value={addDraft.baseQuantity}
                      onChange={(e) =>
                        setAddDraft((d) => ({
                          ...d,
                          baseQuantity: e.target.value,
                        }))
                      }
                      disabled={busy}
                    />
                  </td>
                  <td>
                    <ZhTextInput
                      density="compact"
                      value={addDraft.barcode}
                      onChange={(e) =>
                        setAddDraft((d) => ({
                          ...d,
                          barcode: e.target.value,
                        }))
                      }
                      disabled={busy}
                    />
                  </td>
                  <td>
                    <ZhDecimalInput
                      density="compact"
                      decimals={3}
                      positiveOnly
                      value={addDraft.weight}
                      onChange={(e) =>
                        setAddDraft((d) => ({ ...d, weight: e.target.value }))
                      }
                      disabled={busy}
                    />
                  </td>
                  <td>
                    <input
                      type="checkbox"
                      checked={addDraft.isBaseUnit}
                      disabled={busy}
                      onChange={(e) =>
                        setAddDraft((d) => ({
                          ...d,
                          isBaseUnit: e.target.checked,
                        }))
                      }
                    />
                  </td>
                  <td>
                    <input
                      type="checkbox"
                      checked={addDraft.isPurchaseDefault}
                      disabled={busy}
                      onChange={(e) =>
                        setAddDraft((d) => ({
                          ...d,
                          isPurchaseDefault: e.target.checked,
                        }))
                      }
                    />
                  </td>
                  <td>
                    <input
                      type="checkbox"
                      checked={addDraft.isSaleDefault}
                      disabled={busy}
                      onChange={(e) =>
                        setAddDraft((d) => ({
                          ...d,
                          isSaleDefault: e.target.checked,
                        }))
                      }
                    />
                  </td>
                  <td className="pg-td-right">
                    <div className="items-row-actions">
                      <ZHBtn
                        type="button"
                        variant="primary"
                        size="sm"
                        disabled={busy}
                        onClick={() => void handleAdd()}
                      >
                        {t("common.save", "Guardar")}
                      </ZHBtn>
                      <ZHBtn
                        type="button"
                        variant="ghost"
                        size="sm"
                        disabled={busy}
                        onClick={() => {
                          setAdding(false);
                          setAddError(null);
                        }}
                      >
                        {t("common.cancel", "Cancelar")}
                      </ZHBtn>
                    </div>
                    {addError && (
                      <p className="zh-field-hint zh-field-hint--error">
                        {addError}
                      </p>
                    )}
                    {!addError && addWarning && (
                      <p className="zh-field-hint zh-field-hint--warning">
                        {addWarning}
                      </p>
                    )}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function SupplierCodesDetailSection({
  t,
  supplierCodes,
  packagingLevels,
  baseUomAbbrev,
  disabled = false,
  onUpdatePresentation,
}: {
  t: T;
  supplierCodes: ItemSupplierCodeDto[];
  packagingLevels: ItemPackagingLevelDto[];
  baseUomAbbrev: string;
  disabled?: boolean;
  onUpdatePresentation: (
    supplierId: string,
    code: string,
    packagingLevelId: string | null,
  ) => Promise<void>;
}) {
  const active = supplierCodes.filter((s) => s.isActive);
  const activePackaging = packagingLevels.filter((p) => p.isActive);
  const unnamedSupplierLabel = t(
    "items.supplierCodes.unnamedSupplier",
    "Proveedor sin nombre",
  );
  const identificationPrefix = t("items.supplierCodes.identificationPrefix", "RUC");
  const packagingLabel = (id: string | null) => {
    const packaging = activePackaging.find((p) => p.id === id);
    return packaging
      ? `${packaging.name} × ${packaging.baseQuantity} ${baseUomAbbrev}`
      : t("items.supplierCodes.noPresentation", "Sin presentación");
  };
  const supplierDisplay = (supplier: ItemSupplierCodeDto) => {
    const displayName = supplier.supplierDisplayName?.trim();
    const identification = supplier.supplierIdentification?.trim();
    return {
      primary: displayName || identification || unnamedSupplierLabel,
      secondary: displayName && identification ? identification : null,
    };
  };

  return (
    <SectionWrapper
      title={t("items.supplierCodes.detailTitle", "Códigos de proveedor")}
      icon="local_shipping"
      emptyMessage={t(
        "items.supplierCodes.empty",
        "No hay códigos de proveedor configurados.",
      )}
      count={active.length}
    >
      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>{t("items.supplierCodes.col.supplier", "Proveedor")}</th>
              <th>{t("items.supplierCodes.col.code", "Código")}</th>
              <th>{t("items.supplierCodes.col.primary", "Principal")}</th>
              <th>{t("items.supplierCodes.col.presentation", "Presentación")}</th>
            </tr>
          </thead>
          <tbody>
            {active.map((s) => (
              <tr key={s.id}>
                <td>
                  {(() => {
                    const supplier = supplierDisplay(s);
                    return (
                      <>
                        <strong>{supplier.primary}</strong>
                        {supplier.secondary && (
                          <p className="zh-field-hint">
                            {identificationPrefix}: {supplier.secondary}
                          </p>
                        )}
                      </>
                    );
                  })()}
                </td>
                <td>
                  <code>{s.code}</code>
                </td>
                <td>
                  {s.isPrimary ? t("common.yes", "Sí") : t("common.no", "No")}
                </td>
                <td>
                  <ZhSelect
                    value={s.packagingLevelId ?? ""}
                    aria-label={t(
                      "items.supplierCodes.presentationSelect",
                      "Presentación del código proveedor",
                    )}
                    disabled={disabled || activePackaging.length === 0 || !s.supplierId}
                    onChange={(event) =>
                      void onUpdatePresentation(
                        s.supplierId ?? "",
                        s.code,
                        event.target.value || null,
                      )
                    }
                  >
                    <option value="">
                      {t("items.supplierCodes.noPresentation", "Sin presentación")}
                    </option>
                    {activePackaging.map((p) => (
                      <option key={p.id} value={p.id}>
                        {packagingLabel(p.id)}
                      </option>
                    ))}
                  </ZhSelect>
                  {activePackaging.length > 0 && !s.packagingLevelId && (
                    <p className="zh-field-hint zh-field-hint--warning">
                      {t(
                        "items.supplierCodes.presentationMissingWarning",
                        "Sin presentación vinculada; una compra XML inventariable se bloqueará al confirmar.",
                      )}
                    </p>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}
