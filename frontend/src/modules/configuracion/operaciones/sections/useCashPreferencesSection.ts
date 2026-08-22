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
  type CashPreferencesDto,
} from "../api/operationalPreferencesService";
import {
  cashPreferencesSchema,
  type CashPreferencesValues,
} from "../schemas/operationalPreferencesSchemas";

export function useCashPreferencesSection() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");
  const canEdit = canShow("settings.operations.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [fullGroup, setFullGroup] = useState<CashPreferencesDto | null>(null);

  const settingsState = useAsync(
    () => operationalPreferencesService.getPreferences(),
    canView,
  );

  const form = useForm<CashPreferencesValues>({
    resolver: zodResolver(cashPreferencesSchema),
    defaultValues: {
      requireReasonForDifference: true,
      allowCloseWithDifference: true,
      maxAllowedDifference: 0,
    },
  });

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = form;
  const requireReasonForDifferenceValue = watch("requireReasonForDifference");
  const allowCloseWithDifferenceValue = watch("allowCloseWithDifference");

  const resetFromData = (dto: CashPreferencesDto) => {
    setFullGroup(dto);
    reset({
      requireReasonForDifference: dto.requireReasonForDifference,
      allowCloseWithDifference: dto.allowCloseWithDifference,
      maxAllowedDifference: dto.maxAllowedDifference,
    });
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d.cash);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !fullGroup) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await operationalPreferencesService.updatePreferences({
        cash: { ...fullGroup, ...values },
      });
      setSaved(true);
      resetFromData(updated.cash);
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) => setSaveError(msg));
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
    if (settingsState.data) resetFromData(settingsState.data.cash);
  };

  return {
    canView,
    canEdit,
    saving,
    saveError,
    saved,
    settingsState,
    form,
    errors,
    isDirty,
    requireReasonForDifferenceValue,
    allowCloseWithDifferenceValue,
    register,
    setValue,
    onSubmit,
    handleDiscard,
  };
}
