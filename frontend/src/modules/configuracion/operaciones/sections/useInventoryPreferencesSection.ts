import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useAsync } from "../../../../hooks/useAsync";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  operationalPreferencesService,
  type InventoryPreferencesDto,
} from "../api/operationalPreferencesService";
import {
  inventoryPreferencesSchema,
  type InventoryPreferencesValues,
} from "../schemas/operationalPreferencesSchemas";

/** A1 — control general de disponibilidad en la empresa actual. */
export function useInventoryPreferencesSection() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");
  const canEdit = canShow("settings.operations.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [fullGroup, setFullGroup] = useState<InventoryPreferencesDto | null>(
    null,
  );

  const settingsState = useAsync(
    () => operationalPreferencesService.getPreferences(),
    canView,
  );

  const form = useForm<InventoryPreferencesValues>({
    resolver: zodResolver(inventoryPreferencesSchema),
    defaultValues: { stockControlEnabled: true },
  });

  const {
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { isDirty },
  } = form;
  const stockControlEnabledValue = watch("stockControlEnabled");

  const resetFromData = (dto: InventoryPreferencesDto) => {
    setFullGroup(dto);
    reset({ stockControlEnabled: dto.stockControlEnabled });
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d.inventory);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !fullGroup) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await operationalPreferencesService.updatePreferences({
        inventory: { ...fullGroup, ...values },
      });
      setSaved(true);
      resetFromData(updated.inventory);
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, {
            offline: t("settings.operations.offlineError"),
            generic: t("settings.operations.genericSaveError"),
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    if (settingsState.data) resetFromData(settingsState.data.inventory);
  };

  return {
    canView,
    canEdit,
    saving,
    saveError,
    saved,
    settingsState,
    form,
    isDirty,
    stockControlEnabledValue,
    setValue,
    onSubmit,
    handleDiscard,
  };
}
