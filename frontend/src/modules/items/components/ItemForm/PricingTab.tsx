import { useState } from "react";
import { useFormContext } from "react-hook-form";
import {
  ZHField,
  ZHFormAlert,
  ZHFormSection,
  ZHGrid,
} from "../../../../components/zh/ZHForm";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import { formatMoney } from "../../../../lib/sanitizers";
import { useAsync } from "../../../../hooks/useAsync";
import { useDebounce } from "../../../../hooks/useDebounce";
import { formatApiError } from "../../../lib/formatApiError";
import { priceListService } from "../../../pricing/api/pricingService";
import { companyProfileService } from "../../../configuracion/empresa/api/companyProfileService";
import {
  itemService,
  type ItemPricingSimulationRow,
} from "../../api/itemService";
import type { PriceSource } from "../../../pricing/api/pricingService";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

const SOURCE_LABELS: Record<PriceSource, string> = {
  BasePrice: "Precio Base",
  GeneralRule: "Regla General",
  Exception: "Excepción",
};

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  isEditMode: boolean;
  itemId?: string;
};

export function PricingTab({ t, disabled, isEditMode, itemId }: Props) {
  const {
    register,
    watch,
    formState: { errors },
  } = useFormContext<CreateItemFormValues>();
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);
  const dc = getDecimalConfig();

  // Moneda real de la lista de precios predeterminada; si aún no existe una lista
  // (ítem nuevo), se usa la moneda configurada en el perfil de la empresa
  // (Company.CurrencyCode) — nunca un código fijo en el código del formulario.
  const priceListsState = useAsync(() =>
    priceListService.list(true).catch(() => []),
  );
  const defaultList = (priceListsState.data ?? []).find((pl) => pl.isDefault);

  const companyProfileState = useAsync(() =>
    companyProfileService.getProfile().catch(() => null),
  );
  const currencyCode =
    defaultList?.currencyCode ?? companyProfileState.data?.currencyCode ?? "";

  const basePrice = watch("baseSalePrice");
  const maxDiscount = watch("saleConfig.maxDiscountPercent");

  return (
    <>
      <ZHFormSection
        title={t("items.pricing.title", "Precio de venta")}
        description={t(
          "items.pricing.sectionDesc",
          "Precio base (PVP) sin impuestos y el descuento máximo que se le puede aplicar en una venta.",
        )}
      >
        {!isEditMode && (
          <ZHFormAlert
            type="info"
            message={t(
              "items.pricing.hint",
              "Podrás marcar a qué listas pertenece este ítem después de guardarlo.",
            )}
          />
        )}

        <ZHGrid cols={2}>
          <ZHField
            label={t(
              "items.pricing.pvp",
              `PVP — Precio base sin IVA (${currencyCode})`,
            )}
            fieldError={fe((errors as any).baseSalePrice?.message)}
          >
            <div className="items-currency-input">
              <span className="items-currency-input__prefix">
                {currencyCode}
              </span>
              <ZhDecimalInput
                decimals={dc.salesUnitPrice}
                positiveOnly
                placeholder={t("items.pricing.pvpPlaceholder", "0.00")}
                {...register("baseSalePrice", {
                  valueAsNumber: true,
                  setValueAs: (v) => (v === "" ? null : Number(v)),
                })}
                disabled={disabled}
                className="items-currency-input__field"
              />
            </div>
          </ZHField>

          <ZHField
            label={t(
              "items.pricing.maxDiscount",
              "Descuento máximo permitido (%)",
            )}
          >
            <ZhDecimalInput
              decimals={dc.percentage}
              positiveOnly
              placeholder={t("items.pricing.maxDiscountPlaceholder", "0")}
              {...register("saleConfig.maxDiscountPercent", {
                valueAsNumber: true,
                setValueAs: (v) => (v === "" ? null : Number(v)),
              })}
              disabled={disabled}
            />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <PriceListSimulationTable
        t={t}
        itemId={itemId}
        disabled={disabled}
        basePrice={basePrice ?? null}
        maxDiscount={maxDiscount ?? null}
      />
    </>
  );
}

/**
 * Tabla única de simulación de precios: una fila por cada PriceList activa y vigente,
 * escalable a cualquier cantidad de listas sin cambios de código. El precio (sin impuestos)
 * y el precio mínimo permitido se calculan 100% en el backend (mismo motor que
 * PricingResolver) — aquí no se reimplementa ninguna fórmula de negocio. Pricing nunca
 * calcula IVA/ICE (frontera con la infraestructura tributaria) — por eso esta tabla no
 * muestra un precio final con impuestos.
 *
 * Funciona en ambos modos:
 * - Creación (sin itemId): simulación "qué pasaría si" contra las reglas generales de cada
 *   lista — no hay ItemId todavía, así que no puede haber reglas por-ítem ni asignación.
 *   La columna "Activa" queda deshabilitada (no hay nada a qué asignar aún).
 * - Edición (con itemId): además de la simulación, "Activa" persiste la pertenencia
 *   Item↔PriceList (mismo endpoint que antes usaba el botón "Guardar asignación") —
 *   se dispara sola al marcar/desmarcar, sin botón.
 *
 * En ambos modos, la tabla reacciona automáticamente (debounced) a cambios en precio base
 * y descuento máximo, aunque el formulario todavía no se haya guardado.
 */
