import { ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { SupplierSearchSelect } from "../../masterData/components/SupplierSearchSelect";
import type { SupplierCreditSourceType } from "../api/supplierCreditService";
import type {
  SupplierCreditFiltersValue,
  SupplierCreditStatusFilter,
} from "../utils/supplierCreditFilters";
import { supplierCreditSourceLabel } from "../utils/supplierCreditOrigin";

interface Props {
  value: SupplierCreditFiltersValue;
  onChange: (patch: Partial<SupplierCreditFiltersValue>) => void;
  onReset: () => void;
}

/**
 * ZH-SUPPLIER-BALANCES-UX-02D-E — barra de filtros de "Saldos a favor de proveedores" (mismo
 * patrón que `PayablesFilters`): proveedor (buscador oficial), origen y estado. Todo cambio se
 * resuelve en el servidor; el caller resetea la página a 1.
 */
export function SupplierCreditFilters({ value, onChange, onReset }: Props) {
  return (
    <ZHFilterBar onClear={onReset} clearLabel="Restablecer filtros">
      <ZHField label="Proveedor">
        <SupplierSearchSelect
          value={value.supplierId}
          onChange={(supplier) => onChange({ supplierId: supplier?.id ?? null })}
          aria-label="Proveedor"
        />
      </ZHField>

      <ZHField label="Origen">
        <ZhSelect
          aria-label="Origen"
          value={value.sourceType}
          onChange={(event) =>
            onChange({ sourceType: event.target.value as SupplierCreditSourceType | "" })
          }
        >
          <option value="">Todos</option>
          <option value="SupplierPayment">{supplierCreditSourceLabel("SupplierPayment")}</option>
          <option value="PurchaseReturn">{supplierCreditSourceLabel("PurchaseReturn")}</option>
        </ZhSelect>
      </ZHField>

      <ZHField label="Estado">
        <ZhSelect
          aria-label="Estado"
          value={value.status}
          onChange={(event) => onChange({ status: event.target.value as SupplierCreditStatusFilter })}
        >
          <option value="open">Abiertos</option>
          <option value="closed">Cerrados</option>
          <option value="all">Todos</option>
        </ZhSelect>
      </ZHField>
    </ZHFilterBar>
  );
}
