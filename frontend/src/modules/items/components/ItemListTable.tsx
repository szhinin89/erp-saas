import type { ItemDto } from "../../../types/items";
import { Badge } from "../../../components/PageShell";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";

type Props = {
  items: ItemDto[];
  loading: boolean;
  t: (key: string, fallback?: string) => string;
  onView: (item: ItemDto) => void;
  onEdit: (item: ItemDto) => void;
  onToggle: (item: ItemDto) => void;
  toggling: boolean;
};

export function ItemListTable({
  items,
  loading,
  t,
  onView,
  onEdit,
  onToggle,
  toggling,
}: Props) {
  if (loading) {
    return (
      <p className="loading-state">{t("common.loading", "Cargando...")}</p>
    );
  }

  if (items.length === 0) {
    return (
      <div className="empty-state">
        <p>{t("items.emptyFilteredTitle", "No se encontraron ítems.")}</p>
        <p>
          {t(
            "items.emptyFilteredMessage",
            "Cambie la búsqueda, estado o tipo para ver más resultados.",
          )}
        </p>
      </div>
    );
  }

  const columns: ZHDataTableColumn<ItemDto>[] = [
    { key: "sku", header: t("items.list.col.sku", "SKU"), render: (item) => <code className="prd-sku">{item.sku}</code> },
    {
      key: "name",
      header: t("items.list.col.name", "Nombre"),
      render: (item) => (
        <>
          <div className="prd-name">{item.shortName}</div>
          <div className="prd-desc-subtle">{item.description}</div>
        </>
      ),
    },
    { key: "type", header: t("items.list.col.type", "Tipo"), render: (item) => <span title={item.itemTypeName}>{item.itemTypeName}</span> },
    { key: "uom", header: t("items.list.col.uom", "UOM"), render: (item) => <code title={item.defaultUomCode}>{item.defaultUomAbbrev}</code> },
    {
      key: "flags",
      header: t("items.list.col.flags", "Flags"),
      render: (item) => (
        <div className="prd-badge-wrap">
          {item.tracksLot && (
            <Badge label={t("items.flags.lot", "LOT")} variant="info" size="md" title={t("items.flags.lotTitle", "Lotes")} />
          )}
          {item.tracksSeries && (
            <Badge label={t("items.flags.series", "SER")} variant="info" size="md" title={t("items.flags.seriesTitle", "Series")} />
          )}
          {item.isForSale && (
            <Badge label={t("items.flags.sale", "VENTA")} variant="success" size="md" title={t("items.flags.saleTitle", "En venta")} />
          )}
          {item.isEcommerceActive && (
            <Badge label={t("items.flags.ecommerce", "EC")} variant="neutral" size="md" title={t("items.flags.ecommerceTitle", "eCommerce")} />
          )}
        </div>
      ),
    },
    {
      key: "status",
      header: t("common.status", "Estado"),
      render: (item) => (
        <Badge
          label={item.isActive ? t("common.active", "Activo") : t("common.inactive", "Inactivo")}
          variant={item.isActive ? "success" : "neutral"}
        />
      ),
    },
    {
      key: "actions",
      header: t("common.actions", "Acciones"),
      render: (item) => (
        <div className="prd-row-actions">
          <ZHIconButton icon="visibility" variant="ghost" onClick={() => onView(item)} title={t("common.viewDetail", "Ver detalle")} />
          <ZHIconButton icon="edit" variant="ghost" onClick={() => onEdit(item)} title={t("common.edit", "Editar")} />
          <ZHIconButton
            icon={item.isActive ? "toggle_on" : "toggle_off"}
            variant="ghost"
            onClick={() => onToggle(item)}
            disabled={toggling}
            title={item.isActive ? t("common.disable", "Deshabilitar") : t("common.enable", "Habilitar")}
          />
        </div>
      ),
    },
  ];

  return (
    <ZHDataTable
      columns={columns}
      rows={items}
      rowKey={(item) => item.id}
      showRowNumber
      rowClassName={(item) => (item.isActive ? undefined : "prd-row--inactive")}
    />
  );
}
