import { useCallback, useEffect, useState } from "react";
import { itemService } from "../../api/itemService";
import type { ItemDetailDto } from "../../../../types/items";

export function useItemDetailPage(itemId: string | undefined) {
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

  const toggleVariant = async (variantId: string, enable: boolean) => {
    if (!itemId) return;
    try {
      if (enable) await itemService.enableVariant(itemId, variantId);
      else await itemService.disableVariant(itemId, variantId);
      await fetchItem();
    } catch {
      setError("Error al cambiar estado de la variante.");
    }
  };

  const disableImage = async (imageId: string) => {
    if (!itemId) return;
    try {
      await itemService.disableImage(itemId, imageId);
      await fetchItem();
    } catch {
      setError("Error al desactivar la imagen.");
    }
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
      await itemService.replacePackagingLevels(itemId, levels);
      await fetchItem();
    } catch {
      setError("Error al guardar niveles de empaque.");
    }
  };

  return {
    item,
    loading,
    error,
    refetch: fetchItem,
    toggleVariant,
    disableImage,
    replaceUnitConversions,
    replaceSubstitutes,
    replacePackagingLevels,
  };
}
