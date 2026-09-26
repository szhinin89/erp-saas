import { useCallback, useEffect, useState } from "react";
import { Badge } from "../../../components/PageShell";
import { ZhSearchSelect } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type { SupplierPickerRow } from "../types/businessPartner.types";
import {
  SUPPLIER_SEARCH_MAX_RESULTS,
  getSupplierPickerRow,
  searchSuppliers,
} from "../utils/supplierSearch";

const getKey = (o: SupplierPickerRow) => o.id;
const getLabel = (o: SupplierPickerRow) => o.fullName;
const getDescription = (o: SupplierPickerRow) => o.identificationNumber;
const getChipLabel = (o: SupplierPickerRow) => `${o.fullName} — ${o.identificationNumber}`;

interface CommonProps {
  disabled?: boolean;
  placeholder?: string;
  /** Solo proveedores con BP activo. Default: todos (los inactivos se marcan con badge). */
  activeOnly?: boolean;
  id?: string;
  "aria-label"?: string;
}

export type SupplierSearchSelectProps = CommonProps &
  (
    | {
        mode?: "single";
        /** Id del proveedor (se hidrata vía GET por id, p.ej. formularios RHF) o la fila ya
         * resuelta. `null`/`""` = sin selección. */
        value: SupplierPickerRow | string | null;
        onChange: (value: SupplierPickerRow | null) => void;
      }
    | {
        mode: "multiple";
        value: readonly SupplierPickerRow[];
        onChange: (value: SupplierPickerRow[]) => void;
      }
  );

/**
 * ZH-SUPPLIER-SEARCH-REUSABLE-01 / ZH-SUPPLIER-SEARCH-SINGLE-SOURCE-02 — buscador ÚNICO de
 * proveedores registrados (BusinessPartner con rol Supplier) para todo el ERP; reemplaza a
 * `SupplierPicker` y al selector propio de Gastos. Nunca carga el catálogo completo: búsqueda
 * remota desde 2 caracteres, debounce 300 ms, cancela la request anterior, máx. 30 resultados.
 * Para proveedores NO registrados (p.ej. Recepción TXT) usar `ZhSearchSelect` con datasource local.
 */
export function SupplierSearchSelect(props: SupplierSearchSelectProps) {
  const { t } = useI18n();
  const { activeOnly = false } = props;
  const loadOptions = useCallback(
    (query: string, signal: AbortSignal) => searchSuppliers(query, { activeOnly }, signal),
    [activeOnly],
  );

  // Hidratación de la selección inicial por id (single). Se cachea la última fila elegida/resuelta
  // para no volver a pedirla cuando el padre devuelve el mismo id tras onChange.
  const singleValue = props.mode === "multiple" ? null : props.value;
  const valueId = typeof singleValue === "string" && singleValue ? singleValue : null;
  const [resolved, setResolved] = useState<SupplierPickerRow | null>(null);
  useEffect(() => {
    if (!valueId || resolved?.id === valueId) return;
    let cancelled = false;
    getSupplierPickerRow(valueId)
      .then((row) => {
        if (!cancelled) setResolved(row);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [valueId, resolved?.id]);

  const resolvedById = valueId && resolved?.id === valueId ? resolved : null;
  const selectedRow = typeof singleValue === "string" ? resolvedById : singleValue;

  const shared = {
    getOptionKey: getKey,
    getOptionLabel: getLabel,
    getOptionDescription: getDescription,
    getChipLabel,
    getOptionMeta: (o: SupplierPickerRow) =>
      o.isActive ? null : (
        <Badge
          variant="warning"
          label={t("purchases.supplier.inactiveBadge", "Proveedor inactivo")}
        />
      ),
    loadOptions,
    maxResults: SUPPLIER_SEARCH_MAX_RESULTS,
    disabled: props.disabled,
    id: props.id,
    "aria-label": props["aria-label"],
    placeholder:
      props.placeholder ??
      t("purchases.supplierPicker.placeholder", "Buscar por RUC, razón social o nombre..."),
    emptyText: (query: string) => (
      <>
        <div>{t("purchases.supplierPicker.noResults", { query })}</div>
        <div className="zh-picker__empty-help">
          {t("purchases.supplierPicker.emptyHelp", "¿Aún no tiene proveedores registrados?")}{" "}
          <a href="/suppliers" className="zh-picker__link">
            {t("purchases.supplierPicker.register", "Registre un proveedor")}
          </a>
        </div>
      </>
    ),
    loadingText: t("common.searching", "Buscando..."),
    truncatedText: t("supplierSearch.truncated", "Siga escribiendo para refinar la búsqueda."),
    removeLabel: t("supplierSearch.remove", "Quitar proveedor"),
  };

  if (props.mode === "multiple") {
    return (
      <ZhSearchSelect<SupplierPickerRow>
        {...shared}
        mode="multiple"
        clearLabel={t("supplierSearch.clear", "Limpiar selección")}
        value={props.value}
        onChange={props.onChange}
      />
    );
  }

  const onSingleChange = props.onChange;
  return (
    <ZhSearchSelect<SupplierPickerRow>
      {...shared}
      mode="single"
      clearLabel={t("purchases.supplierPicker.change", "Cambiar proveedor")}
      value={selectedRow}
      onChange={(row) => {
        setResolved(row);
        onSingleChange(row);
      }}
    />
  );
}
