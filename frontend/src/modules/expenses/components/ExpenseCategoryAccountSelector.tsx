import { ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import type { AccountDto } from "../../accounting/api/accountingApi";

interface Props {
  value: string;
  accounts: AccountDto[];
  disabled?: boolean;
  error?: string | null;
  onChange: (value: string) => void;
}

export function ExpenseCategoryAccountSelector({
  value,
  accounts,
  disabled,
  error,
  onChange,
}: Props) {
  return (
    <ZHField
      label="Cuenta contable destino"
      required
      fieldError={error}
      hint="Solo se asigna en subcategorias y debe ser una cuenta de gasto activa que permita movimiento."
    >
      <ZhSelect
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">Seleccione una cuenta de gasto</option>
        {accounts.map((account) => (
          <option key={account.id} value={account.id}>
            {account.code} - {account.name}
          </option>
        ))}
      </ZhSelect>
    </ZHField>
  );
}
