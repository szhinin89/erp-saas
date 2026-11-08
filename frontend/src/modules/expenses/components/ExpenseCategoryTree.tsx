import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";
import type {
  ExpenseCategoryNodeLevel,
  ExpenseCategoryTreeNodeDto,
} from "../api/expenseCategoryService";

const LEVEL_LABEL: Record<ExpenseCategoryNodeLevel, string> = {
  Type: "Tipo de gasto",
  Category: "Categoría",
  Subcategory: "Subcategoría",
};

const CHILD_LEVEL: Record<ExpenseCategoryNodeLevel, ExpenseCategoryNodeLevel | null> = {
  Type: "Category",
  Category: "Subcategory",
  Subcategory: null,
};

type OpenerEvent = React.MouseEvent<HTMLButtonElement>;

interface ActionProps {
  accountsById: Map<string, AccountDto>;
  selectedId: string | null;
  collapsedIds: ReadonlySet<string>;
  canCreate: boolean;
  canUpdate: boolean;
  canActivate: boolean;
  canDeactivate: boolean;
  onSelect: (node: ExpenseCategoryTreeNodeDto) => void;
  onToggleExpanded: (node: ExpenseCategoryTreeNodeDto) => void;
  onCreateChild: (
    parent: ExpenseCategoryTreeNodeDto,
    level: ExpenseCategoryNodeLevel,
    event: OpenerEvent,
  ) => void;
  onEdit: (node: ExpenseCategoryTreeNodeDto, event: OpenerEvent) => void;
  onToggleActive: (node: ExpenseCategoryTreeNodeDto) => void;
}

interface Props extends ActionProps {
  nodes: ExpenseCategoryTreeNodeDto[];
  loading?: boolean;
  onCreateType: (event: OpenerEvent) => void;
  onExpandAll: () => void;
  onCollapseAll: () => void;
}

function childrenListId(node: ExpenseCategoryTreeNodeDto) {
  return `exp-cat-children-${node.id}`;
}

export function ExpenseCategoryTree({
  nodes,
  loading,
  onCreateType,
  onExpandAll,
  onCollapseAll,
  ...rowProps
}: Props) {
  const hasNodes = nodes.length > 0;

  return (
    <section className="exp-cat-tree" aria-label="Catálogo jerárquico de gastos">
      <div className="exp-cat-tree__toolbar">
        <div>
          <h2 className="exp-cat-panel-title">Jerarquía</h2>
          <p className="exp-cat-panel-subtitle">
            Tipo de gasto › Categoría › Subcategoría con cuenta contable destino.
          </p>
        </div>
        <div className="exp-cat-tree__toolbar-actions">
          {hasNodes && (
            <>
              <ZHBtn type="button" variant="ghost" size="sm" icon="unfold_more" onClick={onExpandAll}>
                Expandir todo
              </ZHBtn>
              <ZHBtn type="button" variant="ghost" size="sm" icon="unfold_less" onClick={onCollapseAll}>
                Contraer todo
              </ZHBtn>
            </>
          )}
          {rowProps.canCreate && (
            <ZHBtn type="button" variant="primary" size="sm" icon="add" onClick={onCreateType}>
              Crear tipo
            </ZHBtn>
          )}
        </div>
      </div>

      {hasNodes ? (
        <ul className="exp-cat-tree__list">
          {nodes.map((node) => (
            <TreeNodeRow key={node.id} node={node} {...rowProps} />
          ))}
        </ul>
      ) : (
        <div className="exp-cat-empty">
          {loading ? "Cargando catálogo..." : "No hay tipos de gasto registrados."}
        </div>
      )}
    </section>
  );
}

function TreeNodeRow({
  node,
  ...props
}: ActionProps & { node: ExpenseCategoryTreeNodeDto }) {
  const {
    accountsById,
    selectedId,
    collapsedIds,
    canCreate,
    canUpdate,
    canActivate,
    canDeactivate,
    onSelect,
    onToggleExpanded,
    onCreateChild,
    onEdit,
    onToggleActive,
  } = props;
  const account = node.accountingAccountId
    ? accountsById.get(node.accountingAccountId)
    : undefined;
  const description = node.description?.trim() ?? "";
  const isContainer = node.level !== "Subcategory";
  const childCount = node.children.length;
  const expanded = isContainer && childCount > 0 && !collapsedIds.has(node.id);
  const selected = selectedId === node.id;
  const childLevel = CHILD_LEVEL[node.level];
  const canCreateChild = canCreate && node.isActive && childLevel !== null;
  const canToggle =
    (node.isActive && canDeactivate) || (!node.isActive && canActivate);
  const rowClass = [
    "exp-cat-node",
    selected ? "exp-cat-node--selected" : "",
    node.isActive ? "" : "exp-cat-node--inactive",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <li className="exp-cat-node-wrap">
      <div
        className={`exp-cat-node-row ${isContainer ? "exp-cat-node-row--container" : ""}`}
      >
        {isContainer && (
          <ZHBtn
            type="button"
            variant="secondary"
            size="xs"
            expanded={expanded}
            aria-controls={childCount > 0 ? childrenListId(node) : undefined}
            aria-label={`${expanded ? "Contraer" : "Expandir"} ${node.name} (${childCount})`}
            title={expanded ? "Contraer" : "Expandir"}
            disabled={childCount === 0}
            onClick={() => onToggleExpanded(node)}
          >
            {childCount}
          </ZHBtn>
        )}

        <button
          type="button"
          className={rowClass}
          aria-current={selected ? "true" : undefined}
          data-node-id={node.id}
          onClick={() => onSelect(node)}
        >
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
            {description && (
              <span className="exp-cat-node__description" title={description}>
                {description}
              </span>
            )}
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
          {canCreateChild && childLevel && (
            <ZHIconButton
              icon="add"
              title={childLevel === "Category" ? "Crear categoría" : "Crear subcategoría"}
              ariaLabel={`${childLevel === "Category" ? "Crear categoría" : "Crear subcategoría"} dentro de ${node.name}`}
              variant="ghost"
              onClick={(event) => onCreateChild(node, childLevel, event)}
            />
          )}
          {canUpdate && (
            <ZHIconButton
              icon="edit"
              title="Editar"
              ariaLabel={`Editar ${node.name}`}
              variant="ghost"
              onClick={(event) => onEdit(node, event)}
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

      {childCount > 0 && (
        <ul
          id={childrenListId(node)}
          className="exp-cat-node__children"
          hidden={!expanded}
        >
          {node.children.map((child) => (
            <TreeNodeRow key={child.id} node={child} {...props} />
          ))}
        </ul>
      )}
    </li>
  );
}
