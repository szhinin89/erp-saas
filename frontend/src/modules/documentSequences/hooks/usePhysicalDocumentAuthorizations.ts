import { useCallback, useEffect, useMemo, useState } from "react";
import axios from "axios";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  physicalDocumentAuthorizationsService,
  type PhysicalDocumentAuthorizationDto,
} from "../api/physicalDocumentAuthorizationsService";
import {
  emissionPointLookupFacade,
  type EmissionPointListItemDto,
} from "../../emissionPoints/facades/emissionPointLookupFacade";
import {
  sriLookupFacade,
  type SriDocTypeLookup,
} from "../../items/facades/sriLookupFacade";
import {
  emptyPhysicalDocumentAuthorizationForm,
  physicalDocumentAuthorizationSchema,
  type PhysicalDocumentAuthorizationFormValues,
} from "../schemas/physicalDocumentAuthorizationSchema";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

/**
 * PHYSICAL-FISCAL-F1 — autorizaciones de impresión del SRI de los puntos de emisión físicos. Solo
 * registra y deshabilita: la política fiscal física (backend) decide si una caja puede vender.
 */
export function usePhysicalDocumentAuthorizations() {
  const { canShow } = usePermissionsUi();
  const canManage = canShow("settings.document-sequences.manage");

  const [physicalPoints, setPhysicalPoints] = useState<EmissionPointListItemDto[]>([]);
  const [docTypes, setDocTypes] = useState<SriDocTypeLookup[]>([]);
  const [authorizations, setAuthorizations] = useState<PhysicalDocumentAuthorizationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [selectedPointId, setSelectedPointId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");

  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<PhysicalDocumentAuthorizationFormValues>({
    resolver: zodResolver(physicalDocumentAuthorizationSchema),
    defaultValues: emptyPhysicalDocumentAuthorizationForm(),
  });

  const fetchAll = useCallback(async () => {
    setError("");
    setLoading(true);
    try {
      const [points, docTypeList, list] = await Promise.all([
        emissionPointLookupFacade.list("active"),
        sriLookupFacade.docTypes(),
        physicalDocumentAuthorizationsService.list(),
      ]);
      setPhysicalPoints(points.filter((p) => p.emissionType === "Physical"));
      setDocTypes(docTypeList);
      setAuthorizations(list);
    } catch {
      setError("Error al cargar las autorizaciones de impresión.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (canManage) void fetchAll();
  }, [canManage, fetchAll]);

  useEffect(() => {
    if (!selectedPointId && physicalPoints.length > 0) setSelectedPointId(physicalPoints[0].id);
  }, [physicalPoints, selectedPointId]);

  const selectedPoint = physicalPoints.find((p) => p.id === selectedPointId) ?? null;

  const rows = useMemo(
    () => authorizations.filter((a) => a.emissionPointId === selectedPointId),
    [authorizations, selectedPointId],
  );

  const docTypeName = (code: string) => docTypes.find((d) => d.code === code)?.name ?? code;

  const save = handleSubmit(async (form) => {
    if (!selectedPoint) return;
    setSaveError("");
    setSaving(true);
    try {
      await physicalDocumentAuthorizationsService.create({
        emissionPointId: selectedPoint.id,
        ...form,
      });
      reset(emptyPhysicalDocumentAuthorizationForm());
      await fetchAll();
      message.success("Autorización de impresión registrada.");
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 409) {
        setSaveError(
          "El rango de secuenciales se superpone con otra autorización activa del mismo punto y tipo.",
        );
        return;
      }
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied)
        setSaveError(
          formatApiRequestError(err, { generic: "Error al registrar la autorización." }),
        );
    } finally {
      setSaving(false);
    }
  });

  const disable = async (authorization: PhysicalDocumentAuthorizationDto) => {
    const confirmed = await message.confirm({
      title: "Deshabilitar autorización",
      message: `La autorización ${authorization.authorizationNumber} dejará de habilitar la emisión física de este punto. El registro se conserva.`,
      variant: "warning",
      confirmLabel: "Deshabilitar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    try {
      await physicalDocumentAuthorizationsService.disable(authorization.id);
      await fetchAll();
      message.success("Autorización deshabilitada.");
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "Error al deshabilitar la autorización." }),
      );
    }
  };

  return {
    canManage,
    loading,
    error,
    fetchAll,
    physicalPoints,
    docTypes,
    selectedPointId,
    setSelectedPointId,
    selectedPoint,
    rows,
    docTypeName,
    saving,
    saveError,
    register,
    errors,
    save,
    disable,
  };
}

export type PhysicalDocumentAuthorizationsContext = ReturnType<
  typeof usePhysicalDocumentAuthorizations
>;
