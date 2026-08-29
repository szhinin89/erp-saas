import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhTextInput, ZhSelect } from "../../../components/zh/inputs";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { message } from "../../../lib/messages";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import "../../../styles/shared/items-catalog.css";

import { useItems } from "../hooks/useItems";
import { useItemTypeOptions } from "../hooks/useItemTypeOptions";
import { useItemUiStore } from "../store/itemUiStore";
import { ItemFormTabs } from "../components/ItemForm/ItemFormTabs";
import { ItemListTable } from "../components/ItemListTable";
import type { CreateItemFormValues } from "../schemas/createItemSchema";
import type { CreateItemRequest, UpdateItemRequest } from "../api/itemService";
import type { ItemDto } from "../../../types/items";

type TabId = "resumen" | "listado" | "nuevo";

const TABS: { id: TabId; labelKey: string; labelFb: string; icon: string }[] = [
  {
    id: "resumen",
    labelKey: "items.tabs.resumen",
    labelFb: "Resumen",
    icon: "bar_chart_4_bars",
  },
  {
    id: "listado",
    labelKey: "items.tabs.listado",
    labelFb: "Listado",
    icon: "view_list",
  },
  {
    id: "nuevo",
    labelKey: "items.tabs.nuevo",
    labelFb: "Nuevo Ítem",
    icon: "add_box",
  },
];

