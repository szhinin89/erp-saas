import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage, Badge } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { useCatalogCrud } from "../hooks/useCatalogCrud";
import { CatalogListSection } from "../components/CatalogListSection";
import { CatalogFormModal } from "../components/CatalogFormModal";
import {
  attributeGroupService,
  type AttributeGroupDto,
  type CreateAttributeGroupPayload,
  type UpdateAttributeGroupPayload,
} from "../api/catalogService";
import {
  attributeGroupSchema,
  emptyAttributeGroupForm,
  type AttributeGroupFormValues,
  type AttributeGroupFormInput,
} from "../schemas/catalogSchemas";

export function AttributeGroupsPage() {
  const ctx = useCatalogCrud<
    AttributeGroupDto,
    AttributeGroupFormValues,
    CreateAttributeGroupPayload,
    UpdateAttributeGroupPayload,
    [],
    AttributeGroupFormInput
  >({
    service: attributeGroupService,
    schema: attributeGroupSchema,
    emptyForm: emptyAttributeGroupForm,
    toCreatePayload: (f) => f,
    toUpdatePayload: (id, f) => ({ ...f, id }),
    toFormValues: (dto) => ({
      code: dto.code,
      name: dto.name,
      sortOrder: dto.sortOrder,
    }),
    getId: (dto) => dto.id,
    permissionPrefix: "catalog",
  });

  if (!ctx.canView) return <NoAccessPage title="Grupos de Atributos" />;

  return (
    <ErpPageTemplate kicker="Catálogo" title="Grupos de Atributos">
      {ctx.error && (
        <ZHPageNotice variant="error" message="Error" detail={ctx.error} />
      )}
      <CatalogListSection
        ctx={ctx}
        title="Grupos Registrados"
        icon="tune"
        createLabel="Nuevo Grupo"
        searchPlaceholder="Buscar grupos..."
        columns={[
          {
            key: "code",
            label: "Código",
            render: (r) => (
              <Badge label={r.code as string} variant="gray" className="mono" />
            ),
          },
          {
            key: "name",
            label: "Nombre",
            render: (r) => <strong>{r.name as string}</strong>,
          },
          {
            key: "sortOrder",
            label: "Orden",
            render: (r) => <span>{r.sortOrder as number}</span>,
          },
        ]}
      />
      <CatalogFormModal ctx={ctx} entityLabel="Grupo de Atributos">
        <ZHGrid cols={2}>
          <ZHField label="Código *" required error={ctx.errors.code?.message}>
            <input
              className="zh-input mono"
              placeholder="DIM"
              disabled={ctx.saving || !!ctx.editingId}
              {...ctx.register("code")}
            />
          </ZHField>
          <ZHField label="Nombre *" required error={ctx.errors.name?.message}>
            <input
              className="zh-input"
              placeholder="Dimensiones"
              disabled={ctx.saving}
              {...ctx.register("name")}
            />
          </ZHField>
        </ZHGrid>
        <ZHField label="Orden" error={ctx.errors.sortOrder?.message}>
          <input
            type="number"
            className="zh-input"
            min={0}
            disabled={ctx.saving}
            {...ctx.register("sortOrder", { valueAsNumber: true })}
          />
        </ZHField>
      </CatalogFormModal>
    </ErpPageTemplate>
  );
}
