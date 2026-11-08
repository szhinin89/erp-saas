import type { FormEvent } from "react";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextarea, ZhTextInput } from "../../../components/zh/inputs";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";
import type { ExpenseCategoryNodeLevel } from "../api/expenseCategoryService";
import { ExpenseCategoryAccountSelector } from "./ExpenseCategoryAccountSelector";

export type ExpenseCategoryFormMode = "idle" | "create" | "edit";

export interface ExpenseCategoryFormState {
  code: string;
  name: string;
  description: string;
  accountingAccountId: string;
}

export type ExpenseCategoryFieldErrors = Partial<
  Record<keyof ExpenseCategoryFormState, string>
>;

interface Props {
  /** `id` del `<form>`, enlazado al botón Guardar del pie de `ZHModal`. */
  formId: string;
  level: ExpenseCategoryNodeLevel;
  form: ExpenseCategoryFormState;
  fieldErrors: ExpenseCategoryFieldErrors;
  accounts: AccountDto[];
  saving: boolean;
  onChange: (patch: Partial<ExpenseCategoryFormState>) => void;
  onSubmit: () => void;
}

/**
 * Campos del nodo del catálogo de gastos. Se presenta dentro del `ZHModal` de la página;
 * la validación, el guardado y las acciones del pie pertenecen a la página.
 * DEBT: estado controlado manual (sin React Hook Form + Zod, F-V1/F-V2) — pendiente de
 * migración en una fase propia.
 */
export function ExpenseCategoryFormPanel({
  formId,
  level,
  form,
  fieldErrors,
  accounts,
  saving,
  onChange,
  onSubmit,
}: Props) {
  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSubmit();
  };

  return (
    <form id={formId} noValidate onSubmit={handleSubmit}>
      <ZHGrid cols={1}>
        <ZHGrid cols={2}>
          <ZHField label="Código" required fieldError={fieldErrors.code}>
            <ZhTextInput
              mode="uppercase"
              value={form.code}
              disabled={saving}
              onChange={(event) => onChange({ code: event.target.value })}
            />
          </ZHField>
          <ZHField label="Nombre" required fieldError={fieldErrors.name}>
            <ZhTextInput
              value={form.name}
              disabled={saving}
              onChange={(event) => onChange({ name: event.target.value })}
            />
          </ZHField>
        </ZHGrid>

        <ZHField label="Descripción" fieldError={fieldErrors.description}>
          <ZhTextarea
            rows={3}
            value={form.description}
            disabled={saving}
            onChange={(event) => onChange({ description: event.target.value })}
          />
        </ZHField>

        {level === "Subcategory" ? (
          <ExpenseCategoryAccountSelector
            value={form.accountingAccountId}
            accounts={accounts}
            disabled={saving}
            error={fieldErrors.accountingAccountId}
            onChange={(accountingAccountId) => onChange({ accountingAccountId })}
          />
        ) : (
          <ZHPageNotice
            variant="info"
            message="La cuenta contable destino se asigna solo en subcategorías."
            icon="info"
          />
        )}
      </ZHGrid>
    </form>
  );
}
