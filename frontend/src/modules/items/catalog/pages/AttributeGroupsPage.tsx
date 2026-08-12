import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage, Badge } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhTextInput, ZhNumberInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
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
  const { t } = useI18n();
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

  if (!ctx.canView)
    return (
      <NoAccessPage
        title={t(
          "catalog.attributeGroups.title",
          "Grupos de atributos",
        )}
      />
    );

  return (
    <ErpPageTemplate
      kicker={t("catalog.kicker", "Catálogo")}
      title={t("catalog.attributeGroups.title", "Grupos de atributos")}
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
        title={t("catalog.attributeGroups.registered", "Grupos registrados")}
        icon="tune"
        createLabel={t("catalog.attributeGroups.new", "Nuevo grupo")}
        searchPlaceholder={t(
          "catalog.attributeGroups.search",
          "Buscar grupos...",
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
            key: "sortOrder",
            label: t("catalog.col.sortOrder", "Orden"),
            render: (r) => <span>{r.sortOrder as number}</span>,
          },
        ]}
      />
      <CatalogFormModal
        ctx={ctx}
        entityLabel={t("catalog.attributeGroups.entity", "Grupo de atributos")}
      >
        <ZHGrid cols={2}>
          <ZHField
            label={t("catalog.form.codeRequired", "Código *")}
            required
            error={ctx.errors.code?.message}
          >
            <ZhTextInput
              className="zh-input mono"
              placeholder={t("catalog.attributeGroups.codePlaceholder", "DIM")}
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
                "catalog.attributeGroups.namePlaceholder",
                "Dimensiones",
              )}
              disabled={ctx.saving}
              {...ctx.register("name")}
            />
          </ZHField>
        </ZHGrid>
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
      </CatalogFormModal>
    </ErpPageTemplate>
  );
}
