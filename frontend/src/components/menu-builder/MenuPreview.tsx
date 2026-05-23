import { useState, type CSSProperties, type ReactNode } from 'react';
import { useI18n } from '../../i18n/i18n';
import type { MenuItem } from './menuBuilderTypes';
import './menu-preview-sim.css';

export type MenuPreviewLayout = 'vertical' | 'horizontal';

type Props = {
  items: MenuItem[];
  layout: MenuPreviewLayout;
  className?: string;
  /** Optional controls rendered on the right side of the simulated browser chrome bar. */
  controls?: ReactNode;
};

function isFolder(item: MenuItem): boolean {
  return !(item.ruta?.trim()) && !(item.permiso?.trim());
}

function sidebarPad(depth: number): CSSProperties {
  return { paddingLeft: 12 + depth * 12 };
}

function sidebarChildMargin(depth: number): CSSProperties {
  return { marginLeft: 12 + depth * 12 + 6 };
}

/* ── Horizontal: top-nav items ───────────────────────────── */
function HorizontalNavItem({ item }: { item: MenuItem }) {
  const [open, setOpen] = useState(false);
  const children = item.children ?? [];
  const folder = isFolder(item);
  const hasChildren = folder && children.length > 0;

  return (
    <div className="smp-hnav-wrap">
      <button
        type="button"
        className={`smp-hnav-btn${hasChildren || !folder ? '' : ' smp-hnav-btn--muted'}`}
        onMouseEnter={(e) => {
          (e.currentTarget as HTMLElement).style.background = 'rgba(255,255,255,0.12)';
          if (hasChildren) setOpen(true);
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLElement).style.background = 'none';
          setOpen(false);
        }}
        onClick={() => hasChildren && setOpen((o) => !o)}
        aria-expanded={hasChildren ? open : undefined}
      >
        {item.nombre}
        {hasChildren && <span className="smp-hnav-chev">▾</span>}
      </button>

      {hasChildren && open && (
        <div className="smp-dropdown">
          {children.map((c) => (
            <div key={c.id} className="smp-dropdown-item">
              {c.nombre}
              {c.ruta && <span className="smp-dropdown-route">{c.ruta}</span>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/* ── Vertical: sidebar items ─────────────────────────────── */
function SidebarItem({ item, depth = 0 }: { item: MenuItem; depth?: number }) {
  const [open, setOpen] = useState(true);
  const children = item.children ?? [];
  const folder = isFolder(item);
  const hasChildren = folder && children.length > 0;
  const leaf = !folder;

  if (leaf) {
    return (
      <div className="smp-sidebar-leaf" style={sidebarPad(depth)}>
        <span className="smp-sidebar-dot" />
        <span className="smp-sidebar-label">{item.nombre}</span>
      </div>
    );
  }

  return (
    <div>
      <button
        type="button"
        className="smp-sidebar-folder-btn"
        style={sidebarPad(depth)}
        onClick={() => setOpen((o) => !o)}
      >
        <span className={`smp-sidebar-chev${open ? ' smp-sidebar-chev--open' : ''}`}>▶</span>
        <span className="smp-sidebar-label">{item.nombre}</span>
        {hasChildren && <span className="smp-sidebar-count">{children.length}</span>}
      </button>
      {open && hasChildren && (
        <div className="smp-sidebar-children" style={sidebarChildMargin(depth)}>
          {children.map((c) => (
            <SidebarItem key={c.id} item={c} depth={depth + 1} />
          ))}
        </div>
      )}
    </div>
  );
}

/* ── Empty state ─────────────────────────────────────────── */
function PreviewEmpty() {
  const { t } = useI18n();
  return (
    <div className="smp-empty">
      <span className="smp-empty-icon">◇</span>
      {t('platform.menuBuilder.previewEmpty')}
    </div>
  );
}

/* ── Shared chrome bar ───────────────────────────────────── */
function ChromeBar({ controls }: { controls?: ReactNode }) {
  return (
    <div className="smp-chrome">
      <div className="smp-chrome-dots">
        <div className="smp-chrome-dot smp-chrome-dot--red" />
        <div className="smp-chrome-dot smp-chrome-dot--yellow" />
        <div className="smp-chrome-dot smp-chrome-dot--green" />
      </div>
      <div className="smp-chrome-url">app.empresa.com/dashboard</div>
      {controls ? <div className="smp-chrome-controls">{controls}</div> : null}
    </div>
  );
}

function SkeletonContent({ cardHeights }: { cardHeights: number[] }) {
  return (
    <div className="smp-content">
      <div className="smp-skeleton-line smp-skeleton-line--lg smp-skeleton-line--w40" />
      <div className="smp-skeleton-line smp-skeleton-line--sm smp-skeleton-line--w60" />
      <div className="smp-skeleton-cards">
        {cardHeights.map((h, i) => (
          <div
            key={`${h}-${i}`}
            className={`smp-skeleton-card smp-skeleton-card--h${h}`}
          />
        ))}
      </div>
    </div>
  );
}

/* ── Main export ─────────────────────────────────────────── */
export function MenuPreview({ items, layout, className = '', controls }: Props) {
  const empty = items.length === 0;
  const rootClass = ['menu-preview-sim', 'smp-root', className, layout === 'horizontal' ? 'smp-root--horizontal' : 'smp-root--vertical']
    .filter(Boolean)
    .join(' ');

  if (layout === 'horizontal') {
    return (
      <div className={rootClass}>
        <ChromeBar controls={controls} />
        <div className="smp-topnav smp-topnav--horizontal">
          <div className="smp-brand smp-brand--bordered">ZH ERP</div>
          {empty ? (
            <span className="smp-topnav-empty">Sin ítems de menú</span>
          ) : (
            items.map((item) => <HorizontalNavItem key={item.id} item={item} />)
          )}
          <div className="smp-topnav-icons">
            <div className="smp-avatar-placeholder smp-avatar-placeholder--md" />
            <div className="smp-avatar-placeholder smp-avatar-placeholder--md" />
          </div>
        </div>
        <SkeletonContent cardHeights={[48, 48, 48]} />
      </div>
    );
  }

  return (
    <div className={rootClass}>
      <ChromeBar controls={controls} />
      <div className="smp-topnav smp-topnav--vertical">
        <div className="smp-brand">ZH ERP</div>
        <div className="smp-topnav-icons smp-topnav-icons--soft">
          <div className="smp-avatar-placeholder smp-avatar-placeholder--sm" />
        </div>
      </div>
      <div className="smp-body-row">
        <nav className="smp-sidebar">
          {empty ? <PreviewEmpty /> : items.map((item) => <SidebarItem key={item.id} item={item} />)}
        </nav>
        <div className="smp-content">
          <div className="smp-skeleton-line smp-skeleton-line--lg smp-skeleton-line--w50" />
          <div className="smp-skeleton-line smp-skeleton-line--sm smp-skeleton-line--w35" />
          <div className="smp-skeleton-cards">
            <div className="smp-skeleton-card smp-skeleton-card--h56" />
            <div className="smp-skeleton-card smp-skeleton-card--h56" />
          </div>
          <div className="smp-skeleton-card smp-skeleton-card--h40" />
        </div>
      </div>
    </div>
  );
}
