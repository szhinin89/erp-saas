import { useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHLineAction } from "../../../components/zh/ZHLineAction";
import { ZHLineActionItem } from "../../../components/zh/ZHLineActionItem";
import { CorrectSupplierMatchModal } from "../components/CorrectSupplierMatchModal";
import { ProductPicker } from "../components/ProductPicker";
import type { ProductProfile } from "../components/ProductPicker";
import { ItemEditorModal } from "../../../components/items/ItemEditorModal/ItemEditorModal";
import type { ItemCreatedResult } from "../../../components/items/ItemEditorModal/types";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { buildPurchaseItemProfile, resolvePurchaseItemSelection } from "../utils/purchaseItemProfile";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchaseLineDraft = PurchaseLineFormValues;

export function ProductLineActionMenu({
  line: l,
  ctx,
  disabled,
  onProductSelect,
  onViewProduct,
  onUnlinkProduct,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled: boolean;
  onProductSelect: (product: ProductProfile) => void;
  onViewProduct: () => void;
  onUnlinkProduct: () => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [selectorOpen, setSelectorOpen] = useState(false);
  const [productModalOpen, setProductModalOpen] = useState(false);
  const hasProduct = !!l.itemId;
  const canEditProduct = !disabled;
  const triggerDisabled = !hasProduct && !canEditProduct;

  // Click-outside/Escape ahora los maneja ZHLineAction kind="menu" internamente; este
  // componente solo decide si el cierre procede (se ignora mientras hay un modal interno
  // abierto — mismo gate que antes tenía el handler de mousedown).
  const handleOpenChange = (next: boolean) => {
    if (!next && productModalOpen) return;
    setOpen(next);
    if (!next) setSelectorOpen(false);
  };

  const closeAfterModal = () => {
    setProductModalOpen(false);
    setOpen(false);
    setSelectorOpen(false);
  };

  return (
    <ZHLineAction
      kind="menu"
      icon="grid_view"
      tone="primary"
      label={t("purchases.lines.productActions", "Acciones del producto")}
      title={t("purchases.lines.productActions", "Acciones del producto")}
      disabled={triggerDisabled}
      open={open}
      onOpenChange={handleOpenChange}
    >
      {!hasProduct ? (
        <>
          <ZHLineActionItem
            icon="search"
            label={t("purchases.lines.selectProduct", "Seleccionar producto")}
            disabled={disabled || ctx.matchingKey === l._key}
            onClick={() => setSelectorOpen((current) => !current)}
          />
          {selectorOpen && (
            <div className="pdl-product-menu__picker">
              <ProductPicker
                disabled={disabled || ctx.matchingKey === l._key}
                vatRates={ctx.vatRatesMap}
                initialQuery={l.description?.trim() || undefined}
                onSelect={(product) => {
                  onProductSelect(product);
                  setOpen(false);
                  setSelectorOpen(false);
                }}
              />
            </div>
          )}
          {l.purchaseReceptionLineId ? (
            <ReceptionCreateItemAction
              line={l}
              ctx={ctx}
              disabled={disabled}
              buttonClassName="zh-line-action-item"
              label={t("purchases.lines.createProduct", "Crear producto")}
              onAfterModalClose={closeAfterModal}
            />
          ) : (
            <CreateItemLineAction
              line={l}
              ctx={ctx}
              disabled={disabled}
              buttonClassName="zh-line-action-item"
              label={t("purchases.lines.createProduct", "Crear producto")}
              onModalOpenChange={setProductModalOpen}
              onAfterModalClose={closeAfterModal}
            />
          )}
        </>
      ) : (
        <>
          <ZHLineActionItem
            icon="visibility"
            label={t("purchases.lines.viewProduct", "Ver producto")}
            onClick={() => {
              setOpen(false);
              onViewProduct();
            }}
          />
          {canEditProduct && (
            <UpdateItemAction
              line={l}
              ctx={ctx}
              buttonClassName="zh-line-action-item"
              label={t("purchases.lines.updateProduct", "Actualizar producto")}
              onModalOpenChange={setProductModalOpen}
              onAfterModalClose={closeAfterModal}
            />
          )}
          {canEditProduct && l.purchaseReceptionLineId && (
            <CorrectMatchAction
              line={l}
              ctx={ctx}
              buttonClassName="zh-line-action-item"
              onModalOpenChange={setProductModalOpen}
              onAfterModalClose={closeAfterModal}
            />
          )}
          {canEditProduct && (
            <ZHLineActionItem
              icon="link_off"
              label={
                ctx.matchingKey === l._key
                  ? t("purchases.lines.unlinkingProduct", "Desvinculando...")
                  : t("purchases.lines.unlinkProduct", "Desvincular producto")
              }
              disabled={ctx.matchingKey === l._key}
              onClick={() => {
                setOpen(false);
                onUnlinkProduct();
              }}
            />
          )}
        </>
      )}
    </ZHLineAction>
  );
}

/**
 * "Crear Item" para una línea de compra manual sin producto asignado — usa el ItemEditorModal
 * genérico directamente (sin wrapper), en modo Crear (sin `itemId`): esta línea no tiene
 * PurchaseReceptionLineId, por lo que no hay nada que vincular en el backend de Compras más allá
 * de seleccionar el ítem en el formulario, igual que hace ProductPicker.onSelect.
 */
function CreateItemLineAction({
  line: l,
  ctx,
  disabled,
  buttonClassName,
  label,
  onModalOpenChange,
  onAfterModalClose,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled?: boolean;
  buttonClassName?: string;
  label?: string;
  onModalOpenChange?: (open: boolean) => void;
  onAfterModalClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();

  const handleSaved = (item: ItemCreatedResult) => {
    setOpen(false);
    onModalOpenChange?.(false);
    onAfterModalClose?.();
    const patch = resolvePurchaseItemSelection(buildPurchaseItemProfile(item), {
      existingLine: l,
      overwriteVatCode: true,
    });
    ctx.updateLine(l._key, "itemId", patch.itemId ?? item.id);
    ctx.updateLine(
      l._key,
      "description",
      patch.description ?? `${item.sku} — ${item.shortName}`,
    );
    ctx.updateLine(l._key, "_readinessIssue", undefined);
    // El Item recién creado desde Compras trae su propio IVA de compra (ahora obligatorio en
    // ItemEditorModal) — se refleja de inmediato en la línea, sin depender de fetchItemContext
    // (que solo llena `context` para mostrar, no el campo `vatCode` del formulario).
    ctx.updateLine(l._key, "vatCode", patch.vatCode ?? "");
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    void ctx.fetchItemContext(l._key, item.id, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        className={buttonClassName}
        disabled={disabled}
        onClick={() => {
          onModalOpenChange?.(true);
          setOpen(true);
        }}
      >
        {label ?? t("purchases.lines.createProduct", "Crear producto")}
      </ZHBtn>
      <ItemEditorModal
        open={open}
        initialData={{
          name: l.description || undefined,
          barcode: l.xmlSupplierAuxCode ?? l.xmlSupplierCode ?? undefined,
          supplierCode: l.xmlSupplierCode ?? undefined,
          supplierId: ctx.formWatch.supplierId || undefined,
          source: l.xmlSupplierCode ? "PurchaseReception" : "Manual",
          purchaseContext:
            l.unitPrice > 0
              ? {
                  unitCost: l.unitPrice,
                  quantity: l.quantity,
                  discountPct: l.discountPct ?? undefined,
                  vatCode: l.vatCode || undefined,
                }
              : undefined,
        }}
        onClose={() => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
        }}
        onSaved={handleSaved}
      />
    </>
  );
}

/**
 * "Actualizar Item" — visible en cualquier línea que ya tenga Item vinculado (manual o de
 * Recepción, da igual el origen). Reutiliza el mismo ItemEditorModal en modo Actualizar (pasando
 * `itemId`) y, al guardar, refresca el contexto de la línea con el mismo flujo ya existente
 * (`fetchItemContext`) — nunca un flujo paralelo de actualización visual.
 */
/**
 * FASE 3 — "Corregir relación" de una línea XML ya vinculada: producto/presentación correctos para
 * esta línea y para las próximas facturas del proveedor con el mismo código (backend atómico).
 */
function CorrectMatchAction({
  line: l,
  ctx,
  buttonClassName,
  onModalOpenChange,
  onAfterModalClose,
}: {
  line: PurchaseLineFormValues;
  ctx: ReturnType<typeof usePurchasesPage>;
  buttonClassName?: string;
  onModalOpenChange?: (open: boolean) => void;
  onAfterModalClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();
  const close = () => {
    setOpen(false);
    onModalOpenChange?.(false);
    onAfterModalClose?.();
  };
  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        className={buttonClassName}
        disabled={ctx.matchingKey === l._key}
        onClick={() => {
          onModalOpenChange?.(true);
          setOpen(true);
        }}
      >
        {t("purchases.lines.correctMatch", "Corregir relación")}
      </ZHBtn>
      <CorrectSupplierMatchModal
        open={open}
        supplierName={ctx.supplierProfile?.name ?? ""}
        supplierCode={l.xmlSupplierCode ?? ""}
        xmlDescription={l.xmlDescription ?? l.description}
        currentLabel={l.description}
        onClose={close}
        onSubmit={(itemId, packagingLevelId) =>
          ctx.handleCorrectMatch(l._key, itemId, packagingLevelId)
        }
      />
    </>
  );
}

function UpdateItemAction({
  line: l,
  ctx,
  buttonClassName,
  label,
  onModalOpenChange,
  onAfterModalClose,
}: {
  line: PurchaseLineFormValues;
  ctx: ReturnType<typeof usePurchasesPage>;
  buttonClassName?: string;
  label?: string;
  onModalOpenChange?: (open: boolean) => void;
  onAfterModalClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();
  if (!l.itemId) return null;

  const handleSaved = (item: ItemCreatedResult) => {
    setOpen(false);
    onModalOpenChange?.(false);
    onAfterModalClose?.();
    const patch = resolvePurchaseItemSelection(buildPurchaseItemProfile(item), {
      existingLine: l,
      overwriteVatCode: true,
    });
    ctx.updateLine(
      l._key,
      "description",
      patch.description ?? `${item.sku} — ${item.shortName}`,
    );
    ctx.updateLine(l._key, "_readinessIssue", undefined);
    // Si el usuario cambió el IVA de compra al editar el Item desde esta línea, la línea debe
    // reflejar el valor vigente del Item de inmediato.
    ctx.updateLine(l._key, "vatCode", patch.vatCode ?? "");
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    void ctx.fetchItemContext(l._key, item.id, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        className={buttonClassName}
        onClick={() => {
          onModalOpenChange?.(true);
          setOpen(true);
        }}
      >
        {label ?? t("purchases.lines.updateProduct", "Actualizar producto")}
      </ZHBtn>
      <ItemEditorModal
        open={open}
        itemId={l.itemId}
        initialData={{
          purchaseContext:
            l.unitPrice > 0
              ? {
                  unitCost: l.unitPrice,
                  quantity: l.quantity,
                  discountPct: l.discountPct ?? undefined,
                  vatCode: l.vatCode || undefined,
                }
              : undefined,
        }}
        onClose={() => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
        }}
        onSaved={handleSaved}
      />
    </>
  );
}

/**
 * "Crear producto" para una línea que sí tiene PurchaseReceptionLineId — PURCHASES-HARDENING-P0
 * (B9): abre el mismo "Resolver productos pendientes" (ResolvePendingProductsModal) limitado a esta
 * línea, así la creación individual usa exactamente el SSOT del resolver masivo (frontend
 * `buildResolveRequest` + backend ResolvePurchaseReceptionLines): barcode real de unidad → unidad,
 * de caja/pack → su presentación, sin barcode real de unidad → código del proveedor como Interno
 * provisional. Nunca una heurística aparte.
 */
function ReceptionCreateItemAction({
  line: l,
  ctx,
  disabled,
  buttonClassName,
  label,
  onAfterModalClose,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled?: boolean;
  buttonClassName?: string;
  label?: string;
  onAfterModalClose?: () => void;
}) {
  const { t } = useI18n();
  return (
    <ZHBtn
      variant="ghost"
      size="xs"
      type="button"
      className={buttonClassName}
      disabled={disabled}
      onClick={() => {
        onAfterModalClose?.();
        ctx.openPendingProducts(l.purchaseReceptionLineId as string);
      }}
    >
      {label ?? t("purchases.lines.createProduct", "Crear producto")}
    </ZHBtn>
  );
}
