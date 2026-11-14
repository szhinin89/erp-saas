import { useMemo, useState } from "react";
import { ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { useI18n } from "../../../i18n/i18n";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";
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
  const { t } = useI18n();
  const path = useMemo(() => findPath(tree, value), [tree, value]);
  // EXPENSE-UX-01 (U3) — Tipo y Categoría ya no se derivan solo de la subcategoría: al cambiarlos sin
  // una subcategoría válida bajo la nueva selección, la subcategoría se limpia y el usuario debe
  // elegirla explícitamente (antes se seleccionaba en silencio la primera disponible). La selección
  // intermedia vive aquí; en cuanto hay subcategoría, el camino real del árbol manda.
  const [picked, setPicked] = useState({ typeId: "", categoryId: "" });
  const typeId = path.type?.id ?? picked.typeId;
  const categoryId = path.category?.id ?? picked.categoryId;
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
      <ZHField label={t("expenses.form.subcategory.type")} required density="compact">
        <ZhSelect
          density="compact"
          value={typeId}
          disabled={disabled}
          onChange={(event) => {
            const nextTypeId = event.target.value;
            // Misma selección de Tipo: la subcategoría sigue siendo válida, se conserva.
            if (nextTypeId === typeId) return;
            setPicked({ typeId: nextTypeId, categoryId: "" });
            if (value) onChange("");
          }}
        >
          <option value="">{t("expenses.form.subcategory.select")}</option>
          {tree
            .filter((node) => node.isActive)
            .map((node) => (
              <option key={node.id} value={node.id}>
                {node.code} - {node.name}
              </option>
            ))}
        </ZhSelect>
      </ZHField>

      <ZHField label={t("expenses.form.subcategory.category")} required density="compact">
        <ZhSelect
          density="compact"
          value={categoryId}
          disabled={disabled || !selectedType}
          onChange={(event) => {
            const nextCategoryId = event.target.value;
            // Misma Categoría: la subcategoría sigue siendo válida, se conserva.
            if (nextCategoryId === categoryId) return;
            setPicked({ typeId, categoryId: nextCategoryId });
            if (value) onChange("");
          }}
        >
          <option value="">{t("expenses.form.subcategory.select")}</option>
          {categories.map((node) => (
            <option key={node.id} value={node.id}>
              {node.code} - {node.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>

      <ZHField
        label={t("expenses.form.subcategory.subcategory")}
        required
        density="compact"
        fieldError={error}
        hint={
          account
            ? `${account.code} - ${account.name}`
            : t("expenses.form.subcategory.accountHint")
        }
        hintType={account ? "success" : "muted"}
      >
        <ZhSelect
          density="compact"
          value={value}
          disabled={disabled || !selectedCategory}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="">{t("expenses.form.subcategory.select")}</option>
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
