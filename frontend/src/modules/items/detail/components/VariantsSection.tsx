import { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  ZHField,
  ZHGrid,
  ZHFormActions,
  ZHBtn,
} from "../../../../components/zh/ZHForm";
import { ZhNumberInput } from "../../../../components/zh/inputs/ZhNumberInput";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { EmptyState, Badge } from "../../../../components/PageShell";
import { applyServerErrors } from "../../../lib/validationErrors";
import { apiGet } from "../../../lib/apiEnvelope";
import { useI18n } from "../../../../i18n/i18n";
import { itemService } from "../../api/itemService";
import {
  attributeDefinitionService,
  type AttributeDefinitionDto,
} from "../../catalog/api/catalogService";
import type { ItemVariantDto } from "../../../../types/items";

const variantSchema = z.object({
  skuOverride: z.string().max(50).nullable().optional(),
  sortOrder: z.number().min(0).default(0),
  attributes: z.array(
    z.object({
      attributeDefinitionId: z.string().uuid(),
      value: z.string().min(1, "Valor requerido."),
    }),
  ),
});

type VariantFormValues = z.infer<typeof variantSchema>;

interface Props {
  itemId: string;
  variants: ItemVariantDto[];
  /** Único contrato de modo del módulo Items — true bloquea toda mutación (alta, barcodes, toggle). */
  disabled?: boolean;
  onToggle: (variantId: string, enable: boolean) => Promise<void>;
  onRefresh: () => void;
}

