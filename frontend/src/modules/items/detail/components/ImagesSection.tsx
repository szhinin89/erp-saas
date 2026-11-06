import { useState } from "react";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { Badge } from "../../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import type { ItemImageDto } from "../../../../types/items";

type Filter = "active" | "inactive" | "all";

export function ImagesSection({
  t, images, disabled = false, pending = false, error, onDisable, onEnable,
}: {
  t: (key: string, fallback?: string) => string;
  images: ItemImageDto[];
  disabled?: boolean;
  pending?: boolean;
  error?: string;
  onDisable: (id: string) => Promise<void>;
  onEnable: (id: string) => Promise<void>;
}) {
  const [filter, setFilter] = useState<Filter>("active");
  const { canShow } = usePermissionsUi();
  const canEdit = canShow("items.edit") && !disabled;
  const rows = images
    .filter((img) => filter === "all" || img.isActive === (filter === "active"))
    .sort((a, b) => a.sortOrder - b.sortOrder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  const columns: ZHDataTableColumn<ItemImageDto>[] = [
    { key: "storageObjectId", header: t("items.images.col.storageObjectId", "ID Objeto"),
      render: (img) => <code>{img.storageObjectId.slice(0, 12)}…</code> },
    { key: "altText", header: t("items.images.col.altText", "Alt Text"), render: (img) => img.altText || "—" },
    { key: "isActive", header: t("items.images.col.status"), render: (img) => (
      <Badge label={t(img.isActive ? "items.images.status.active" : "items.images.status.inactive")}
        variant={img.isActive ? "green" : "gray"} />
    ) },
    { key: "isMain", header: t("items.images.col.isMain", "Principal"),
      render: (img) => t(img.isMain ? "common.yes" : "common.no") },
    { key: "isEcommerce", header: t("items.images.col.isEcommerce", "eCommerce"),
      render: (img) => t(img.isEcommerce ? "common.yes" : "common.no") },
    { key: "sortOrder", header: t("items.images.col.sortOrder", "Orden"), render: (img) => img.sortOrder },
    { key: "actions", header: t("common.actions", "Acciones"), align: "right", render: (img) => (
      img.isActive || canEdit ? (
        <ZHBtn type="button" variant="ghost" size="sm"
          title={t(img.isActive ? "common.deactivate" : "items.images.activate", img.isActive ? "Desactivar" : "Activar")}
          aria-label={t(img.isActive ? "common.deactivate" : "items.images.activate", img.isActive ? "Desactivar" : "Activar")}
          onClick={() => void (img.isActive ? onDisable(img.id) : onEnable(img.id))}
          disabled={!canEdit || pending}>
          <span className="material-symbols-outlined">{img.isActive ? "block" : "check_circle"}</span>
        </ZHBtn>
      ) : null
    ) },
  ];
  return (
    <div className="pg-section" aria-busy={pending}>
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">image</span>
          <span className="pg-section-label">{t("items.images.sectionTitle", "Imágenes")} ({rows.length})</span>
        </div>
      </div>
      <ZHField label={t("items.images.filter.label")} hint={t("items.images.activate.hint")}>
        <ZhSelect aria-label={t("items.images.filter.label")} value={filter} disabled={pending}
          onChange={(event) => setFilter(event.target.value as Filter)}>
          <option value="active">{t("items.images.filter.active")}</option>
          <option value="inactive">{t("items.images.filter.inactive")}</option>
          <option value="all">{t("items.images.filter.all")}</option>
        </ZhSelect>
      </ZHField>
      {error && <ZHPageNotice variant="error" message={error} />}
      <ZHDataTable columns={columns} rows={rows} rowKey={(img) => img.id}
        emptyMessage={t("items.images.empty", "No hay imágenes.")} />
    </div>
  );
}
