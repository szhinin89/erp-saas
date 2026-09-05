import { useCallback, useEffect, useMemo, useState } from "react";
import axios from "axios";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  documentSequencesService,
  type DocumentSequenceDto,
} from "../api/documentSequencesService";
import {
  emissionPointsService,
  type EmissionPointListItemDto,
} from "../../emissionPoints/api/emissionPointsService";
import {
  sriLookupService,
  type SriDocTypeLookup,
} from "../../items/catalog/api/catalogService";
import {
  documentSequenceConfigureSchema,
  emptyDocumentSequenceConfigureForm,
  type DocumentSequenceConfigureFormValues,
} from "../schemas/documentSequencesPageSchema";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

/** Estado de una secuencia para un (EmissionPoint, DocType) — nunca deriva de CurrentSeq > 1. */
export type SequenceRowStatus = "not_configured" | "configured" | "used";

export type DocumentSequenceRow = {
  emissionPointId: string;
  docTypeCode: string;
  docTypeName: string;
  nextNumber: number | null;
  status: SequenceRowStatus;
};

export function useDocumentSequencesPage() {
  const { canShow } = usePermissionsUi();
  const canManage = canShow("settings.document-sequences.manage");

  // ── Datos de referencia ──────────────────────────────────────────────────
  const [emissionPoints, setEmissionPoints] = useState<
    EmissionPointListItemDto[]
  >([]);
  const [docTypes, setDocTypes] = useState<SriDocTypeLookup[]>([]);
  const [sequences, setSequences] = useState<DocumentSequenceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // ── Selección de punto de emisión ────────────────────────────────────────
  const [selectedEmissionPointId, setSelectedEmissionPointId] = useState<
    string | null
  >(null);

  // ── Panel de configuración ───────────────────────────────────────────────
  const [panelOpen, setPanelOpen] = useState(false);
  const [editingRow, setEditingRow] = useState<DocumentSequenceRow | null>(
    null,
  );
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");

  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<DocumentSequenceConfigureFormValues>({
    resolver: zodResolver(documentSequenceConfigureSchema),
    defaultValues: emptyDocumentSequenceConfigureForm(),
  });

  // ── Carga de datos ────────────────────────────────────────────────────────
  const fetchAll = useCallback(async () => {
    setError("");
    setLoading(true);
    try {
      const [epList, docTypeList, sequenceList] = await Promise.all([
        emissionPointsService.list("active"),
        sriLookupService.docTypes(),
        documentSequencesService.list(),
      ]);
      setEmissionPoints(epList);
      // Tipos de documento SOPORTADOS por esta pantalla: los que el ERP emite electrónicamente
      // (catálogo real, nunca lista estática — ver DOCUMENT-SEQUENCES-CONFIG-UI-04).
      setDocTypes(docTypeList.filter((d) => d.isElectronic));
      setSequences(sequenceList);
    } catch {
      setError("Error al cargar las secuencias documentales.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchAll();
  }, [fetchAll]);

  // Selecciona automáticamente el primer punto de emisión disponible al cargar.
  useEffect(() => {
    if (!selectedEmissionPointId && emissionPoints.length > 0) {
      setSelectedEmissionPointId(emissionPoints[0].id);
    }
  }, [emissionPoints, selectedEmissionPointId]);

  const selectedEmissionPoint =
    emissionPoints.find((ep) => ep.id === selectedEmissionPointId) ?? null;

  // ── Matriz: tipos de documento soportados x estado de secuencia ──────────
  const rows: DocumentSequenceRow[] = useMemo(() => {
    if (!selectedEmissionPointId) return [];
    return docTypes.map((dt) => {
      const sequence = sequences.find(
        (s) =>
          s.emissionPointId === selectedEmissionPointId &&
          s.docTypeCode === dt.code,
      );
      const status: SequenceRowStatus = !sequence
        ? "not_configured"
        : sequence.hasBeenUsed
          ? "used"
          : "configured";
      return {
        emissionPointId: selectedEmissionPointId,
        docTypeCode: dt.code,
        docTypeName: dt.name,
        nextNumber: sequence?.nextNumber ?? null,
        status,
      };
    });
  }, [docTypes, sequences, selectedEmissionPointId]);

  // ── Panel — abrir para configurar/editar ──────────────────────────────────
  const openConfigure = (row: DocumentSequenceRow) => {
    if (row.status === "used") return;
    setEditingRow(row);
    setSaveError("");
    reset({ nextNumber: row.nextNumber ?? 1 });
    setPanelOpen(true);
  };

  const closePanel = () => {
    setPanelOpen(false);
    setEditingRow(null);
    setSaveError("");
  };

  // ── Guardar (con confirmación previa) ─────────────────────────────────────
  const save = handleSubmit(async (form) => {
    if (!editingRow || !selectedEmissionPoint) return;

    const formattedNumber = String(form.nextNumber).padStart(9, "0");
    const confirmed = await message.confirm({
      title: "Confirmar secuencial",
      message: `El próximo documento tipo ${editingRow.docTypeName} del punto ${selectedEmissionPoint.establishmentCode}-${selectedEmissionPoint.code} usará el secuencial ${formattedNumber}. Esta configuración solo puede cambiarse libremente mientras no se haya usado.`,
      variant: "warning",
      confirmLabel: "Confirmar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    setSaveError("");
    setSaving(true);
    try {
      await documentSequencesService.configure({
        emissionPointId: editingRow.emissionPointId,
        docTypeCode: editingRow.docTypeCode,
        nextNumber: form.nextNumber,
      });
      await fetchAll();
      message.success("Secuencia documental configurada correctamente.");
      closePanel();
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 409) {
        setSaveError(
          "La secuencia ya fue usada y no puede modificarse desde esta pantalla.",
        );
        return;
      }
      if (axios.isAxiosError(err) && err.response?.status === 404) {
        setSaveError(
          "El punto de emisión no existe o no pertenece a la empresa activa.",
        );
        return;
      }
      const applied = applyServerErrors(err, setFieldError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, {
            generic: "Error al configurar la secuencia documental.",
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  return {
    // Permisos
    canManage,
    // Datos
    emissionPoints,
    docTypes,
    loading,
    error,
    rows,
    fetchAll,
    // Selección
    selectedEmissionPointId,
    setSelectedEmissionPointId,
    selectedEmissionPoint,
    // Panel
    panelOpen,
    editingRow,
    saving,
    saveError,
    register,
    errors,
    openConfigure,
    closePanel,
    save,
  };
}

export type DocumentSequencesPageContext = ReturnType<
  typeof useDocumentSequencesPage
>;
