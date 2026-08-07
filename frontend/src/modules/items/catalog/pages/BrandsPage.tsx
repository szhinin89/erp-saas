import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage, Badge } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { useCatalogCrud } from "../hooks/useCatalogCrud";
import { CatalogListSection } from "../components/CatalogListSection";
import { CatalogFormModal } from "../components/CatalogFormModal";
import {
  brandService,
  type BrandDto,
  type CreateBrandPayload,
  type UpdateBrandPayload,
} from "../api/catalogService";
import {
  brandSchema,
  emptyBrandForm,
  type BrandFormValues,
  type BrandFormInput,
} from "../schemas/catalogSchemas";

export function BrandsPage() {
  const ctx = useCatalogCrud<
    BrandDto,
    BrandFormValues,
    CreateBrandPayload,
    UpdateBrandPayload,
    [],
    BrandFormInput
  >({
    service: brandService,
    schema: brandSchema,
    emptyForm: emptyBrandForm,
    toCreatePayload: (f) => f,
    toUpdatePayload: (id, f) => ({ ...f, id }),
    toFormValues: (dto) => ({
      code: dto.code,
      name: dto.name,
      manufacturer: dto.manufacturer,
      countryOfOrigin: dto.countryOfOrigin,
    }),
    getId: (dto) => dto.id,
    permissionPrefix: "catalog",
  });

  if (!ctx.canView) return <NoAccessPage title="Marcas" />;

  return (
    <ErpPageTemplate kicker="Catálogo" title="Marcas">
      {ctx.error && (
        <ZHPageNotice variant="error" message="Error" detail={ctx.error} />
      )}
      <CatalogListSection
        ctx={ctx}
        title="Marcas Registradas"
        icon="sell"
        createLabel="Nueva Marca"
        searchPlaceholder="Buscar marcas..."
        columns={[
          {
            key: "code",
            label: "Código",
            render: (r) => (
              <Badge label={r.code as string} variant="neutral" className="mono" />
            ),
          },
          {
            key: "name",
            label: "Nombre",
            render: (r) => <strong>{r.name as string}</strong>,
          },
          {
            key: "manufacturer",
            label: "Fabricante",
            render: (r) => (
              <span className="subtle">
                {(r.manufacturer as string) || "—"}
              </span>
            ),
          },
        ]}
      />
      <CatalogFormModal ctx={ctx} entityLabel="Marca">
        <ZHGrid cols={2}>
          <ZHField label="Código *" required error={ctx.errors.code?.message}>
            <input
              className="zh-input mono"
              placeholder="SAMSUNG"
              disabled={ctx.saving || !!ctx.editingId}
              {...ctx.register("code")}
            />
          </ZHField>
          <ZHField label="Nombre *" required error={ctx.errors.name?.message}>
            <input
              className="zh-input"
              placeholder="Samsung Electronics"
              disabled={ctx.saving}
              {...ctx.register("name")}
            />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField label="Fabricante" error={ctx.errors.manufacturer?.message}>
            <input
              className="zh-input"
              placeholder="Samsung Corp."
              disabled={ctx.saving}
              {...ctx.register("manufacturer")}
            />
          </ZHField>
          <ZHField
            label="País de origen"
            error={ctx.errors.countryOfOrigin?.message}
          >
            <input
              className="zh-input"
              placeholder="Corea del Sur"
              disabled={ctx.saving}
              {...ctx.register("countryOfOrigin")}
            />
          </ZHField>
        </ZHGrid>
      </CatalogFormModal>
    </ErpPageTemplate>
  );
}
