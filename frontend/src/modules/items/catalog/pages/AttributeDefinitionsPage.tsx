import { useCallback, useEffect, useState } from "react";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage, Badge } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import {
  ZhSelect,
  ZhTextInput,
  ZhNumberInput,
} from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
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
const DATA_TYPE_LABEL_KEYS: Record<(typeof DATA_TYPES)[number], string> = {
  Text: "catalog.attributeDefinitions.dataType.text",
  Number: "catalog.attributeDefinitions.dataType.number",
  Boolean: "catalog.attributeDefinitions.dataType.boolean",
  Date: "catalog.attributeDefinitions.dataType.date",
  Select: "catalog.attributeDefinitions.dataType.select",
};

export function AttributeDefinitionsPage() {
  const { t } = useI18n();
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

  if (!ctx.canView)
    return (
      <NoAccessPage
        title={t(
          "catalog.attributeDefinitions.title",
          "Definiciones de atributos",
        )}
      />
    );

  return (
    <ErpPageTemplate
      kicker={t("catalog.kicker", "Productos y servicios")}
      title={t(
        "catalog.attributeDefinitions.title",
        "Definiciones de atributos",
      )}
    >
      {ctx.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.error", "Error")}
          detail={ctx.error}
        />
      )}
      <CatalogListSection
        ctx={ctx}
        title={t(
          "catalog.attributeDefinitions.registered",
          "Definiciones registradas",
        )}
        icon="settings_input_component"
        createLabel={t(
          "catalog.attributeDefinitions.new",
          "Nueva definición",
        )}
        searchPlaceholder={t(
          "catalog.attributeDefinitions.search",
          "Buscar definiciones...",
        )}
        columns={[
          {
            key: "code",
            label: t("catalog.col.code", "Código"),
            render: (r) => (
              <Badge label={r.code as string} variant="neutral" className="mono" />
            ),
          },
          {
            key: "name",
            label: t("catalog.col.name", "Nombre"),
            render: (r) => <strong>{r.name as string}</strong>,
          },
          {
            key: "dataType",
            label: t("catalog.attributeDefinitions.dataType", "Tipo"),
            render: (r) => (
              <Badge
                label={t(
                  DATA_TYPE_LABEL_KEYS[
                    r.dataType as (typeof DATA_TYPES)[number]
                  ] ?? "catalog.attributeDefinitions.dataType.unknown",
                  r.dataType as string,
                )}
                variant="neutral"
                size="md"
              />
            ),
          },
          {
            key: "isVariantAxis",
            label: t(
              "catalog.attributeDefinitions.variantAxis",
              "Eje variante",
            ),
            render: (r) => (
              <span>
                {(r.isVariantAxis as boolean)
                  ? t("common.yes", "Sí")
                  : t("common.no", "No")}
              </span>
            ),
          },
        ]}
      />
      <CatalogFormModal
        ctx={ctx}
        entityLabel={t(
          "catalog.attributeDefinitions.entity",
          "Definición de atributo",
        )}
      >
        <ZHField
          label={t("catalog.attributeDefinitions.groupRequired", "Grupo *")}
          required
          error={ctx.errors.groupId?.message}
        >
          <ZhSelect disabled={ctx.saving} {...ctx.register("groupId")}>
            <option value="">
              {t(
                "catalog.attributeDefinitions.selectGroup",
                "— Seleccionar grupo —",
              )}
            </option>
            {groups.map((g) => (
              <option key={g.id} value={g.id}>
                {g.code} — {g.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHGrid cols={2}>
          <ZHField
            label={t("catalog.form.codeRequired", "Código *")}
            required
            error={ctx.errors.code?.message}
          >
            <ZhTextInput
              className="zh-input mono"
              placeholder={t(
                "catalog.attributeDefinitions.codePlaceholder",
                "COLOR",
              )}
              disabled={ctx.saving || !!ctx.editingId}
              {...ctx.register("code")}
            />
          </ZHField>
          <ZHField
            label={t("catalog.form.nameRequired", "Nombre *")}
            required
            error={ctx.errors.name?.message}
          >
            <ZhTextInput
              className="zh-input"
              placeholder={t(
                "catalog.attributeDefinitions.namePlaceholder",
                "Color",
              )}
              disabled={ctx.saving}
              {...ctx.register("name")}
            />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField
            label={t(
              "catalog.attributeDefinitions.dataTypeRequired",
              "Tipo de dato *",
            )}
            required
            error={ctx.errors.dataType?.message}
          >
            <ZhSelect disabled={ctx.saving} {...ctx.register("dataType")}>
              {DATA_TYPES.map((dt) => (
                <option key={dt} value={dt}>
                  {t(DATA_TYPE_LABEL_KEYS[dt], dt)}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField
            label={t("catalog.col.sortOrder", "Orden")}
            error={ctx.errors.sortOrder?.message}
          >
            <ZhNumberInput
              className="zh-input"
              positiveOnly
              disabled={ctx.saving}
              {...ctx.register("sortOrder", { valueAsNumber: true })}
            />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField
            label={t(
              "catalog.attributeDefinitions.variantAxisField",
              "Eje de variante",
            )}
          >
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
                  <span>
                    {t(
                      "catalog.attributeDefinitions.useAsVariantAxis",
                      "Usar como eje de variante",
                    )}
                  </span>
                </label>
              )}
            />
          </ZHField>
          <ZHField label={t("catalog.attributeDefinitions.required", "Requerido")}>
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
                  <span>
                    {t(
                      "catalog.attributeDefinitions.isRequiredHelp",
                      "Este atributo es obligatorio",
                    )}
                  </span>
                </label>
              )}
            />
          </ZHField>
        </ZHGrid>
      </CatalogFormModal>
    </ErpPageTemplate>
  );
}
