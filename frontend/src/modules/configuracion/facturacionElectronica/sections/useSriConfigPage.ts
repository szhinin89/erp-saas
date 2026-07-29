import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useAsync } from "../../../../hooks/useAsync";
import { useI18n } from "../../../../i18n/i18n";
import {
  electronicInvoicingService,
  type SriCertificateInfoDto,
  type SriConfigurationValidationDto,
} from "../api/electronicInvoicingService";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { useElectronicInvoicingStatusStore } from "../../../../store/electronicInvoicingStatusStore";
import {
  sriConfigSchema,
  SRI_WSDL_DEFAULTS,
  type SriConfigValues,
} from "../schemas/sriConfigurationSchema";

const ALLOWED_CERT_EXTENSIONS = [".p12", ".pfx"];
const MAX_CERT_SIZE_BYTES = 5 * 1024 * 1024;
const CERT_PASSWORD_INSPECT_DEBOUNCE_MS = 500;

export function useSriConfigPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("electronic-invoicing.view");
  const canEdit = canShow("electronic-invoicing.configure");

  // Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente"): 1=Pruebas, 2=Producción.
  const sriEnvOptions = [
    {
      value: 1 as const,
      label: t("settings.electronicInvoicing.sri.envTestLabel"),
      description: t("settings.electronicInvoicing.sri.envTestDesc"),
    },
    {
      value: 2 as const,
      label: t("settings.electronicInvoicing.sri.envProdLabel"),
      description: t("settings.electronicInvoicing.sri.envProdDesc"),
    },
  ];

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [showPass, setShowPass] = useState(false);

  const [validating, setValidating] = useState(false);
  const [validationResult, setValidationResult] =
    useState<SriConfigurationValidationDto | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const [certUploading, setCertUploading] = useState(false);
  const [certUploadProgress, setCertUploadProgress] = useState(0);
  const [certUploadError, setCertUploadError] = useState<string | null>(null);
  const [certInspection, setCertInspection] =
    useState<SriCertificateInfoDto | null>(null);
  const [certInspecting, setCertInspecting] = useState(false);
  const certInspectTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const sriState = useAsync(
    () => electronicInvoicingService.getSriConfiguration(),
    canView,
  );

  const form = useForm<SriConfigValues>({
    resolver: zodResolver(sriConfigSchema),
    defaultValues: {
      certPassword: "",
      environment: 1,
      wsdlUrl: SRI_WSDL_DEFAULTS.pruebas,
    },
  });

  const {
    handleSubmit,
    reset,
    watch,
    setValue,
    getValues,
    formState: { errors, isDirty },
  } = form;
  const envValue = watch("environment");
  const certPasswordValue = watch("certPassword");

  const resetFromData = (d: NonNullable<typeof sriState.data>) => {
    reset({
      certPassword: "",
      environment: d.environment === 2 ? 2 : 1,
      wsdlUrl: d.wsdlUrl ?? SRI_WSDL_DEFAULTS.pruebas,
    });
  };

  useEffect(() => {
    const d = sriState.data;
    if (!d) return;
    resetFromData(d);
    setCertInspection(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sriState.data]);

  useEffect(() => {
    const currentUrl = getValues("wsdlUrl");
    const isDefault = Object.values(SRI_WSDL_DEFAULTS).includes(currentUrl);
    if (isDefault || !currentUrl) {
      setValue(
        "wsdlUrl",
        envValue === 2
          ? SRI_WSDL_DEFAULTS.produccion
          : SRI_WSDL_DEFAULTS.pruebas,
        {
          shouldDirty: true,
        },
      );
    }
  }, [envValue, getValues, setValue]);

  // Validación en vivo de la contraseña contra el certificado ya subido — sin persistirla.
  useEffect(() => {
    if (certInspectTimer.current) clearTimeout(certInspectTimer.current);
    if (!sriState.data?.hasCertificate || !certPasswordValue) {
      setCertInspection(null);
      return;
    }
    certInspectTimer.current = setTimeout(async () => {
      setCertInspecting(true);
      try {
        const result =
          await electronicInvoicingService.inspectCertificate(
            certPasswordValue,
          );
        setCertInspection(result);
      } catch {
        setCertInspection(null);
      } finally {
        setCertInspecting(false);
      }
    }, CERT_PASSWORD_INSPECT_DEBOUNCE_MS);
    return () => {
      if (certInspectTimer.current) clearTimeout(certInspectTimer.current);
    };
  }, [certPasswordValue, sriState.data?.hasCertificate]);

  const handleCertFileSelected = async (file: File) => {
    setCertUploadError(null);
    setCertInspection(null);

    const extension = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_CERT_EXTENSIONS.includes(extension)) {
      setCertUploadError(
        t("settings.electronicInvoicing.sri.certInvalidExtension"),
      );
      return;
    }
    if (file.size > MAX_CERT_SIZE_BYTES) {
      setCertUploadError(t("settings.electronicInvoicing.sri.certTooLarge"));
      return;
    }

    setCertUploading(true);
    setCertUploadProgress(0);
    try {
      const result = await electronicInvoicingService.uploadCertificate(
        file,
        setCertUploadProgress,
      );
      if (result.inspection) setCertInspection(result.inspection);
      sriState.refetch();
      void useElectronicInvoicingStatusStore.getState().refresh();
    } catch (err) {
      setCertUploadError(
        formatApiRequestError(err, {
          offline: t("settings.electronicInvoicing.sri.offlineError"),
          generic: t("settings.electronicInvoicing.sri.certUploadGenericError"),
        }),
      );
    } finally {
      setCertUploading(false);
    }
  };

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await electronicInvoicingService.upsertSriConfiguration({
        certPassword: values.certPassword ?? "",
        environment: values.environment,
        emissionType: 1,
        wsdlUrl: values.wsdlUrl,
      });
      setSaved(true);
      sriState.refetch();
      void useElectronicInvoicingStatusStore.getState().refresh();
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, {
            offline: t("settings.electronicInvoicing.sri.offlineError"),
            generic: t("settings.electronicInvoicing.sri.genericSaveError"),
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
    const d = sriState.data;
    if (d) resetFromData(d);
  };

  const handleValidate = async () => {
    setValidationError(null);
    setValidationResult(null);
    setValidating(true);
    try {
      const result =
        await electronicInvoicingService.validateSriConfiguration();
      setValidationResult(result);
    } catch (err) {
      setValidationError(
        formatApiRequestError(err, {
          offline: t("settings.electronicInvoicing.sri.offlineError"),
          generic: t("settings.electronicInvoicing.sri.validateGenericError"),
        }),
      );
    } finally {
      setValidating(false);
    }
  };

  return {
    canView,
    canEdit,
    saving,
    saveError,
    saved,
    showPass,
    setShowPass,
    sriState,
    sriEnvOptions,
    form,
    errors,
    isDirty,
    onSubmit,
    handleDiscard,
    validating,
    validationResult,
    validationError,
    handleValidate,
    certUploading,
    certUploadProgress,
    certUploadError,
    certInspection,
    certInspecting,
    handleCertFileSelected,
  };
}
