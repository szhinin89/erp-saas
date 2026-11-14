import { useEffect, useMemo, useState } from "react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhCheckbox } from "../../../components/zh/inputs/ZhCheckbox";
import { useCompanyScopedAsync } from "../../../hooks/useCompanyScopedAsync";
import { useI18n } from "../../../i18n/i18n";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  SupplierExpenseCatalogItemDto,
  SupplierExpenseSubcategoriesDto,
  SupplierExpenseSubcategoryItemDto,
} from "../types/businessPartner.types";

const K = "masterdata.suppliers.expenseSubcategories.";

type Option = { id: string; label: string; unavailable?: boolean };
type Group = { key: string; title: string; options: Option[] };

/** Tipo › Categoría → Subcategoría del catálogo que entrega el GET; las asignadas que ya no están disponibles se conservan visibles. */
function buildGroups(
  catalog: SupplierExpenseCatalogItemDto[],
  assigned: SupplierExpenseSubcategoryItemDto[],
  unavailableLabel: string,
): Group[] {
  const groups = new Map<string, Group>();
  for (const item of catalog) {
    const title = `${item.typeName} › ${item.categoryName}`;
    const group = groups.get(title) ?? { key: title, title, options: [] };
    group.options.push({
      id: item.expenseSubcategoryId,
      label: `${item.code} — ${item.name}`,
    });
    groups.set(title, group);
  }
  const seen = new Set(catalog.map((i) => i.expenseSubcategoryId));
  const orphans = assigned.filter((i) => !seen.has(i.expenseSubcategoryId));
  const result = [...groups.values()];
  if (orphans.length > 0)
    result.push({
      key: "unavailable",
      title: unavailableLabel,
      options: orphans.map((i) => ({
        id: i.expenseSubcategoryId,
        label: `${i.code} — ${i.name}`,
        unavailable: true,
      })),
    });
  return result;
}

/**
 * EXPENSE-SUPPLIER-SUBCATEGORIES-01 — sección de la ficha del proveedor: subcategorías de gasto
 * permitidas en la empresa activa. Lectura con `…businesspartners.view`; edición con
 * `…configure-company`. Modo, disponibles y catálogo candidato vienen del `GET` (el catálogo solo para
 * quien configura); aquí solo se elige el conjunto y se envía con `PUT` (`[]` = «Quitar restricción»,
 * con confirmación).
 */
