import type { ProductProfile } from "../components/ProductPicker";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { resolvePurchaseItemSelection } from "./purchaseItemProfile";
import { resetLineToSupplierOrigin, resolvePackagingChange, type buildPurchaseLinePresentation } from "./purchaseLinePresentation";

type PackagingLevel = { id: string; baseQuantity?: number; uomCode?: string };

/**
 * Acciones de una línea de compra sobre el formulario (presentación, reinicio al origen del
 * proveedor, selección y desvinculación de producto). Lógica movida sin cambios desde
 * PurchaseLineCard; solo escribe vía `ctx.updateLine` y los handlers del hook de la página.
 */
export function buildPurchaseLineHandlers({ line: l, ctx, vm, packagingLevels, baseUomCode }: {
  line: PurchaseLineFormValues;
  ctx: ReturnType<typeof usePurchasesPage>;
  vm: ReturnType<typeof buildPurchaseLinePresentation>;
  packagingLevels: PackagingLevel[];
  baseUomCode: string | undefined;
}) {
  // FASE 5 PURCHASE-UX — Cantidad/Costo a registrar (unidad base) son autoritativos. Línea XML sin
  // editar: la presentación expresa la cantidad ORIGINAL del proveedor (2 cajas x12 → 24). Línea
  // manual o XML ya editada: se conserva exactamente lo a registrar, la presentación solo se deriva.
  // Ver resolvePackagingChange.
  const handlePackagingChange = (packagingLevelId: string) => {
    const selected = packagingLevels.find((p) => p.id === packagingLevelId);
    const nextFactor = selected?.baseQuantity ?? 1;
    const next = resolvePackagingChange(
      l,
      vm.inventory.conversionFactorValue,
      nextFactor,
      packagingLevelId || undefined,
    );
    ctx.updateLine(l._key, "packagingLevelId", packagingLevelId || undefined);
    ctx.updateLine(l._key, "uomCode", selected?.uomCode ?? baseUomCode);
    ctx.updateLine(l._key, "baseUomCode", baseUomCode);
    ctx.updateLine(l._key, "conversionFactor", nextFactor);
    ctx.updateLine(l._key, "quantity", next.quantity);
    ctx.updateLine(l._key, "unitPrice", next.unitPrice);
    ctx.updateLine(l._key, "quantityInBaseUom", next.quantityInBaseUom);
    ctx.updateLine(l._key, "baseUnitPrice", next.baseUnitPrice);
    ctx.updateLine(l._key, "xmlOriginFactor", next.xmlOriginFactor);
    ctx.updateLine(l._key, "xmlOriginPackagingLevelId", next.xmlOriginPackagingLevelId);
  };
  // FASE 5 PURCHASE-UX — acción EXPLÍCITA (única) para volver a la cantidad/costo del proveedor,
  // en la presentación de origen.
  const handleResetToSupplierOrigin = () => {
    const origin = resetLineToSupplierOrigin(l, vm.inventory.conversionFactorValue);
    if (!origin) return;
    const originLevel = packagingLevels.find((p) => p.id === origin.packagingLevelId);
    ctx.updateLine(l._key, "packagingLevelId", origin.packagingLevelId);
    ctx.updateLine(l._key, "uomCode", originLevel?.uomCode ?? baseUomCode);
    ctx.updateLine(l._key, "conversionFactor", origin.conversionFactor);
    ctx.updateLine(l._key, "xmlOriginFactor", origin.xmlOriginFactor);
    ctx.updateLine(l._key, "xmlOriginPackagingLevelId", origin.xmlOriginPackagingLevelId);
    ctx.updateLine(l._key, "quantity", origin.quantity);
    ctx.updateLine(l._key, "unitPrice", origin.unitPrice);
    ctx.updateLine(l._key, "quantityInBaseUom", origin.quantityInBaseUom);
    ctx.updateLine(l._key, "baseUnitPrice", origin.baseUnitPrice);
  };
  const handleProductSelect = (p: ProductProfile) => {
    const patch = resolvePurchaseItemSelection(p, {
      existingLine: l,
    });
    ctx.updateLine(l._key, "_readinessIssue", undefined);
    if (patch.vatCode !== undefined)
      ctx.updateLine(l._key, "vatCode", patch.vatCode);
    if (patch.iceCode !== undefined)
      ctx.updateLine(l._key, "iceCode", patch.iceCode);
    if (l.purchaseReceptionLineId) {
      void ctx.handleMatchItem(
        l._key,
        p.id,
        patch.description ?? p.label,
      );
      return;
    }
    ctx.updateLine(l._key, "itemId", patch.itemId ?? p.id);
    ctx.updateLine(
      l._key,
      "description",
      patch.description ?? p.label,
    );
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    void ctx.fetchItemContext(l._key, p.id, wh);
  };
  const handleUnlinkProduct = () => {
    if (l.purchaseReceptionLineId) {
      void ctx.handleUnmatchItem(l._key);
      return;
    }
    ctx.updateLine(l._key, "itemId", undefined);
    ctx.updateLine(l._key, "description", "");
    ctx.updateLine(l._key, "context", undefined);
    ctx.updateLine(l._key, "_readinessIssue", undefined);
  };
  return { handlePackagingChange, handleResetToSupplierOrigin, handleProductSelect, handleUnlinkProduct };
}
