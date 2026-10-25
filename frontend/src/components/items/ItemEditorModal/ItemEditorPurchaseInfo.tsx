import { ZHField, ZHFormSection, ZHGrid } from "../../zh/ZHForm";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import type { CreateItemInitialData } from "./types";

/** "¿Qué llegó del proveedor?" — solo lectura, nunca editable, igual en ambos modos. */
export function PurchaseInfoReadOnly({
  purchaseContext,
}: {
  purchaseContext: NonNullable<CreateItemInitialData["purchaseContext"]>;
}) {
  // Presentación semántica (06): mismo resolver que el Design System.
  const pp = {
    purchaseUnitPriceDecimals: usePrecisionDecimals("purchaseUnitPrice"),
    quantityDecimals: usePrecisionDecimals("quantity"),
    percentageDecimals: usePrecisionDecimals("percentage"),
  };
  const { unitCost, quantity, discountPct } = purchaseContext;
  const hasDiscount = discountPct != null;
  const costFinal = unitCost * (1 - (discountPct ?? 0) / 100);

  return (
    <ZHFormSection
      title="Información de Compra"
      description="Datos de la factura de origen — solo lectura."
    >
      <ZHGrid cols={2}>
        <ZHField label="Costo unitario" readOnly density="compact">
          <input
            value={formatMoneyWithSymbol(unitCost, pp.purchaseUnitPriceDecimals)}
            disabled
          />
        </ZHField>
        <ZHField label="Cantidad" readOnly density="compact">
          <input value={formatMoney(quantity, pp.quantityDecimals)} disabled />
        </ZHField>
        <ZHField label="Descuento aplicado" readOnly density="compact">
          <input
            value={
              hasDiscount
                ? `${formatMoney(discountPct ?? 0, pp.percentageDecimals)}%`
                : "—"
            }
            disabled
          />
        </ZHField>
        <ZHField
          label="Costo final utilizado para la simulación"
          readOnly
          density="compact"
        >
          <input
            value={formatMoneyWithSymbol(costFinal, pp.purchaseUnitPriceDecimals)}
            disabled
          />
        </ZHField>
      </ZHGrid>
    </ZHFormSection>
  );
}