function PriceListSimulationTable({
  t,
  itemId,
  disabled,
  basePrice,
  maxDiscount,
}: {
  t: (key: string, fallback?: string) => string;
  itemId?: string;
  disabled: boolean;
  basePrice: number | null;
  maxDiscount: number | null;
}) {
  const dc = getDecimalConfig();
  const debouncedBasePrice = useDebounce(basePrice, 400);
  const debouncedMaxDiscount = useDebounce(maxDiscount, 400);

  const overrides = {
    baseSalePrice: debouncedBasePrice,
    maxDiscountPercent: debouncedMaxDiscount,
  };
  const hasBasePrice = debouncedBasePrice != null && debouncedBasePrice > 0;

  const simState = useAsync(
    () =>
      itemId
        ? itemService.getPricingSimulation(itemId, overrides)
        : itemService.previewPricingSimulation(overrides),
    hasBasePrice,
    [itemId, debouncedBasePrice, debouncedMaxDiscount],
  );

  const [pendingIds, setPendingIds] = useState<Set<string>>(new Set());
  const [toggleError, setToggleError] = useState("");

  const rows = simState.data ?? [];

  const handleToggle = async (row: ItemPricingSimulationRow) => {
    if (!itemId) return;
    setToggleError("");
    const currentlyAssigned = rows
      .filter((r) => r.isAssigned)
      .map((r) => r.priceListId);
    const nextAssigned = row.isAssigned
      ? currentlyAssigned.filter((id) => id !== row.priceListId)
      : [...currentlyAssigned, row.priceListId];

    setPendingIds((prev) => new Set(prev).add(row.priceListId));
    try {
      await itemService.setPriceLists(itemId, nextAssigned);
      simState.refetch();
    } catch (e: unknown) {
      setToggleError(formatApiError(e));
    } finally {
      setPendingIds((prev) => {
        const next = new Set(prev);
        next.delete(row.priceListId);
        return next;
      });
    }
  };

  return (
    <ZHFormSection
      title={t(
        "items.pricing.simulation.title",
        "Simulación de listas de precios",
      )}
      description={
        itemId
          ? t(
              "items.pricing.simulation.desc",
              "Marca a qué listas pertenece el ítem. El precio de cada lista se recalcula automáticamente — no hay nada que guardar salvo la pertenencia.",
            )
          : t(
              "items.pricing.simulation.descPreview",
              "Así quedaría el precio en cada lista activa. Podrás marcar a cuáles pertenece el ítem después de guardarlo.",
            )
      }
    >
      {toggleError && <ZHFormAlert type="error" message={toggleError} />}
      {simState.error && <ZHFormAlert type="error" message={simState.error} />}

      {!hasBasePrice ? (
        <p>
          {t(
            "items.pricing.simulation.needsPrice",
            "Ingresa un precio base para ver la simulación.",
          )}
        </p>
      ) : simState.loading && rows.length === 0 ? (
        <p>{t("common.loading", "Cargando...")}</p>
      ) : rows.length === 0 ? (
        <p>
          {t(
            "items.pricing.simulation.empty",
            "No hay listas de precios activas y vigentes. Créalas desde el módulo Precios.",
          )}
        </p>
      ) : (
        <div className="prd-table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t("items.pricing.simulation.col.active", "Activa")}</th>
                <th>{t("items.pricing.simulation.col.list", "Lista")}</th>
                <th>{t("items.pricing.simulation.col.origin", "Origen")}</th>
                <th>
                  {t("items.pricing.simulation.col.rule", "Regla aplicada")}
                </th>
                <th>
                  {t("items.pricing.simulation.col.net", "Precio sin IVA")}
                </th>
                <th>
                  {t(
                    "items.pricing.simulation.col.maxDiscount",
                    "Descuento máximo",
                  )}
                </th>
                <th>
                  {t(
                    "items.pricing.simulation.col.minAllowed",
                    "Precio mínimo permitido",
                  )}
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.priceListId}>
                  <td>
                    <input
                      type="checkbox"
                      checked={row.isAssigned}
                      disabled={
                        !itemId || disabled || pendingIds.has(row.priceListId)
                      }
                      onChange={() => handleToggle(row)}
                    />
                  </td>
                  <td>{row.priceListName}</td>
                  <td>{SOURCE_LABELS[row.ruleSummary.source]}</td>
                  <td>
                    {row.ruleSummary.description}
                    {row.ruleSummary.source === "Exception" && (
                      <>
                        {" "}
                        <a
                          href="/pricing"
                          title="Administrar en el módulo Precios"
                        >
                          {t(
                            "items.pricing.simulation.viewException",
                            "Ver excepción",
                          )}
                        </a>
                      </>
                    )}
                  </td>
                  <td>
                    {row.currencyCode}{" "}
                    {formatMoney(row.netPrice, dc.salesUnitPrice)}
                  </td>
                  <td>
                    {row.maxDiscountPercent != null
                      ? `${row.maxDiscountPercent}%`
                      : "—"}
                  </td>
                  <td>
                    {row.currencyCode}{" "}
                    {formatMoney(row.minAllowedPrice, dc.salesUnitPrice)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </ZHFormSection>
  );
}
