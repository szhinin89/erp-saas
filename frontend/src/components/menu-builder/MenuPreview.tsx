import { useState, type ReactNode } from 'react';
import { useI18n } from '../../i18n/i18n';
import type { MenuItem } from './menuBuilderTypes';

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

/* ── Horizontal: top-nav items ───────────────────────────── */
function HorizontalNavItem({ item }: { item: MenuItem }) {
  const [open, setOpen] = useState(false);
  const children = item.children ?? [];
  const folder = isFolder(item);
  const hasChildren = folder && children.length > 0;

  return (
    <div style={{ position: 'relative' }}>
      <button
        type="button"
        style={{
          display: 'flex', alignItems: 'center', gap: 4,
          padding: '6px 12px', border: 'none', background: 'none',
          color: '#fff', fontSize: 13, fontWeight: 500,
          cursor: 'pointer', borderRadius: 4,
          opacity: hasChildren || !folder ? 1 : 0.5,
          transition: 'background 0.15s',
          whiteSpace: 'nowrap',
        }}
        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = 'rgba(255,255,255,0.12)'; if (hasChildren) setOpen(true); }}
        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = 'none'; setOpen(false); }}
        onClick={() => hasChildren && setOpen((o) => !o)}
        aria-expanded={hasChildren ? open : undefined}
      >
        {item.nombre}
        {hasChildren && <span style={{ fontSize: 10, marginLeft: 2, opacity: 0.7 }}>▾</span>}
      </button>

      {hasChildren && open && (
        <div style={{
          position: 'absolute', top: '100%', left: 0,
          minWidth: 180, background: '#fff',
          border: '1px solid rgba(0,0,0,0.1)', borderRadius: 6,
          boxShadow: '0 4px 16px rgba(0,0,0,0.12)',
          zIndex: 100, padding: '4px 0',
        }}>
          {children.map((c) => (
            <div key={c.id} style={{
              padding: '7px 14px', fontSize: 12.5,
              color: '#1e293b', cursor: 'default',
              display: 'flex', alignItems: 'center', gap: 8,
              borderBottom: '1px solid rgba(0,0,0,0.05)',
            }}>
              {c.nombre}
              {c.ruta && (
                <span style={{ fontSize: 10, color: '#64748b', fontFamily: 'monospace', marginLeft: 'auto' }}>
                  {c.ruta}
                </span>
              )}
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

  const indent = depth * 12;

  if (leaf) {
    return (
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6,
        padding: `5px 12px 5px ${12 + indent}px`,
        fontSize: 12, color: '#42474e', cursor: 'default',
        borderRadius: 4, transition: 'background 0.1s',
      }}>
        <span style={{ width: 4, height: 4, borderRadius: '50%', background: '#94a3b8', flexShrink: 0 }} />
        <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {item.nombre}
        </span>
      </div>
    );
  }

  return (
    <div>
      <button
        type="button"
        style={{
          display: 'flex', alignItems: 'center', gap: 6,
          padding: `6px 12px 6px ${12 + indent}px`,
          width: '100%', border: 'none', background: 'none',
          fontSize: 12, fontWeight: 600, color: '#1e293b',
          cursor: 'pointer', textAlign: 'left', borderRadius: 4,
          transition: 'background 0.1s',
        }}
        onClick={() => setOpen((o) => !o)}
      >
        <span style={{ fontSize: 9, color: '#94a3b8', transition: 'transform 0.15s',
          transform: open ? 'rotate(90deg)' : 'none', display: 'inline-block' }}>▶</span>
        <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {item.nombre}
        </span>
        {hasChildren && (
          <span style={{ fontSize: 10, color: '#94a3b8', flexShrink: 0 }}>{children.length}</span>
        )}
      </button>
      {open && hasChildren && (
        <div style={{ borderLeft: '1px solid rgba(0,0,0,0.06)', marginLeft: 12 + indent + 6 }}>
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
    <div style={{
      display: 'flex', flexDirection: 'column', alignItems: 'center',
      justifyContent: 'center', padding: 32, color: '#94a3b8',
      fontSize: 13, textAlign: 'center', gap: 8,
    }}>
      <span style={{ fontSize: 28, opacity: 0.4 }}>◇</span>
      {t('superadmin.menuBuilder.previewEmpty')}
    </div>
  );
}

/* ── Shared chrome bar ───────────────────────────────────── */
function ChromeBar({ controls }: { controls?: ReactNode }) {
  return (
    <div style={{
      background: '#e2e8f0', padding: '5px 10px',
      display: 'flex', alignItems: 'center', gap: 6,
      borderBottom: '1px solid rgba(0,0,0,0.08)',
    }}>
      <div style={{ display: 'flex', gap: 4, flexShrink: 0 }}>
        <div style={{ width: 8, height: 8, borderRadius: '50%', background: '#f87171' }} />
        <div style={{ width: 8, height: 8, borderRadius: '50%', background: '#fbbf24' }} />
        <div style={{ width: 8, height: 8, borderRadius: '50%', background: '#34d399' }} />
      </div>
      <div style={{
        flex: 1, marginLeft: 4, background: '#fff',
        borderRadius: 4, padding: '2px 8px', fontSize: 10, color: '#94a3b8',
      }}>app.empresa.com/dashboard</div>
      {controls ? <div style={{ flexShrink: 0, marginLeft: 6 }}>{controls}</div> : null}
    </div>
  );
}

/* ── Main export ─────────────────────────────────────────── */
export function MenuPreview({ items, layout, className = '', controls }: Props) {
  const empty = items.length === 0;

  /* ── Horizontal: simulated top-bar app ─────────────────── */
  if (layout === 'horizontal') {
    return (
      <div className={`menu-preview-sim ${className}`.trim()} style={{
        display: 'flex', flexDirection: 'column',
        background: '#f1f5f9', minHeight: 220,
        border: '1px solid rgba(0,0,0,0.08)', borderRadius: 8, overflow: 'hidden',
      }}>
        <ChromeBar controls={controls} />

        {/* Simulated top nav bar */}
        <div style={{
          background: '#3a5f84', display: 'flex', alignItems: 'center',
          padding: '0 12px', gap: 4, minHeight: 40, flexShrink: 0,
        }}>
          {/* Brand */}
          <div style={{
            color: '#fff', fontWeight: 700, fontSize: 12,
            marginRight: 8, whiteSpace: 'nowrap', paddingRight: 8,
            borderRight: '1px solid rgba(255,255,255,0.2)',
          }}>ZH ERP</div>

          {empty ? (
            <span style={{ color: 'rgba(255,255,255,0.4)', fontSize: 11, fontStyle: 'italic' }}>
              Sin ítems de menú
            </span>
          ) : (
            items.map((item) => <HorizontalNavItem key={item.id} item={item} />)
          )}

          {/* Right icons */}
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 4, opacity: 0.6 }}>
            <div style={{ width: 22, height: 22, borderRadius: '50%', background: 'rgba(255,255,255,0.15)' }} />
            <div style={{ width: 22, height: 22, borderRadius: '50%', background: 'rgba(255,255,255,0.15)' }} />
          </div>
        </div>

        {/* Simulated page content */}
        <div style={{ flex: 1, padding: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ height: 10, width: '40%', background: '#cbd5e1', borderRadius: 4 }} />
          <div style={{ height: 8, width: '60%', background: '#e2e8f0', borderRadius: 4 }} />
          <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
            {[1, 2, 3].map((i) => (
              <div key={i} style={{ flex: 1, height: 48, background: '#fff', borderRadius: 6,
                border: '1px solid rgba(0,0,0,0.06)' }} />
            ))}
          </div>
        </div>
      </div>
    );
  }

  /* ── Vertical: simulated sidebar app ───────────────────── */
  return (
    <div className={`menu-preview-sim ${className}`.trim()} style={{
      display: 'flex', flexDirection: 'column',
      background: '#f1f5f9', minHeight: 280,
      border: '1px solid rgba(0,0,0,0.08)', borderRadius: 8, overflow: 'hidden',
    }}>
      <ChromeBar controls={controls} />

      {/* Top bar (narrow) */}
      <div style={{
        background: '#3a5f84', display: 'flex', alignItems: 'center',
        padding: '0 12px', height: 36, flexShrink: 0,
      }}>
        <div style={{ color: '#fff', fontWeight: 700, fontSize: 12 }}>ZH ERP</div>
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 4, opacity: 0.5 }}>
          <div style={{ width: 20, height: 20, borderRadius: '50%', background: 'rgba(255,255,255,0.2)' }} />
        </div>
      </div>

      {/* Body: sidebar + content */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
        {/* Sidebar */}
        <nav style={{
          width: 168, background: '#fff', borderRight: '1px solid rgba(0,0,0,0.07)',
          flexShrink: 0, padding: '8px 0', overflowY: 'auto',
        }}>
          {empty ? (
            <PreviewEmpty />
          ) : (
            items.map((item) => <SidebarItem key={item.id} item={item} />)
          )}
        </nav>

        {/* Simulated content area */}
        <div style={{ flex: 1, padding: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ height: 10, width: '50%', background: '#cbd5e1', borderRadius: 4 }} />
          <div style={{ height: 8, width: '35%', background: '#e2e8f0', borderRadius: 4 }} />
          <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
            {[1, 2].map((i) => (
              <div key={i} style={{ flex: 1, height: 56, background: '#fff', borderRadius: 6,
                border: '1px solid rgba(0,0,0,0.06)' }} />
            ))}
          </div>
          <div style={{ height: 40, background: '#fff', borderRadius: 6,
            border: '1px solid rgba(0,0,0,0.06)' }} />
        </div>
      </div>
    </div>
  );
}
