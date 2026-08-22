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
  type SalesPosPreferencesDto,
} from "../api/operationalPreferencesService";
import {
  salesPosPreferencesSchema,
  type SalesPosPreferencesValues,
} from "../schemas/operationalPreferencesSchemas";

export function useSalesPosPreferencesSection() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");
  const canEdit = canShow("settings.operations.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [fullGroup, setFullGroup] = useState<SalesPosPreferencesDto | null>(null);

  const settingsState = useAsync(
    () => operationalPreferencesService.getPreferences(),
    canView,
  );

  const form = useForm<SalesPosPreferencesValues>({
    resolver: zodResolver(salesPosPreferencesSchema),
    defaultValues: { allowManualDiscount: true, maxDiscountPercent: 0 },
  });

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = form;
  const allowManualDiscountValue = watch("allowManualDiscount");

  const resetFromData = (dto: SalesPosPreferencesDto) => {
    setFullGroup(dto);
    reset({
      allowManualDiscount: dto.allowManualDiscount,
      maxDiscountPercent: dto.maxDiscountPercent,
    });
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d.salesPos);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !fullGroup) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await operationalPreferencesService.updatePreferences({
        salesPos: { ...fullGroup, ...values },
      });
      setSaved(true);
      resetFromData(updated.salesPos);
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
    if (settingsState.data) resetFromData(settingsState.data.salesPos);
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
    allowManualDiscountValue,
    register,
    setValue,
    onSubmit,
    handleDiscard,
  };
}
