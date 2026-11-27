import { ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZhSelect } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type { SessionBranchDto } from "../../session/facades/sessionBranchFacade";
import type {
  CustomerAdvanceListFilters,
  CustomerAdvanceStatus,
} from "../api/customerAdvanceService";
import { advanceStatusKey } from "../utils/customerAdvance";
import { CustomerAdvanceCustomerPicker } from "./CustomerAdvanceCustomerPicker";

/** Filtros del contrato oficial (cliente, estado, sucursal), resueltos en el servidor. */
export function CustomerAdvanceFilters({
  value,
  branches,
  onChange,
  onReset,
}: {
  value: CustomerAdvanceListFilters;
  branches: SessionBranchDto[];
  onChange: (patch: CustomerAdvanceListFilters) => void;
  onReset: () => void;
}) {
  const { t } = useI18n();
  const customerLabel = t("finance.customerAdvances.field.customer");
  const statusLabel = t("finance.customerAdvances.field.status");
  const branchLabel = t("finance.customerAdvances.field.branch");
  return (
    <ZHFilterBar onClear={onReset} clearLabel={t("finance.customerAdvances.filters.reset")}>
      <ZHField label={customerLabel}>
        <CustomerAdvanceCustomerPicker
          // El buscador conserva su selección interna: se remonta al restablecer.
          key={value.customerId ?? "all"}
          value={value.customerId ?? null}
          onChange={(customerId) => onChange({ customerId })}
        />
      </ZHField>

      <ZHField label={statusLabel}>
        <ZhSelect
          aria-label={statusLabel}
          value={value.status ?? ""}
          onChange={(event) =>
            onChange({ status: event.target.value as CustomerAdvanceStatus | "" })
          }
        >
          <option value="">{t("finance.customerAdvances.filters.all")}</option>
          <option value="Confirmed">{t(advanceStatusKey("Confirmed"))}</option>
          <option value="Cancelled">{t(advanceStatusKey("Cancelled"))}</option>
        </ZhSelect>
      </ZHField>

      <ZHField label={branchLabel}>
        <ZhSelect
          aria-label={branchLabel}
          value={value.branchId ?? ""}
          onChange={(event) => onChange({ branchId: event.target.value || null })}
        >
          <option value="">{t("finance.customerAdvances.filters.all")}</option>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
    </ZHFilterBar>
  );
}
