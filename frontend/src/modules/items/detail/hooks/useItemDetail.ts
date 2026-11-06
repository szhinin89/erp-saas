import { useCallback, useEffect, useRef, useState } from "react";
import { itemService } from "../../api/itemService";
import { formatApiRequestError } from "../../../lib/apiError";
import { message } from "../../../../lib/messages";
import { useI18n } from "../../../../i18n/i18n";
import type { ItemDetailDto } from "../../../../types/items";

export function useItemDetailPage(itemId: string | undefined) {
  const { t } = useI18n();
  const [item, setItem] = useState<ItemDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const fetchItem = useCallback(async () => {
    if (!itemId) return;
    setError("");
    setLoading(true);
    try {
      setItem(await itemService.getById(itemId));
    } catch {
      setError("Error al cargar el ítem.");
    } finally {
      setLoading(false);
    }
  }, [itemId]);

  useEffect(() => {
    void fetchItem();
  }, [fetchItem]);

  // FUNC-DEACTIVATION-CONFIRM-01 (AA-D5): desactivar variante/imagen confirma antes de llamar
  // al backend; activar variante conserva el flujo directo. Un solo cambio de estado a la vez.
  const [statusChangePending, setStatusChangePending] = useState(false);
  const statusChangeRef = useRef(false);
  const [imageActivationError, setImageActivationError] = useState("");

  const enableImage = async (imageId: string) => {
    if (!itemId || statusChangeRef.current) return;
    statusChangeRef.current = true;
    setStatusChangePending(true);
    setImageActivationError("");
    try {
      await itemService.enableImage(itemId, imageId);
      try {
        // Keep the current rows until the server supplies the refreshed state.
        setItem(await itemService.getById(itemId));
      } catch (err) {
        setImageActivationError(`${t("items.images.activate.refreshFailed")} ${formatApiRequestError(err, {
          generic: t("items.images.activate.refreshError"),
        })}`);
      }
    } catch (err) {
      setImageActivationError(formatApiRequestError(err, {
        generic: t("items.images.activate.error"),
      }));
    } finally {
      setStatusChangePending(false);
      statusChangeRef.current = false;
    }
  };

  const runStatusChange = async (
    confirmOptions: Parameters<typeof message.confirm>[0] | null,
    action: () => Promise<unknown>,
    errorMessage: string,
  ) => {
    if (!itemId || statusChangeRef.current) return;
    statusChangeRef.current = true;
    try {
      if (confirmOptions && !(await message.confirm(confirmOptions))) return;
      setStatusChangePending(true);
      try {
        await action();
        await fetchItem();
      } catch {
        setError(errorMessage);
      } finally {
        setStatusChangePending(false);
      }
    } finally {
      statusChangeRef.current = false;
    }
  };

  const toggleVariant = async (variantId: string, enable: boolean) => {
    if (!itemId) return;
    const variant = item?.variants.find((v) => v.id === variantId);
    const label = variant
      ? `${variant.sku} — ${variant.name}`
      : t("items.variants.deactivate.unknownLabel");
    await runStatusChange(
      enable
        ? null
        : {
            title: t("items.variants.deactivate.confirm.title", { label }),
            message: t("items.variants.deactivate.confirm.message", { label }),
            variant: "danger",
            confirmLabel: t("common.deactivate"),
            cancelLabel: t("common.cancel"),
          },
      () =>
        enable
          ? itemService.enableVariant(itemId, variantId)
          : itemService.disableVariant(itemId, variantId),
      "Error al cambiar estado de la variante.",
    );
  };

  const disableImage = async (imageId: string) => {
    if (!itemId) return;
    const image = item?.images.find((i) => i.id === imageId);
    const label =
      image?.altText ||
      (image
        ? `${image.storageObjectId.slice(0, 12)}…`
        : t("items.images.deactivate.unknownLabel"));
    await runStatusChange(
      {
        title: t("items.images.deactivate.confirm.title", { label }),
        message: t(
          image?.isMain
            ? "items.images.deactivate.confirm.messageMain"
            : "items.images.deactivate.confirm.message",
          { label },
        ),
        variant: "danger",
        confirmLabel: t("common.deactivate"),
        cancelLabel: t("common.cancel"),
      },
      () => itemService.disableImage(itemId, imageId),
      "Error al desactivar la imagen.",
    );
  };

  const replaceUnitConversions = async (
    conversions: { fromUomCode: string; toUomCode: string; factor: number }[],
  ) => {
    if (!itemId) return;
    try {
      await itemService.replaceUnitConversions(itemId, conversions);
      await fetchItem();
    } catch {
      setError("Error al guardar conversiones.");
    }
  };

  const replaceSubstitutes = async (
    substitutes: {
      substituteItemId: string;
      priority: number;
      note?: string | null;
    }[],
  ) => {
    if (!itemId) return;
    try {
      await itemService.replaceSubstitutes(itemId, substitutes);
      await fetchItem();
    } catch {
      setError("Error al guardar sustitutos.");
    }
  };

  const replacePackagingLevels = async (
    levels: {
      id?: string | null;
      name: string;
      level: number;
      baseQuantity: number;
      uomCode: string;
      barcode?: string | null;
      weight?: number | null;
      isBaseUnit?: boolean;
      isPurchaseDefault?: boolean;
      isSaleDefault?: boolean;
    }[],
  ) => {
    if (!itemId) return;
    try {
      setItem(await itemService.replacePackagingLevels(itemId, levels));
      await fetchItem();
    } catch (err) {
      const message = formatApiRequestError(err, {
        generic: "Error al guardar niveles de empaque.",
      });
      setError(message);
      throw new Error(message, { cause: err });
    }
  };

  const updateSupplierCodePresentation = async (
    supplierId: string,
    code: string,
    packagingLevelId: string | null,
  ) => {
    if (!itemId) return;
    try {
      setItem(
        await itemService.updateSupplierCodePresentation(itemId, {
          supplierId,
          code,
          packagingLevelId,
        }),
      );
    } catch {
      setError("Error al guardar la presentación del código proveedor.");
    }
  };

  return {
    item,
    loading,
    error,
    refetch: fetchItem,
    toggleVariant,
    disableImage,
    enableImage,
    imageActivationError,
    statusChangePending,
    replaceUnitConversions,
    replaceSubstitutes,
    replacePackagingLevels,
    updateSupplierCodePresentation,
  };
}