export function VariantsSection({
  itemId,
  variants,
  disabled = false,
  onToggle,
  onRefresh,
}: Props) {
  const { t } = useI18n();
  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [attrDefs, setAttrDefs] = useState<AttributeDefinitionDto[]>([]);
  const [barcodeTypes, setBarcodeTypes] = useState<
    { code: string; name: string }[]
  >([]);

  // Barcode inline add state
  const [bcVariantId, setBcVariantId] = useState<string | null>(null);
  const [bcCode, setBcCode] = useState("");
  const [bcType, setBcType] = useState("");
  const [bcSaving, setBcSaving] = useState(false);

  const loadBarcodeTypes = useCallback(async () => {
    try {
      const types = await apiGet<{ code: string; name: string }[]>(
        "/api/v1/catalog/barcode-types",
      );
      setBarcodeTypes(types);
    } catch {
      /* empty */
    }
  }, []);

  useEffect(() => {
    void loadBarcodeTypes();
  }, [loadBarcodeTypes]);

  const handleAddBarcode = async () => {
    if (!bcVariantId || !bcCode.trim() || !bcType) return;
    setBcSaving(true);
    try {
      await itemService.addBarcode(itemId, bcVariantId, bcCode.trim(), bcType);
      setBcVariantId(null);
      setBcCode("");
      setBcType("");
      onRefresh();
    } catch {
      /* error handled by API layer */
    }
    setBcSaving(false);
  };

  const handleDisableBarcode = async (variantId: string, barcodeId: string) => {
    try {
      await itemService.disableBarcode(itemId, variantId, barcodeId);
      onRefresh();
    } catch {
      /* error handled by API layer */
    }
  };

  const loadAttrDefs = useCallback(async () => {
    try {
      setAttrDefs(await attributeDefinitionService.list(undefined, true));
    } catch {
      /* empty */
    }
  }, []);

  useEffect(() => {
    void loadAttrDefs();
  }, [loadAttrDefs]);

  const form = useForm<VariantFormValues>({
    resolver: zodResolver(variantSchema),
    defaultValues: { skuOverride: null, sortOrder: 0, attributes: [] },
  });

  const openModal = () => {
    form.reset({
      skuOverride: null,
      sortOrder: 0,
      attributes: attrDefs.map((d) => ({
        attributeDefinitionId: d.id,
        value: "",
      })),
    });
    setSaveError("");
    setModalOpen(true);
  };

  const handleSave = form.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      const filledAttributes = values.attributes.filter(
        (a) => a.value.trim().length > 0,
      );
      await itemService.addVariant(itemId, {
        attributes: filledAttributes,
        skuOverride: values.skuOverride || null,
        sortOrder: values.sortOrder,
      });
      setModalOpen(false);
      onRefresh();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          t("items.variants.createError", "Error al crear la variante."),
        );
    } finally {
      setSaving(false);
    }
  });

  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">
            style
          </span>
          <span className="pg-section-label">
            {t("items.variants.sectionTitle", "Variantes")} ({variants.length})
          </span>
        </div>
        <ZHBtn
          variant="primary"
          size="sm"
          type="button"
          onClick={openModal}
          disabled={disabled}
        >
          <span className="material-symbols-outlined">add</span>{" "}
          {t("items.variants.new", "Nueva Variante")}
        </ZHBtn>
      </div>

      {variants.length === 0 ? (
        <div className="pg-pad-40">
          <EmptyState
            message={t(
              "items.variants.empty",
              "No hay variantes configuradas.",
            )}
          />
        </div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>{t("items.variants.col.sku", "SKU")}</th>
                <th>{t("items.variants.col.name", "Nombre")}</th>
                <th>{t("items.variants.col.attributes", "Atributos")}</th>
                <th>{t("items.variants.col.barcodes", "Barcodes")}</th>
                <th>{t("items.variants.col.status", "Estado")}</th>
                <th className="pg-th-right">
                  {t("items.variants.col.actions", "Acciones")}
                </th>
              </tr>
            </thead>
            <tbody>
              {variants.map((v) => (
                <tr
                  key={v.id}
                  className={v.isActive ? undefined : "pg-row-inactive"}
                >
                  <td>
                    <code className="prd-sku">{v.sku}</code>
                  </td>
                  <td>{v.name || "—"}</td>
                  <td>
                    {v.attributes.length > 0 ? (
                      v.attributes.map((a) => (
                        <Badge
                          key={a.attributeDefinitionId}
                          label={a.value}
                          variant="gray"
                          size="md"
                        />
                      ))
                    ) : (
                      <span className="subtle">—</span>
                    )}
                  </td>
                  <td>
                    <div className="items-barcode-list">
                      {v.barcodes.map((b) => (
                        <span key={b.id} className="items-barcode-chip">
                          <code className="items-barcode-chip__code">
                            {b.code}
                          </code>
                          <button
                            type="button"
                            title={t("common.deactivate", "Desactivar")}
                            className="items-barcode-chip__remove"
                            onClick={() =>
                              void handleDisableBarcode(v.id, b.id)
                            }
                            disabled={disabled}
                          >
                            <span className="material-symbols-outlined">
                              close
                            </span>
                          </button>
                        </span>
                      ))}
                      {bcVariantId === v.id ? (
                        <span className="items-barcode-add">
                          <input
                            value={bcCode}
                            onChange={(e) => setBcCode(e.target.value)}
                            placeholder={t("items.barcodes.code", "Código")}
                            disabled={bcSaving}
                            className="items-barcode-add__input"
                            onKeyDown={(e) => {
                              if (e.key === "Enter") {
                                e.preventDefault();
                                void handleAddBarcode();
                              }
                            }}
                            autoFocus
                          />
                          <select
                            value={bcType}
                            onChange={(e) => setBcType(e.target.value)}
                            disabled={bcSaving}
                            className="items-barcode-add__select"
                          >
                            <option value="">
                              {t("items.barcodes.typeShort", "— Tipo —")}
                            </option>
                            {barcodeTypes.map((bt) => (
                              <option key={bt.code} value={bt.code}>
                                {bt.name}
                              </option>
                            ))}
                          </select>
                          <button
                            type="button"
                            onClick={() => void handleAddBarcode()}
                            disabled={bcSaving || !bcCode.trim() || !bcType}
                            className="items-barcode-add__confirm"
                          >
                            <span className="material-symbols-outlined">
                              check
                            </span>
                          </button>
                          <button
                            type="button"
                            onClick={() => setBcVariantId(null)}
                            className="items-barcode-add__cancel"
                          >
                            <span className="material-symbols-outlined">
                              close
                            </span>
                          </button>
                        </span>
                      ) : (
                        <button
                          type="button"
                          title={t(
                            "items.variants.addBarcode",
                            "Agregar código de barras",
                          )}
                          onClick={() => {
                            setBcVariantId(v.id);
                            setBcCode("");
                            setBcType("");
                          }}
                          className="items-barcode-add-trigger"
                          disabled={disabled}
                        >
                          <span className="material-symbols-outlined">add</span>
                        </button>
                      )}
                    </div>
                  </td>
                  <td>
                    <span
                      className={
                        v.isActive
                          ? "zh-status zh-status--active"
                          : "zh-status zh-status--inactive"
                      }
                    >
                      {v.isActive
                        ? t("common.active", "Activo")
                        : t("common.inactive", "Inactivo")}
                    </span>
                  </td>
                  <td className="pg-td-right">
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="sm"
                      title={
                        v.isActive
                          ? t("common.deactivate", "Desactivar")
                          : t("common.activate", "Activar")
                      }
                      onClick={() => void onToggle(v.id, !v.isActive)}
                      disabled={disabled}
                    >
                      <span className="material-symbols-outlined">
                        {v.isActive ? "block" : "check_circle"}
                      </span>
                    </ZHBtn>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ZHModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        title={t("items.variants.new", "Nueva Variante")}
        subtitle={t(
          "items.variants.newSubtitle",
          "Agrega una variante al ítem.",
        )}
        footer={
          <ZHFormActions
            onCancel={() => setModalOpen(false)}
            onSave={() => void handleSave()}
            hideDraft
            disableSave={saving}
            labels={{
              cancel: t("common.cancel", "Cancelar"),
              save: saving
                ? t("common.saving", "Guardando...")
                : t("items.variants.create", "Crear variante"),
            }}
          />
        }
      >
        {saveError && (
          <ZHPageNotice
            variant="error"
            message={t("common.error", "Error")}
            detail={saveError}
          />
        )}
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField
              label={t("items.variants.skuOverride", "SKU (override)")}
              error={form.formState.errors.skuOverride?.message}
            >
              <input
                className="zh-input mono"
                placeholder={t(
                  "items.variants.skuOverridePlaceholder",
                  "Dejar vacío para auto-generar",
                )}
                disabled={saving}
                {...form.register("skuOverride")}
              />
            </ZHField>
            <ZHField
              label={t("items.variants.sortOrder", "Orden")}
              error={form.formState.errors.sortOrder?.message}
            >
              <ZhNumberInput
                className="zh-input"
                positiveOnly
                disabled={saving}
                {...form.register("sortOrder", { valueAsNumber: true })}
              />
            </ZHField>
          </ZHGrid>

          {attrDefs.length > 0 && (
            <>
              <div className="zh-mt-16 zh-mb-8 items-variant-attrs-heading">
                {t("items.variants.attributesHeading", "Atributos de variante")}
              </div>
              <ZHGrid cols={2}>
                {attrDefs.map((def, idx) => (
                  <ZHField
                    key={def.id}
                    label={`${def.name}${def.isRequired ? " *" : ""}`}
                    error={
                      form.formState.errors.attributes?.[idx]?.value?.message
                    }
                  >
                    <input
                      className="zh-input"
                      placeholder={def.name}
                      disabled={saving}
                      {...form.register(`attributes.${idx}.value`)}
                    />
                  </ZHField>
                ))}
              </ZHGrid>
            </>
          )}
        </div>
      </ZHModal>
    </div>
  );
}
