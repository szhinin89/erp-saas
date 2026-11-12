import { parseValidationErrors } from "../../lib/apiError";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";
import type {
  ExpenseCategoryNodeLevel,
  ExpenseCategoryTreeNodeDto,
} from "../api/expenseCategoryService";
import type {
  ExpenseCategoryFieldErrors,
  ExpenseCategoryFormState,
} from "../components/ExpenseCategoryFormPanel";

/** Modelo puro del catálogo de gastos: navegación del árbol y estado del formulario (sin UI). */

export function findNode(
  nodes: ExpenseCategoryTreeNodeDto[],
  id: string | null,
): ExpenseCategoryTreeNodeDto | null {
  if (!id) return null;
  for (const node of nodes) {
    if (node.id === id) return node;
    const child = findNode(node.children, id);
    if (child) return child;
  }
  return null;
}

/** Ruta desde la raíz hasta el nodo `id` (incluido); vacía si no existe. */
export function pathTo(
  nodes: ExpenseCategoryTreeNodeDto[],
  id: string | null,
): ExpenseCategoryTreeNodeDto[] {
  if (!id) return [];
  for (const node of nodes) {
    if (node.id === id) return [node];
    const rest = pathTo(node.children, id);
    if (rest.length > 0) return [node, ...rest];
  }
  return [];
}

export function formatPath(path: ExpenseCategoryTreeNodeDto[]): string {
  return path.map((node) => `${node.code} · ${node.name}`).join(" › ");
}

export function containerIdsWithChildren(nodes: ExpenseCategoryTreeNodeDto[]): string[] {
  return nodes.flatMap((node) =>
    node.children.length > 0
      ? [node.id, ...containerIdsWithChildren(node.children)]
      : [],
  );
}

export function hasActiveDescendant(node: ExpenseCategoryTreeNodeDto): boolean {
  return node.children.some((child) => child.isActive || hasActiveDescendant(child));
}

export const EMPTY_FORM: ExpenseCategoryFormState = {
  code: "",
  name: "",
  description: "",
  accountingAccountId: "",
};

export const CREATE_TITLE: Record<ExpenseCategoryNodeLevel, string> = {
  Type: "Crear tipo de gasto",
  Category: "Crear categoría",
  Subcategory: "Crear subcategoría",
};

export const EDIT_TITLE: Record<ExpenseCategoryNodeLevel, string> = {
  Type: "Editar tipo de gasto",
  Category: "Editar categoría",
  Subcategory: "Editar subcategoría",
};

export const ROOT_SUBTITLE = "Nivel raíz de las categorías de gastos";

const FIELD_MAP: Record<string, keyof ExpenseCategoryFormState> = {
  code: "code",
  Code: "code",
  name: "name",
  Name: "name",
  description: "description",
  Description: "description",
  accountingAccountId: "accountingAccountId",
  AccountingAccountId: "accountingAccountId",
};

export function accountIsValidExpense(account: AccountDto): boolean {
  return (
    account.isActive &&
    account.allowsPosting &&
    account.accountType.toLowerCase() === "expense"
  );
}

export function toForm(node: ExpenseCategoryTreeNodeDto): ExpenseCategoryFormState {
  return {
    code: node.code,
    name: node.name,
    description: node.description ?? "",
    accountingAccountId: node.accountingAccountId ?? "",
  };
}

export function formsEqual(a: ExpenseCategoryFormState, b: ExpenseCategoryFormState) {
  return (
    a.code === b.code &&
    a.name === b.name &&
    a.description === b.description &&
    a.accountingAccountId === b.accountingAccountId
  );
}

export function collectApiFieldErrors(error: unknown): ExpenseCategoryFieldErrors {
  const validation = parseValidationErrors(error);
  if (!validation) return {};

  return Object.entries(validation).reduce<ExpenseCategoryFieldErrors>(
    (acc, [field, messages]) => {
      const mapped = FIELD_MAP[field];
      const first = messages[0];
      if (mapped && first) acc[mapped] = first;
      return acc;
    },
    {},
  );
}
