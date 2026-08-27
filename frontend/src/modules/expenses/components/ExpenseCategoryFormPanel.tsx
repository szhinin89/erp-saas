import { Badge } from "../../../components/PageShell";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextarea, ZhTextInput } from "../../../components/zh/inputs";
import type { AccountDto } from "../../accounting/api/accountingApi";
import type {
  ExpenseCategoryNodeLevel,
  ExpenseCategoryTreeNodeDto,
} from "../api/expenseCategoryService";
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
  mode: ExpenseCategoryFormMode;
  level: ExpenseCategoryNodeLevel | null;
  parent: ExpenseCategoryTreeNodeDto | null;
  selected: ExpenseCategoryTreeNodeDto | null;
  form: ExpenseCategoryFormState;
  fieldErrors: ExpenseCategoryFieldErrors;
  accounts: AccountDto[];
  accountsById: Map<string, AccountDto>;
  saving: boolean;
  canSave: boolean;
  onChange: (patch: Partial<ExpenseCategoryFormState>) => void;
  onSubmit: () => void;
  onCancel: () => void;
}

const LEVEL_LABEL: Record<ExpenseCategoryNodeLevel, string> = {
  Type: "Tipo de gasto",
  Category: "Categoria de gasto",
  Subcategory: "Subcategoria de gasto",
};

function selectedAccount(
  node: ExpenseCategoryTreeNodeDto | null,
  accountsById: Map<string, AccountDto>,
) {
  return node?.accountingAccountId
    ? accountsById.get(node.accountingAccountId)
    : null;
}

export function ExpenseCategoryFormPanel({
  mode,
  level,
  parent,
  selected,
  form,
  fieldErrors,
  accounts,
  accountsById,
  saving,
  canSave,
  onChange,
  onSubmit,
  onCancel,
}: Props) {
  const editing = mode === "edit";
  const creating = mode === "create";
  const effectiveLevel = level ?? selected?.level ?? null;
  const account = selectedAccount(selected, accountsById);
  const title = creating
    ? `Crear ${effectiveLevel ? LEVEL_LABEL[effectiveLevel].toLowerCase() : "nodo"}`
    : editing
      ? `Editar ${effectiveLevel ? LEVEL_LABEL[effectiveLevel].toLowerCase() : "nodo"}`
      : "Detalle";

  if (mode === "idle") {
    return (
      <section className="exp-cat-form-panel" aria-label="Detalle del catalogo">
        <div className="exp-cat-form-panel__header">
          <div>
            <h2 className="exp-cat-panel-title">{title}</h2>
            <p className="exp-cat-panel-subtitle">
              La cuenta contable se asigna solo en subcategorias.
            </p>
          </div>
        </div>

        <ZHPageNotice
          variant="info"
          message="Tipos y categorias organizan el catalogo; solo la subcategoria define la cuenta destino."
          icon="account_tree"
        />

        {selected ? (
          <div className="exp-cat-detail">
            <div className="exp-cat-detail__header">
              <div>
                <code className="exp-cat-code">{selected.code}</code>
                <h3>{selected.name}</h3>
              </div>
              <Badge
                label={selected.isActive ? "Activo" : "Inactivo"}
                variant={selected.isActive ? "green" : "gray"}
                size="md"
              />
            </div>
            <dl className="exp-cat-detail-list">
              <div>
                <dt>Nivel</dt>
                <dd>{LEVEL_LABEL[selected.level]}</dd>
              </div>
              <div>
                <dt>Descripcion</dt>
                <dd>{selected.description || "Sin descripcion"}</dd>
              </div>
              {selected.level === "Subcategory" && (
                <div>
                  <dt>Cuenta contable destino</dt>
                  <dd>
                    {account
                      ? `${account.code} - ${account.name}`
                      : "Cuenta no disponible"}
                  </dd>
                </div>
              )}
            </dl>
          </div>
        ) : (
          <div className="exp-cat-empty">
            Seleccione un nodo del arbol o cree un tipo de gasto.
          </div>
        )}
      </section>
    );
  }

  return (
    <section className="exp-cat-form-panel" aria-label="Formulario del catalogo">
      <div className="exp-cat-form-panel__header">
        <div>
          <h2 className="exp-cat-panel-title">{title}</h2>
          <p className="exp-cat-panel-subtitle">
            {parent
              ? `Dentro de ${parent.code} - ${parent.name}`
              : "Nodo raiz del catalogo de gastos"}
          </p>
        </div>
        {effectiveLevel && (
          <Badge label={LEVEL_LABEL[effectiveLevel]} variant="blue" size="md" />
        )}
      </div>

      <ZHPageNotice
        variant="info"
        message="La cuenta contable no se envia en tipos ni categorias; es obligatoria solo para subcategorias."
        icon="info"
      />

      <ZHGrid cols={2}>
        <ZHField label="Codigo" required fieldError={fieldErrors.code}>
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

      <ZHField label="Descripcion" fieldError={fieldErrors.description}>
        <ZhTextarea
          rows={3}
          value={form.description}
          disabled={saving}
          onChange={(event) => onChange({ description: event.target.value })}
        />
      </ZHField>

      {effectiveLevel === "Subcategory" && (
        <ExpenseCategoryAccountSelector
          value={form.accountingAccountId}
          accounts={accounts}
          disabled={saving}
          error={fieldErrors.accountingAccountId}
          onChange={(accountingAccountId) => onChange({ accountingAccountId })}
        />
      )}

      <div className="exp-cat-form-actions">
        <ZHBtn type="button" variant="ghost" onClick={onCancel} disabled={saving}>
          Cancelar
        </ZHBtn>
        <ZHBtn
          type="button"
          variant="primary"
          onClick={onSubmit}
          disabled={saving || !canSave}
        >
          {saving ? "Guardando..." : "Guardar"}
        </ZHBtn>
      </div>
    </section>
  );
}
