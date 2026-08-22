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
  type PrintingPreferencesDto,
} from "../api/operationalPreferencesService";
import {
  printingPreferencesSchema,
  type PrintingPreferencesValues,
} from "../schemas/operationalPreferencesSchemas";

export function usePrintingPreferencesSection() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");
  const canEdit = canShow("settings.operations.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [fullGroup, setFullGroup] = useState<PrintingPreferencesDto | null>(null);

  const settingsState = useAsync(
    () => operationalPreferencesService.getPreferences(),
    canView,
  );

  const form = useForm<PrintingPreferencesValues>({
    resolver: zodResolver(printingPreferencesSchema),
    defaultValues: { salesReceiptMode: "AskBeforePrint", salesReceiptCopies: 1 },
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = form;

  const resetFromData = (dto: PrintingPreferencesDto) => {
    setFullGroup(dto);
    reset({
      salesReceiptMode: dto.salesReceiptMode,
      salesReceiptCopies: dto.salesReceiptCopies,
    });
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d.printing);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !fullGroup) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await operationalPreferencesService.updatePreferences({
        printing: { ...fullGroup, ...values },
      });
      setSaved(true);
      resetFromData(updated.printing);
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
    if (settingsState.data) resetFromData(settingsState.data.printing);
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
    register,
    onSubmit,
    handleDiscard,
  };
}
