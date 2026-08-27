import { useMemo } from "react";
import { ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import type { AccountDto } from "../../accounting/api/accountingApi";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";

interface Props {
  tree: ExpenseCategoryTreeNodeDto[];
  accountsById: Map<string, AccountDto>;
  value: string;
  onChange: (subcategoryId: string) => void;
  disabled?: boolean;
  error?: string | null;
}

function findPath(
  nodes: ExpenseCategoryTreeNodeDto[],
  subcategoryId: string,
): {
  type: ExpenseCategoryTreeNodeDto | null;
  category: ExpenseCategoryTreeNodeDto | null;
  subcategory: ExpenseCategoryTreeNodeDto | null;
} {
  for (const type of nodes) {
    for (const category of type.children) {
      const subcategory = category.children.find((node) => node.id === subcategoryId);
      if (subcategory) return { type, category, subcategory };
    }
  }
  return { type: null, category: null, subcategory: null };
}

export function ExpenseSubcategorySelector({
  tree,
  accountsById,
  value,
  onChange,
  disabled,
  error,
}: Props) {
  const path = useMemo(() => findPath(tree, value), [tree, value]);
  const typeId = path.type?.id ?? "";
  const categoryId = path.category?.id ?? "";
  const selectedType = tree.find((node) => node.id === typeId) ?? null;
  const categories = selectedType?.children.filter((node) => node.isActive) ?? [];
  const selectedCategory =
    categories.find((node) => node.id === categoryId) ?? path.category;
  const subcategories =
    selectedCategory?.children.filter(
      (node) =>
        node.isActive &&
        node.accountingAccountId &&
        accountsById.has(node.accountingAccountId),
    ) ?? [];
  const selectedSubcategory =
    subcategories.find((node) => node.id === value) ?? path.subcategory;
  const account = selectedSubcategory?.accountingAccountId
    ? accountsById.get(selectedSubcategory.accountingAccountId)
    : null;

  return (
    <div className="exp-doc-subcategory">
      <ZHField label="Tipo de gasto" required density="compact">
        <ZhSelect
          density="compact"
          value={typeId}
          disabled={disabled}
          onChange={(event) => {
            const nextType = tree.find((node) => node.id === event.target.value);
            const firstCategory = nextType?.children.find((node) => node.isActive);
            const firstSubcategory = firstCategory?.children.find(
              (node) =>
                node.isActive &&
                node.accountingAccountId &&
                accountsById.has(node.accountingAccountId),
            );
            onChange(firstSubcategory?.id ?? "");
          }}
        >
          <option value="">Seleccione...</option>
          {tree
            .filter((node) => node.isActive)
            .map((node) => (
              <option key={node.id} value={node.id}>
                {node.code} - {node.name}
              </option>
            ))}
        </ZhSelect>
      </ZHField>

      <ZHField label="Categoria" required density="compact">
        <ZhSelect
          density="compact"
          value={categoryId}
          disabled={disabled || !selectedType}
          onChange={(event) => {
            const nextCategory = categories.find(
              (node) => node.id === event.target.value,
            );
            const firstSubcategory = nextCategory?.children.find(
              (node) =>
                node.isActive &&
                node.accountingAccountId &&
                accountsById.has(node.accountingAccountId),
            );
            onChange(firstSubcategory?.id ?? "");
          }}
        >
          <option value="">Seleccione...</option>
          {categories.map((node) => (
            <option key={node.id} value={node.id}>
              {node.code} - {node.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>

      <ZHField
        label="Subcategoria"
        required
        density="compact"
        fieldError={error}
        hint={
          account
            ? `${account.code} - ${account.name}`
            : "La cuenta contable se toma de la subcategoria."
        }
        hintType={account ? "success" : "muted"}
      >
        <ZhSelect
          density="compact"
          value={value}
          disabled={disabled || !selectedCategory}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="">Seleccione...</option>
          {subcategories.map((node) => (
            <option key={node.id} value={node.id}>
              {node.code} - {node.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
    </div>
  );
}
