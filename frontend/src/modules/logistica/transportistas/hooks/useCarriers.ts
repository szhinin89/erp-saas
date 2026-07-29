import { useState } from "react";
import { useAsync } from "../../../../hooks/useAsync";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  carrierService,
  type CreateCarrierRequest,
} from "../api/carrierService";

export function useCarriers() {
  const listState = useAsync(() => carrierService.getAll());
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const createCarrier = async (payload: CreateCarrierRequest) => {
    setSaveError(null);
    setSaving(true);
    try {
      const created = await carrierService.create(payload);
      listState.refetch();
      return created;
    } catch (err) {
      setSaveError(
        formatApiRequestError(err, {
          generic: "Error al guardar el transportista.",
        }),
      );
      return null;
    } finally {
      setSaving(false);
    }
  };

  const updateCarrier = async (id: string, payload: CreateCarrierRequest) => {
    setSaveError(null);
    setSaving(true);
    try {
      const updated = await carrierService.update(id, payload);
      listState.refetch();
      return updated;
    } catch (err) {
      setSaveError(
        formatApiRequestError(err, {
          generic: "Error al guardar el transportista.",
        }),
      );
      return null;
    } finally {
      setSaving(false);
    }
  };

  const toggleCarrierStatus = async (id: string, activate: boolean) => {
    setSaveError(null);
    setSaving(true);
    try {
      const updated = activate
        ? await carrierService.enable(id)
        : await carrierService.disable(id);
      listState.refetch();
      return updated;
    } catch (err) {
      setSaveError(
        formatApiRequestError(err, {
          generic: "Error al guardar el transportista.",
        }),
      );
      return null;
    } finally {
      setSaving(false);
    }
  };

  return {
    carriers: listState.data ?? [],
    loading: listState.loading,
    error: listState.error,
    saving,
    saveError,
    createCarrier,
    updateCarrier,
    toggleCarrierStatus,
  };
}
