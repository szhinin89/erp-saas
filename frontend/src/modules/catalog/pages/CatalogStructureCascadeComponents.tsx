import './CatalogStructurePage.css';

export type CascadeItemProps = {
  id: string;
  icon: string;
  name: string;
  subtitle?: string;
  isSelected: boolean;
  isActive: boolean;
  onSelect: () => void;
  onEdit?: () => void;
  onToggle?: () => void;
  canEdit?: boolean;
  canToggle?: boolean;
  disabled?: boolean;
};

export function CascadeItem({
  icon,
  name,
  subtitle,
  isSelected,
  isActive,
  onSelect,
  onEdit,
  onToggle,
  canEdit,
  canToggle,
  disabled,
}: CascadeItemProps) {
  const itemClass = [
    'cat-cascade-item',
    isSelected ? 'cat-cascade-item--selected' : '',
    !isActive ? 'cat-cascade-item--inactive' : '',
  ]
    .filter(Boolean)
    .join(' ');

  const iconClass = [
    'material-symbols-outlined',
    'cat-cascade-item-icon',
    isSelected ? 'cat-cascade-item-icon--selected' : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onSelect}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') onSelect();
      }}
      className={itemClass}
    >
      <div className="cat-cascade-item-main">
        <span className={iconClass} aria-hidden>
          {icon}
        </span>
        <div className="cat-cascade-item-text">
          <p className="cat-cascade-item-name">{name}</p>
          {subtitle && <p className="cat-cascade-item-subtitle">{subtitle}</p>}
        </div>
      </div>
      <div className="cat-cascade-item-actions">
        {canEdit && (
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={(e) => {
              e.stopPropagation();
              onEdit?.();
            }}
            disabled={disabled}
            aria-label="Editar"
          >
            <span className="material-symbols-outlined pg-icon-16">edit</span>
          </button>
        )}
        {canToggle && (
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={(e) => {
              e.stopPropagation();
              onToggle?.();
            }}
            disabled={disabled}
            aria-label={isActive ? 'Desactivar' : 'Activar'}
          >
            <span className="material-symbols-outlined pg-icon-16">
              {isActive ? 'visibility_off' : 'visibility'}
            </span>
          </button>
        )}
      </div>
    </div>
  );
}

export type CascadeColumnProps = {
  icon: string;
  title: string;
  filterLabel?: string;
  loading: boolean;
  empty: boolean;
  children: React.ReactNode;
  onAdd?: () => void;
  canCreate?: boolean;
};

export function CascadeColumn({ icon, title, filterLabel, loading, empty, children, onAdd, canCreate }: CascadeColumnProps) {
  return (
    <div className="cat-cascade-column">
      <div className="pg-section-header cat-cascade-column-head">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">{icon}</span>
          <span className="pg-section-label">{title}</span>
        </div>
        {canCreate && onAdd && (
          <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" onClick={onAdd}>
            <span className="material-symbols-outlined cat-cascade-column-add-icon">add</span>
          </button>
        )}
      </div>

      {filterLabel && <div className="cat-cascade-filter-bar">{filterLabel}</div>}

      <div className="cat-cascade-scroll">
        {loading ? (
          <p className="subtle cat-cascade-empty">Cargando…</p>
        ) : empty ? (
          <p className="subtle cat-cascade-empty">Sin registros</p>
        ) : (
          children
        )}
      </div>
    </div>
  );
}
