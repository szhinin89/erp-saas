import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import type { AccountDto } from "../../accounting/api/accountingApi";
import type {
  ExpenseCategoryNodeLevel,
  ExpenseCategoryTreeNodeDto,
} from "../api/expenseCategoryService";

interface Props {
  nodes: ExpenseCategoryTreeNodeDto[];
  accountsById: Map<string, AccountDto>;
  selectedId: string | null;
  loading?: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canActivate: boolean;
  canDeactivate: boolean;
  onSelect: (node: ExpenseCategoryTreeNodeDto) => void;
  onCreateType: () => void;
  onCreateChild: (
    parent: ExpenseCategoryTreeNodeDto,
    level: ExpenseCategoryNodeLevel,
  ) => void;
  onEdit: (node: ExpenseCategoryTreeNodeDto) => void;
  onToggleActive: (node: ExpenseCategoryTreeNodeDto) => void;
}

const LEVEL_LABEL: Record<ExpenseCategoryNodeLevel, string> = {
  Type: "Tipo de gasto",
  Category: "Categoria de gasto",
  Subcategory: "Subcategoria de gasto",
};

export function ExpenseCategoryTree({
  nodes,
  accountsById,
  selectedId,
  loading,
  canCreate,
  canUpdate,
  canActivate,
  canDeactivate,
  onSelect,
  onCreateType,
  onCreateChild,
  onEdit,
  onToggleActive,
}: Props) {
  return (
    <section className="exp-cat-tree" aria-label="Catalogo jerarquico de gastos">
      <div className="exp-cat-tree__toolbar">
        <div>
          <h2 className="exp-cat-panel-title">Jerarquia</h2>
          <p className="exp-cat-panel-subtitle">
            Tipo de gasto, categoria y subcategoria.
          </p>
        </div>
        {canCreate && (
          <ZHBtn type="button" variant="primary" size="sm" onClick={onCreateType}>
            <span className="material-symbols-outlined" aria-hidden="true">
              add
            </span>
            Crear tipo
          </ZHBtn>
        )}
      </div>

      {loading ? (
        <div className="exp-cat-empty">Cargando catalogo...</div>
      ) : nodes.length === 0 ? (
        <div className="exp-cat-empty">
          No hay tipos de gasto registrados.
        </div>
      ) : (
        <div className="exp-cat-tree__list">
          {nodes.map((node) => (
            <TreeNodeRow
              key={node.id}
              node={node}
              depth={0}
              accountsById={accountsById}
              selectedId={selectedId}
              canCreate={canCreate}
              canUpdate={canUpdate}
              canActivate={canActivate}
              canDeactivate={canDeactivate}
              onSelect={onSelect}
              onCreateChild={onCreateChild}
              onEdit={onEdit}
              onToggleActive={onToggleActive}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function TreeNodeRow({
  node,
  depth,
  accountsById,
  selectedId,
  canCreate,
  canUpdate,
  canActivate,
  canDeactivate,
  onSelect,
  onCreateChild,
  onEdit,
  onToggleActive,
}: {
  node: ExpenseCategoryTreeNodeDto;
  depth: number;
  accountsById: Map<string, AccountDto>;
  selectedId: string | null;
  canCreate: boolean;
  canUpdate: boolean;
  canActivate: boolean;
  canDeactivate: boolean;
  onSelect: (node: ExpenseCategoryTreeNodeDto) => void;
  onCreateChild: (
    parent: ExpenseCategoryTreeNodeDto,
    level: ExpenseCategoryNodeLevel,
  ) => void;
  onEdit: (node: ExpenseCategoryTreeNodeDto) => void;
  onToggleActive: (node: ExpenseCategoryTreeNodeDto) => void;
}) {
  const account = node.accountingAccountId
    ? accountsById.get(node.accountingAccountId)
    : undefined;
  const canCreateCategory = canCreate && node.isActive && node.level === "Type";
  const canCreateSubcategory =
    canCreate && node.isActive && node.level === "Category";
  const canToggle =
    (node.isActive && canDeactivate) || (!node.isActive && canActivate);
  const rowClass = [
    "exp-cat-node",
    `exp-cat-node--depth-${depth}`,
    selectedId === node.id ? "exp-cat-node--selected" : "",
    node.isActive ? "" : "exp-cat-node--inactive",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className="exp-cat-node-wrap">
      <div className="exp-cat-node-row">
        <button type="button" className={rowClass} onClick={() => onSelect(node)}>
          <span className="exp-cat-node__main">
            <span className="exp-cat-node__titleline">
              <code className="exp-cat-code">{node.code}</code>
              <span className="exp-cat-node__name">{node.name}</span>
            </span>
            <span className="exp-cat-node__meta">
              <span>{LEVEL_LABEL[node.level]}</span>
              {account && (
                <span className="exp-cat-node__account">
                  {account.code} - {account.name}
                </span>
              )}
            </span>
          </span>
          <span className="exp-cat-node__status">
            <Badge
              label={node.isActive ? "Activo" : "Inactivo"}
              variant={node.isActive ? "green" : "gray"}
              size="md"
            />
          </span>
        </button>

        <div className="exp-cat-node-actions">
          {canCreateCategory && (
            <ZHIconButton
              icon="add"
              title="Crear categoria dentro de tipo"
              ariaLabel={`Crear categoria dentro de ${node.name}`}
              variant="ghost"
              onClick={() => onCreateChild(node, "Category")}
            />
          )}
          {canCreateSubcategory && (
            <ZHIconButton
              icon="add"
              title="Crear subcategoria dentro de categoria"
              ariaLabel={`Crear subcategoria dentro de ${node.name}`}
              variant="ghost"
              onClick={() => onCreateChild(node, "Subcategory")}
            />
          )}
          {canUpdate && (
            <ZHIconButton
              icon="edit"
              title="Editar"
              ariaLabel={`Editar ${node.name}`}
              variant="ghost"
              onClick={() => onEdit(node)}
            />
          )}
          {canToggle && (
            <ZHIconButton
              icon={node.isActive ? "toggle_on" : "toggle_off"}
              title={node.isActive ? "Desactivar" : "Activar"}
              ariaLabel={`${node.isActive ? "Desactivar" : "Activar"} ${node.name}`}
              variant="ghost"
              onClick={() => onToggleActive(node)}
            />
          )}
        </div>
      </div>

      {node.children.length > 0 && (
        <div className="exp-cat-node__children">
          {node.children.map((child) => (
            <TreeNodeRow
              key={child.id}
              node={child}
              depth={Math.min(depth + 1, 2)}
              accountsById={accountsById}
              selectedId={selectedId}
              canCreate={canCreate}
              canUpdate={canUpdate}
              canActivate={canActivate}
              canDeactivate={canDeactivate}
              onSelect={onSelect}
              onCreateChild={onCreateChild}
              onEdit={onEdit}
              onToggleActive={onToggleActive}
            />
          ))}
        </div>
      )}
    </div>
  );
}
