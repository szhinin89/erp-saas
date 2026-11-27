import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { CustomerPicker } from "../../sales/facades/customerPickerFacade";
import { CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS } from "../utils/customerAdvance";

/**
 * Buscador oficial de clientes. Sin su permiso se explica la causa en lugar de mostrar un buscador
 * que no devolvería resultados.
 */
export function CustomerAdvanceCustomerPicker({
  value,
  onChange,
  disabled,
}: {
  value: string | null;
  onChange: (customerId: string | null) => void;
  disabled?: boolean;
}) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  if (!canShow(CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS.customers))
    return (
      <ZHPageNotice variant="warning" message={t("finance.customerAdvances.access.customers")} />
    );
  return (
    <CustomerPicker
      value={value}
      disabled={disabled}
      onChange={(customer) => onChange(customer?.id ?? null)}
    />
  );
}
