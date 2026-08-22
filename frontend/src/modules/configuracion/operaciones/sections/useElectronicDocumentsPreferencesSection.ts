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
  type ElectronicDocumentsPreferencesDto,
} from "../api/operationalPreferencesService";
import {
  electronicDocumentsPreferencesSchema,
  type ElectronicDocumentsPreferencesValues,
} from "../schemas/operationalPreferencesSchemas";

export function useElectronicDocumentsPreferencesSection() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");
  const canEdit = canShow("settings.operations.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [fullGroup, setFullGroup] = useState<ElectronicDocumentsPreferencesDto | null>(null);

  const settingsState = useAsync(
    () => operationalPreferencesService.getPreferences(),
    canView,
  );

  const form = useForm<ElectronicDocumentsPreferencesValues>({
    resolver: zodResolver(electronicDocumentsPreferencesSchema),
    defaultValues: { emailOnAuthorization: true },
  });

  const {
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { isDirty },
  } = form;
  const emailOnAuthorizationValue = watch("emailOnAuthorization");

  const resetFromData = (dto: ElectronicDocumentsPreferencesDto) => {
    setFullGroup(dto);
    reset({ emailOnAuthorization: dto.emailOnAuthorization });
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d.electronicDocuments);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !fullGroup) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await operationalPreferencesService.updatePreferences({
        electronicDocuments: { ...fullGroup, ...values },
      });
      setSaved(true);
      resetFromData(updated.electronicDocuments);
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
    if (settingsState.data) resetFromData(settingsState.data.electronicDocuments);
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
    emailOnAuthorizationValue,
    setValue,
    onSubmit,
    handleDiscard,
  };
}
