import { ZHField } from "../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { SupplierPicker } from "../../purchases/components/SupplierPicker";
import type { PayableOriginType, PayableStatus } from "../api/payablesService";

export interface PayablesFiltersValue {
  search: string;
  originType: PayableOriginType | "";
  status: PayableStatus | "";
  dueDateFrom: string;
  dueDateTo: string;
  supplierId: string | null;
}

interface Props {
  value: PayablesFiltersValue;
  onChange: (patch: Partial<PayablesFiltersValue>) => void;
}

/** Barra de filtros de la API genérica de CxP — todo cambio resetea la pagina a 1 en el caller. */
export function PayablesFilters({ value, onChange }: Props) {
  return (
    <>
      <ZHField label="Buscar">
        <ZhTextInput
          value={value.search}
          placeholder="Documento o proveedor..."
          onChange={(event) => onChange({ search: event.target.value })}
        />
      </ZHField>

      <ZHField label="Proveedor">
        <SupplierPicker
          value={value.supplierId}
          onChange={(supplier) => onChange({ supplierId: supplier?.id ?? null })}
        />
      </ZHField>

      <ZHField label="Origen">
        <ZhSelect
          value={value.originType}
          onChange={(event) =>
            onChange({ originType: event.target.value as PayableOriginType | "" })
          }
        >
          <option value="">Todos</option>
          <option value="PurchaseInvoice">Compra</option>
          <option value="ExpenseDocument">Gasto</option>
          <option value="Manual">Manual</option>
        </ZhSelect>
      </ZHField>

      <ZHField label="Estado">
        <ZhSelect
          value={value.status}
          onChange={(event) => onChange({ status: event.target.value as PayableStatus | "" })}
        >
          <option value="">Todos</option>
          <option value="pending">Pendiente</option>
          <option value="partiallypaid">Parcial</option>
          <option value="paid">Pagada</option>
          <option value="cancelled">Anulada</option>
        </ZhSelect>
      </ZHField>

      <ZHField label="Vence desde">
        <ZhDateInput
          value={value.dueDateFrom}
          onChange={(event) => onChange({ dueDateFrom: event.target.value })}
        />
      </ZHField>

      <ZHField label="Vence hasta">
        <ZhDateInput
          value={value.dueDateTo}
          onChange={(event) => onChange({ dueDateTo: event.target.value })}
        />
      </ZHField>
    </>
  );
}