export function SupplierExpenseSubcategoriesSection({
  bpId,
  disabled,
}: {
  bpId: string;
  disabled?: boolean;
}) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("masterdata.businesspartners.view");
  const canEdit = canShow("masterdata.businesspartners.configure-company");

  const [override, setOverride] =
    useState<SupplierExpenseSubcategoriesDto | null>(null);
  // null = sin cambios del usuario: la selección es la configuración vigente del servidor.
  const [edits, setEdits] = useState<Set<string> | null>(null);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const config = useCompanyScopedAsync(
    () => businessPartnerFacade.getExpenseSubcategories(bpId),
    canView,
    [bpId],
  );
  const data = override ?? config.data;
  // El PUT no devuelve el catálogo: se conserva el del último GET. `null` = sin permiso de configurar.
  const catalog = config.data?.catalog ?? null;
  const editable = canEdit && catalog !== null;
  const activeIds = useMemo(
    () =>
      (data?.items ?? [])
        .filter((i) => i.isActive)
        .map((i) => i.expenseSubcategoryId),
    [data],
  );

  useEffect(() => {
    setOverride(null);
    setEdits(null);
  }, [bpId, config.data]);

  const selected = edits ?? new Set(activeIds);

  const groups = useMemo(
    () =>
      buildGroups(
        catalog ?? [],
        (data?.items ?? []).filter((i) => i.isActive),
        t(`${K}inactiveInCatalog`),
      ),
    [catalog, data, t],
  );

  if (!canView) return null;

  const dirty =
    selected.size !== activeIds.length ||
    activeIds.some((id) => !selected.has(id));
  const busy = saving || disabled;

  const apply = async (ids: string[], successKey: string) => {
    setSaving(true);
    setActionError(null);
    try {
      const updated = await businessPartnerFacade.setExpenseSubcategories(
        bpId,
        {
          expenseSubcategoryIds: ids,
        },
      );
      setOverride(updated);
      setEdits(null);
      message.success(t(successKey));
    } catch (err) {
      setActionError(
        formatApiRequestError(err, { generic: t(`${K}saveError`) }),
      );
    } finally {
      setSaving(false);
    }
  };

  const removeRestriction = async () => {
    const ok = await message.confirm({
      title: t(`${K}removeTitle`),
      message: t(`${K}removeMessage`),
      confirmLabel: t(`${K}removeConfirm`),
      cancelLabel: t(`${K}cancel`),
      variant: "warning",
    });
    if (ok) await apply([], `${K}removed`);
  };

  const save = () =>
    selected.size === 0
      ? removeRestriction()
      : apply([...selected], `${K}saved`);

  const toggle = (id: string, checked: boolean) =>
    setEdits(() => {
      const next = new Set(selected);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });

  const loadError = config.error
    ? `${t(`${K}loadError`)} ${config.error}`
    : null;

  return (
    <>
      <hr className="md-supplier-section-divider" />
      <div className="md-supplier-expense-subcategories">
        <h4>{t(`${K}title`)}</h4>
        <p className="md-supplier-section-hint">{t(`${K}hint`)}</p>
        {loadError && <ZHPageNotice variant="error" message={loadError} />}
        {actionError && <ZHPageNotice variant="error" message={actionError} />}

        {config.loading && !data ? (
          <p className="md-supplier-section-hint">{t(`${K}loading`)}</p>
        ) : data ? (
          <>
            <ZHPageNotice
              variant={data.mode === "RestrictedEmpty" ? "warning" : "info"}
              message={t(`${K}mode.${data.mode}`)}
            />
            {!editable ? (
              <ReadOnlyList
                items={data.items.filter((i) => i.isActive)}
                t={t}
              />
            ) : groups.length === 0 ? (
              <p className="md-supplier-section-hint">{t(`${K}empty`)}</p>
            ) : (
              groups.map((group) => (
                <fieldset
                  key={group.key}
                  className="md-supplier-expense-subcategories__group"
                >
                  <legend>{group.title}</legend>
                  {group.options.map((option) => (
                    <ZhCheckbox
                      key={option.id}
                      label={
                        option.unavailable
                          ? `${option.label} (${t(`${K}inactiveInCatalog`)})`
                          : option.label
                      }
                      checked={selected.has(option.id)}
                      disabled={busy}
                      onChange={(event) =>
                        toggle(option.id, event.target.checked)
                      }
                    />
                  ))}
                </fieldset>
              ))
            )}
            {editable && (
              <div className="md-supplier-expense-subcategories__actions">
                <ZHBtn
                  variant="primary"
                  size="sm"
                  disabled={busy || !dirty}
                  onClick={() => void save()}
                >
                  {saving ? t(`${K}saving`) : t(`${K}save`)}
                </ZHBtn>
                {activeIds.length > 0 && (
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    disabled={busy}
                    onClick={() => void removeRestriction()}
                  >
                    {t(`${K}remove`)}
                  </ZHBtn>
                )}
              </div>
            )}
          </>
        ) : null}
      </div>
    </>
  );
}

function ReadOnlyList({
  items,
  t,
}: {
  items: SupplierExpenseSubcategoryItemDto[];
  t: (key: string) => string;
}) {
  if (items.length === 0)
    return <p className="md-supplier-section-hint">{t(`${K}readOnlyEmpty`)}</p>;
  return (
    <ul className="md-supplier-expense-subcategories__readonly">
      {items.map((i) => (
        <li key={i.expenseSubcategoryId}>
          {i.typeName} › {i.categoryName} › {i.code} — {i.name}
          {!i.isSubcategoryActive && ` (${t(`${K}inactiveInCatalog`)})`}
        </li>
      ))}
    </ul>
  );
}