export function ItemsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("items.view");
  const canCreate = canShow("items.create");
  const canEdit = canShow("items.edit");

  const searchTerm = useItemUiStore((s) => s.searchTerm);
  const filterIsActive = useItemUiStore((s) => s.filterIsActive);
  const filterItemTypeId = useItemUiStore((s) => s.filterItemTypeId);
  const setSearchTerm = useItemUiStore((s) => s.setSearchTerm);
  const setFilterIsActive = useItemUiStore((s) => s.setFilterIsActive);
  const setFilterItemTypeId = useItemUiStore((s) => s.setFilterItemTypeId);

  const itemTypesState = useItemTypeOptions();
  const itemTypeFilterOptions = itemTypesState.data ?? [];

  const {
    items,
    loading,
    error,
    refetch,
    creating,
    createItem,
    updating,
    updateItem,
    toggling,
    toggleError,
    toggleStatus,
  } = useItems({
    search: searchTerm || undefined,
    isActive: filterIsActive,
    itemTypeId: filterItemTypeId,
    pageNumber: 1,
    pageSize: 50,
  });

  const activeTab = useItemUiStore((s) => s.activeTab);
  const editingItemId = useItemUiStore((s) => s.editingItemId);
  const readOnly = useItemUiStore((s) => s.readOnly);
  const setActiveTab = useItemUiStore((s) => s.setActiveTab);
  const startEdit = useItemUiStore((s) => s.startEdit);
  const startView = useItemUiStore((s) => s.startView);
  const cancelEdit = useItemUiStore((s) => s.cancelEdit);

  const handleSubmit = async (values: CreateItemFormValues): Promise<void> => {
    // baseSalePrice (SSOT, ADR-021) vive plano en el formulario — sin sección
    // intermedia "pricing" — y viaja siempre en el mismo payload que el resto del ítem.
    const { taxConfig, saleConfig, stockConfig, ...base } = values;
    const normalizedTaxConfig = {
      saleVatCode: normalizeOptionalCode(taxConfig.saleVatCode),
      purchaseVatCode: normalizeOptionalCode(taxConfig.purchaseVatCode),
      exciseTaxCode: normalizeOptionalCode(taxConfig.exciseTaxCode),
    };
    const flatPayload = {
      ...base,
      ...normalizedTaxConfig,
      ...saleConfig,
      ...stockConfig,
    };

    if (editingItemId) {
      // Edit mode: barcodes/supplierCodes no viajan en Update — se gestionan desde sus
      // propios endpoints. baseSalePrice sí viaja siempre — el schema Zod
      // (updateItemSchema) ya garantiza que es un número, nunca null/undefined, así el
      // backend no puede confundir "campo omitido" con "borrar el precio".
      const {
        barcodes: _barcodes,
        supplierCodes: _supplierCodes,
        ...updatePayload
      } = flatPayload;
      await updateItem({
        id: editingItemId,
        ...updatePayload,
      } as UpdateItemRequest);

      message.success(
        t("items.updated.success", "Ítem actualizado correctamente."),
      );
      cancelEdit();
      refetch();
      return;
    }

    // Create mode: barcodes/supplierCodes/precio base viajan en el mismo request —
    // el backend los persiste atómicamente junto con el ítem.
    await createItem(flatPayload as CreateItemRequest);

    message.success(t("items.created.success", "Ítem creado correctamente."));
    setActiveTab("listado");
  };

  // La carga del ItemDetailDto vive únicamente dentro de ItemFormTabs (useItemDetailPage) —
  // aquí solo se conoce el id de la fila seleccionada, nunca se vuelve a pedir el detalle.
  const handleEdit = (item: ItemDto) => {
    if (!canEdit) return;
    startEdit(item.id);
  };

  const handleView = (item: ItemDto) => {
    startView(item.id);
  };

  // CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05: afecta disponibilidad del ítem en ventas,
  // compras e inventario futuro — se confirma antes de ejecutar. toggleStatus ya bloquea doble
  // submit (`toggling`, deshabilita el botón en ItemListTable) y ya usa formatApiRequestError.
  const handleToggle = async (item: ItemDto) => {
    if (toggling) return;

    const confirmed = await message.confirm({
      title: item.isActive
        ? `Deshabilitar "${item.shortName}"`
        : `Habilitar "${item.shortName}"`,
      message: item.isActive
        ? `"${item.shortName}" dejará de estar disponible para nuevas operaciones (ventas, compras, inventario). El histórico y los movimientos existentes no se eliminan.`
        : `"${item.shortName}" volverá a estar disponible para operaciones futuras.`,
      variant: item.isActive ? "danger" : "warning",
      confirmLabel: item.isActive ? "Deshabilitar" : "Habilitar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    const ok = await toggleStatus(item.id, !item.isActive);
    if (ok) {
      message.success(
        item.isActive
          ? t("items.disabled.success", "Ítem deshabilitado.")
          : t("items.enabled.success", "Ítem habilitado."),
      );
    }
  };

  if (!canView) return <NoAccessPage title={t("items.title", "Productos")} />;

  const anyError = error || toggleError;

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.inventario", "Inventario")}
      title={t("items.title", "Productos")}
      action={
        canCreate ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => {
              cancelEdit();
              setActiveTab("nuevo");
            }}
          >
            <span className="material-symbols-outlined">add</span>
            {t("items.new", "Nuevo ítem")}
          </ZHBtn>
        ) : null
      }
    >
      {anyError && <ZHPageNotice variant="error" message={anyError} />}

      <ZHTabBar
        tabs={TABS.map((tab) => ({
          id: tab.id,
          label: t(tab.labelKey, tab.labelFb),
          icon: tab.icon,
        }))}
        activeTab={activeTab}
        onChange={setActiveTab}
      />

      {/* Tab panels */}
      <div className="prd-tab-content">
        {/* Resumen */}
        {activeTab === "resumen" && (
          <div className="prd-fadein pg-kpis">
            <ReportKpiCard
              label={t("items.summary.total", "Total ítems")}
              value={String(items.length)}
            />
            <ReportKpiCard
              label={t("items.summary.active", "Activos")}
              value={String(items.filter((i) => i.isActive).length)}
            />
            <ReportKpiCard
              label={t("items.summary.withLot", "Con lotes")}
              value={String(items.filter((i) => i.tracksLot).length)}
            />
          </div>
        )}

        {/* Listado */}
        {activeTab === "listado" && (
          <div className="prd-fadein">
            {/* Filters */}
            <div className="prd-filters-bar">
              <ZhTextInput
                className="zh-input prd-filters-bar__search"
                placeholder={t(
                  "items.list.search",
                  "Buscar por SKU o nombre...",
                )}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
              <ZhSelect
                className="zh-select"
                value={
                  filterIsActive === undefined ? "" : String(filterIsActive)
                }
                onChange={(e) =>
                  setFilterIsActive(
                    e.target.value === ""
                      ? undefined
                      : e.target.value === "true",
                  )
                }
              >
                <option value="">
                  {t("common.allStatuses", "Todos los estados")}
                </option>
                <option value="true">{t("common.active", "Activos")}</option>
                <option value="false">
                  {t("common.inactive", "Inactivos")}
                </option>
              </ZhSelect>
              <ZhSelect
                className="zh-select"
                value={filterItemTypeId ?? ""}
                onChange={(e) =>
                  setFilterItemTypeId(e.target.value || undefined)
                }
              >
                <option value="">
                  {t("common.allTypes", "Todos los tipos")}
                </option>
                {itemTypeFilterOptions.map((it) => (
                  <option key={it.id} value={it.id}>
                    {it.name}
                  </option>
                ))}
              </ZhSelect>
            </div>

            <ItemListTable
              items={items}
              loading={loading}
              t={t}
              onView={handleView}
              onEdit={handleEdit}
              onToggle={handleToggle}
              toggling={toggling}
            />
          </div>
        )}

        {/* Nuevo / Editar / Ver */}
        {activeTab === "nuevo" &&
          (canCreate || (editingItemId && (canEdit || readOnly))) && (
            <div className="prd-fadein prd-form-wrap">
              <ItemFormTabs
                submitting={creating || updating}
                itemId={editingItemId ?? undefined}
                disabled={readOnly}
                onSubmit={handleSubmit}
                onCancel={() => cancelEdit()}
              />
            </div>
          )}
      </div>
    </ErpPageTemplate>
  );
}
