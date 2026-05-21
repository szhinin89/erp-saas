import { useState } from 'react';

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
  const [hovered, setHovered] = useState(false);
  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onSelect}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') onSelect();
      }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '10px 12px',
        borderRadius: 6,
        cursor: 'pointer',
        background: isSelected
          ? 'var(--color-surface-container-high)'
          : hovered
            ? 'var(--color-surface-container-low)'
            : 'transparent',
        border: isSelected ? '1px solid rgba(58,95,132,0.2)' : '1px solid transparent',
        transition: 'background 0.15s',
        opacity: isActive ? 1 : 0.6,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, flex: 1, minWidth: 0 }}>
        <span
          className="material-symbols-outlined"
          style={{
            fontSize: 20,
            flexShrink: 0,
            color: isSelected ? 'var(--color-primary)' : 'var(--color-text-secondary)',
            fontVariationSettings: isSelected ? "'FILL' 1" : "'FILL' 0",
          }}
        >
          {icon}
        </span>
        <div style={{ minWidth: 0 }}>
          <p
            style={{
              margin: 0,
              fontSize: 13,
              fontWeight: 500,
              color: isSelected ? 'var(--color-primary)' : 'var(--color-text)',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {name}
          </p>
          {subtitle && (
            <p
              style={{
                margin: 0,
                fontSize: 10,
                color: 'var(--color-text-secondary)',
                textTransform: 'uppercase',
                letterSpacing: '0.04em',
              }}
            >
              {subtitle}
            </p>
          )}
        </div>
      </div>
      <div
        style={{
          display: 'flex',
          gap: 2,
          opacity: hovered || isSelected ? 1 : 0,
          transition: 'opacity 0.15s',
          flexShrink: 0,
        }}
      >
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
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>
              edit
            </span>
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
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>
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
    <div
      style={{
        background: 'var(--color-surface)',
        border: '1px solid var(--color-border)',
        borderRadius: 'var(--radius-lg)',
        display: 'flex',
        flexDirection: 'column',
        minHeight: 540,
        overflow: 'hidden',
      }}
    >
      <div className="pg-section-header" style={{ borderRadius: 0, borderTop: 'none', borderLeft: 'none', borderRight: 'none' }}>
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon" style={{ fontSize: 18 }}>
            {icon}
          </span>
          <span className="pg-section-label">{title}</span>
        </div>
        {canCreate && onAdd && (
          <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" onClick={onAdd}>
            <span className="material-symbols-outlined" style={{ fontSize: 15 }}>
              add
            </span>
          </button>
        )}
      </div>

      {filterLabel && (
        <div
          style={{
            padding: '6px 16px',
            background: 'var(--color-primary-lt, #e8f0f8)',
            borderBottom: '1px solid var(--color-border)',
            fontSize: 11,
            color: 'var(--color-primary)',
            fontWeight: 500,
          }}
        >
          {filterLabel}
        </div>
      )}

      <div style={{ flex: 1, overflowY: 'auto', padding: '8px 6px' }}>
        {loading ? (
          <p className="subtle" style={{ padding: 16, textAlign: 'center', fontSize: 12 }}>
            Cargando…
          </p>
        ) : empty ? (
          <p className="subtle" style={{ padding: 16, textAlign: 'center', fontSize: 12 }}>
            Sin registros
          </p>
        ) : (
          children
        )}
      </div>
    </div>
  );
}
