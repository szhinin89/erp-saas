import { useCallback, useEffect, useState } from "react";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage, Badge } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhSelect } from "../../../../components/zh/inputs";
import { useCatalogCrud } from "../hooks/useCatalogCrud";
import { CatalogListSection } from "../components/CatalogListSection";
import { CatalogFormModal } from "../components/CatalogFormModal";
import {
  attributeDefinitionService,
  attributeGroupService,
  type AttributeDefinitionDto,
  type AttributeGroupDto,
  type CreateAttributeDefinitionPayload,
  type UpdateAttributeDefinitionPayload,
} from "../api/catalogService";
import {
  attributeDefinitionSchema,
  emptyAttributeDefinitionForm,
  type AttributeDefinitionFormValues,
  type AttributeDefinitionFormInput,
} from "../schemas/catalogSchemas";
import { Controller } from "react-hook-form";

const DATA_TYPES = ["Text", "Number", "Boolean", "Date", "Select"] as const;

export function AttributeDefinitionsPage() {
  const [groups, setGroups] = useState<AttributeGroupDto[]>([]);

  const loadGroups = useCallback(async () => {
    try {
      setGroups(await attributeGroupService.list(true));
    } catch {
      /* selector empty */
    }
  }, []);

  useEffect(() => {
    void loadGroups();
  }, [loadGroups]);

  const ctx = useCatalogCrud<
    AttributeDefinitionDto,
    AttributeDefinitionFormValues,
    CreateAttributeDefinitionPayload,
    UpdateAttributeDefinitionPayload,
    [],
    AttributeDefinitionFormInput
  >({
    service: attributeDefinitionService,
    schema: attributeDefinitionSchema,
    emptyForm: emptyAttributeDefinitionForm,
    toCreatePayload: (f) => f,
    toUpdatePayload: (id, f) => ({ ...f, id }),
    toFormValues: (dto) => ({
      groupId: dto.groupId,
      code: dto.code,
      name: dto.name,
      dataType: dto.dataType,
      isVariantAxis: dto.isVariantAxis,
      isRequired: dto.isRequired,
      sortOrder: dto.sortOrder,
    }),
    getId: (dto) => dto.id,
    permissionPrefix: "catalog",
  });

  if (!ctx.canView) return <NoAccessPage title="Definiciones de Atributos" />;

  return (
    <ErpPageTemplate kicker="Catálogo" title="Definiciones de Atributos">
      {ctx.error && (
        <ZHPageNotice variant="error" message="Error" detail={ctx.error} />
      )}
      <CatalogListSection
        ctx={ctx}
        title="Definiciones Registradas"
        icon="settings_input_component"
        createLabel="Nueva Definición"
        searchPlaceholder="Buscar definiciones..."
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
            key: "dataType",
            label: "Tipo",
            render: (r) => (
              <Badge label={r.dataType as string} variant="neutral" size="md" />
            ),
          },
          {
            key: "isVariantAxis",
            label: "Eje variante",
            render: (r) => (
              <span>{(r.isVariantAxis as boolean) ? "Sí" : "No"}</span>
            ),
          },
        ]}
      />
      <CatalogFormModal ctx={ctx} entityLabel="Definición de Atributo">
        <ZHField label="Grupo *" required error={ctx.errors.groupId?.message}>
          <ZhSelect disabled={ctx.saving} {...ctx.register("groupId")}>
            <option value="">— Seleccionar grupo —</option>
            {groups.map((g) => (
              <option key={g.id} value={g.id}>
                {g.code} — {g.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHGrid cols={2}>
          <ZHField label="Código *" required error={ctx.errors.code?.message}>
            <input
              className="zh-input mono"
              placeholder="COLOR"
              disabled={ctx.saving || !!ctx.editingId}
              {...ctx.register("code")}
            />
          </ZHField>
          <ZHField label="Nombre *" required error={ctx.errors.name?.message}>
            <input
              className="zh-input"
              placeholder="Color"
              disabled={ctx.saving}
              {...ctx.register("name")}
            />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField
            label="Tipo de dato *"
            required
            error={ctx.errors.dataType?.message}
          >
            <ZhSelect disabled={ctx.saving} {...ctx.register("dataType")}>
              {DATA_TYPES.map((dt) => (
                <option key={dt} value={dt}>
                  {dt}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Orden" error={ctx.errors.sortOrder?.message}>
            <input
              type="number"
              className="zh-input"
              min={0}
              disabled={ctx.saving}
              {...ctx.register("sortOrder", { valueAsNumber: true })}
            />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField label="Eje de variante">
            <Controller
              name="isVariantAxis"
              control={ctx.control}
              render={({ field }) => (
                <label className="zh-checkbox-label">
                  <input
                    type="checkbox"
                    checked={field.value}
                    onChange={field.onChange}
                    disabled={ctx.saving}
                  />
                  <span>Usar como eje de variante</span>
                </label>
              )}
            />
          </ZHField>
          <ZHField label="Requerido">
            <Controller
              name="isRequired"
              control={ctx.control}
              render={({ field }) => (
                <label className="zh-checkbox-label">
                  <input
                    type="checkbox"
                    checked={field.value}
                    onChange={field.onChange}
                    disabled={ctx.saving}
                  />
                  <span>Este atributo es obligatorio</span>
                </label>
              )}
            />
          </ZHField>
        </ZHGrid>
      </CatalogFormModal>
    </ErpPageTemplate>
  );
}
