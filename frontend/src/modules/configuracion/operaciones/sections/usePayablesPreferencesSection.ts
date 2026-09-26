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
  type PayablesPreferencesDto,
} from "../api/operationalPreferencesService";
import {
  payablesPreferencesSchema,
  type PayablesPreferencesValues,
} from "../schemas/operationalPreferencesSchemas";

/** ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — mismo patrón que usePurchasesPreferencesSection. */
export function usePayablesPreferencesSection() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");
  const canEdit = canShow("settings.operations.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [fullGroup, setFullGroup] = useState<PayablesPreferencesDto | null>(null);

  const settingsState = useAsync(
    () => operationalPreferencesService.getPreferences(),
    canView,
  );

  const form = useForm<PayablesPreferencesValues>({
    resolver: zodResolver(payablesPreferencesSchema),
    defaultValues: { allowSupplierPaymentWithoutPayable: false },
  });

  const {
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { isDirty },
  } = form;
  const allowSupplierPaymentWithoutPayableValue = watch("allowSupplierPaymentWithoutPayable");

  const resetFromData = (dto: PayablesPreferencesDto) => {
    setFullGroup(dto);
    reset({ allowSupplierPaymentWithoutPayable: dto.allowSupplierPaymentWithoutPayable });
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d.payables);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !fullGroup) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await operationalPreferencesService.updatePreferences({
        payables: { ...fullGroup, ...values },
      });
      setSaved(true);
      resetFromData(updated.payables);
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
    if (settingsState.data) resetFromData(settingsState.data.payables);
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
    allowSupplierPaymentWithoutPayableValue,
    setValue,
    onSubmit,
    handleDiscard,
  };
}
