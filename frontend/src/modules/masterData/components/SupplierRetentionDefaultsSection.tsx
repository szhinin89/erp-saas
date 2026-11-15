import { useCallback, useEffect, useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid, ZHToggle } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhSelect } from "../../../components/zh/inputs";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { useSriRetentionCodes } from "../api/useSriRetentionCodes";
import type { SupplierRetentionDefaultDto } from "../types/businessPartner.types";

// ── SupplierRetentionDefaultsSection — RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 ──
// Lista dinámica de retenciones predeterminadas por proveedor+empresa activa. Reemplaza los
// antiguos selects fijos "Código ret. IVA/Renta predeterminado" (máx. 1 código por impuesto,
// tenant-wide). Mismo patrón de sección independiente que "Condición predeterminada para
// compras/gastos": fetch/save propios, desacoplada del form de Config SRI.
// (Extraída de MasterDataSuppliersPage sin cambio de comportamiento.)
export function SupplierRetentionDefaultsSection({
  bpId,
  disabled,
}: {
  bpId: string;
  disabled?: boolean;
}) {
  const [rows, setRows] = useState<SupplierRetentionDefaultDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [newTaxType, setNewTaxType] = useState<"IVA" | "RENTA">("IVA");
  const [newCodeId, setNewCodeId] = useState("");
  const [adding, setAdding] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  const { options: vatOptions, loading: loadingVat } =
    useSriRetentionCodes("IVA");
  const { options: incomeOptions, loading: loadingIncome } =
    useSriRetentionCodes("RENTA");
  const catalogOptions = newTaxType === "IVA" ? vatOptions : incomeOptions;
  const loadingCatalog = newTaxType === "IVA" ? loadingVat : loadingIncome;

  const load = useCallback(() => {
    setLoading(true);
    businessPartnerFacade
      .getRetentionDefaults(bpId)
      .then(setRows)
      .catch((err: unknown) =>
        setError(
          formatApiRequestError(err, {
            generic: "No se pudieron cargar las retenciones predeterminadas.",
          }),
        ),
      )
      .finally(() => setLoading(false));
  }, [bpId]);

  useEffect(() => {
    load();
  }, [load]);

  const availableCodes = catalogOptions.filter(
    (o) => !rows.some((r) => r.sriRetentionCodeId === o.id),
  );

  const sortedRows = [...rows].sort((a, b) => a.displayOrder - b.displayOrder);

  const handleAdd = async () => {
    if (!newCodeId) return;
    setAdding(true);
    setError(null);
    try {
      const created = await businessPartnerFacade.addRetentionDefault(bpId, {
        sriRetentionCodeId: newCodeId,
      });
      setRows((prev) => [...prev, created]);
      setNewCodeId("");
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo agregar la retención predeterminada.",
        }),
      );
    } finally {
      setAdding(false);
    }
  };

  const handleToggleActive = async (
    row: SupplierRetentionDefaultDto,
    next: boolean,
  ) => {
    setBusyId(row.id);
    setError(null);
    try {
      const updated = await businessPartnerFacade.setRetentionDefaultState(
        bpId,
        row.id,
        { isActive: next, displayOrder: row.displayOrder },
      );
      setRows((prev) => prev.map((r) => (r.id === row.id ? updated : r)));
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo actualizar la retención predeterminada.",
        }),
      );
    } finally {
      setBusyId(null);
    }
  };

  const handleMove = async (row: SupplierRetentionDefaultDto, direction: -1 | 1) => {
    const index = sortedRows.findIndex((r) => r.id === row.id);
    const other = sortedRows[index + direction];
    if (!other) return;
    setBusyId(row.id);
    setError(null);
    try {
      const [updatedRow, updatedOther] = await Promise.all([
        businessPartnerFacade.setRetentionDefaultState(bpId, row.id, {
          isActive: row.isActive,
          displayOrder: other.displayOrder,
        }),
        businessPartnerFacade.setRetentionDefaultState(bpId, other.id, {
          isActive: other.isActive,
          displayOrder: row.displayOrder,
        }),
      ]);
      setRows((prev) =>
        prev.map((r) =>
          r.id === updatedRow.id
            ? updatedRow
            : r.id === updatedOther.id
              ? updatedOther
              : r,
        ),
      );
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo reordenar la retención predeterminada.",
        }),
      );
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="md-supplier-purchase-default-section">
      <h4>Retenciones predeterminadas</h4>
      <p className="md-supplier-section-hint">
        Se proponen automáticamente al calcular retenciones en Compras/Gastos
        para este proveedor en la empresa activa. El usuario puede
        modificarlas antes de confirmar. La retención final emitida se guarda
        siempre en el documento de retención, nunca aquí.
      </p>
      {error && <ZHPageNotice variant="error" message={error} />}

      {loading ? (
        <p className="md-supplier-section-hint">Cargando…</p>
      ) : sortedRows.length === 0 ? (
        <p className="md-supplier-section-hint">
          Sin retenciones predeterminadas configuradas para esta empresa.
        </p>
      ) : (
        <ul className="md-supplier-retention-list">
          {sortedRows.map((row, index) => (
            <li
              key={row.id}
              className={`md-supplier-retention-row${row.isActive ? "" : " md-supplier-retention-row--inactive"}`}
            >
              <span className="md-supplier-retention-row__code">
                {row.taxType} — {row.code} — {row.codeName} (
                {row.percentage}%)
              </span>
              {!row.isCatalogCodeActive && (
                <span className="md-supplier-retention-row__warning">
                  Código inactivo en catálogo
                </span>
              )}
              <div className="md-supplier-retention-row__actions">
                <ZHIconButton
                  icon="arrow_upward"
                  title="Subir"
                  ariaLabel={`Subir prioridad de ${row.code}`}
                  variant="ghost"
                  disabled={disabled || busyId === row.id || index === 0}
                  onClick={() => void handleMove(row, -1)}
                />
                <ZHIconButton
                  icon="arrow_downward"
                  title="Bajar"
                  ariaLabel={`Bajar prioridad de ${row.code}`}
                  variant="ghost"
                  disabled={
                    disabled ||
                    busyId === row.id ||
                    index === sortedRows.length - 1
                  }
                  onClick={() => void handleMove(row, 1)}
                />
                <ZHToggle
                  label={row.isActive ? "Activa" : "Inactiva"}
                  description="Se propone automáticamente al calcular retenciones si está activa"
                  value={row.isActive}
                  onChange={(next) => void handleToggleActive(row, next)}
                  disabled={disabled || busyId === row.id}
                />
              </div>
            </li>
          ))}
        </ul>
      )}

      <ZHGrid cols={3}>
        <ZHField label="Tipo de impuesto">
          <ZhSelect
            value={newTaxType}
            onChange={(e) => {
              setNewTaxType(e.target.value as "IVA" | "RENTA");
              setNewCodeId("");
            }}
            disabled={disabled || adding}
          >
            <option value="IVA">IVA</option>
            <option value="RENTA">Renta</option>
          </ZhSelect>
        </ZHField>
        <ZHField label="Código de retención SRI">
          <ZhSelect
            value={newCodeId}
            onChange={(e) => setNewCodeId(e.target.value)}
            disabled={disabled || adding || loadingCatalog}
          >
            <option value="">— Seleccione —</option>
            {loadingCatalog ? (
              <option value="">Cargando…</option>
            ) : (
              availableCodes.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.code} — {o.name} ({o.percentage}%)
                </option>
              ))
            )}
          </ZhSelect>
        </ZHField>
        <div className="md-supplier-retention-add-btn">
          <ZHBtn
            type="button"
            variant="secondary"
            size="sm"
            disabled={disabled || adding || !newCodeId}
            onClick={() => void handleAdd()}
          >
            {adding ? "Agregando..." : "Agregar retención"}
          </ZHBtn>
        </div>
      </ZHGrid>
    </div>
  );
}
