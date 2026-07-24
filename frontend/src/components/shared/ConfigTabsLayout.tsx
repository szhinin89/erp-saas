import { type ReactNode } from 'react';

interface ConfigTabsLayoutProps {
  activeTab: 'list' | 'editor';
  onTabChange: (tab: 'list' | 'editor') => void;
  /** Label for the editor tab — changes dynamically: "Nueva Sucursal" / "Editar" */
  editorLabel: string;
  editorIcon?: string;
  listContent: ReactNode;
  editorContent: ReactNode;
  /** Rendered above the tab bar (e.g. a ZHPageNotice for errors) */
  error?: ReactNode;
}

/**
 * Two-tab layout shared by configuration modules:
 *   Tab 1 — Lista   (full-width list + KPIs + toolbar)
 *   Tab 2 — Editor  (full-width form, ready for sub-tab expansion)
 *
 * Tab switching is driven by the parent: openCreate/openEdit wrappers
 * set 'editor'; the Cancel handler sets 'list'.
 */
export function ConfigTabsLayout({
  activeTab,
  onTabChange,
  editorLabel,
  editorIcon = 'edit',
  listContent,
  editorContent,
  error,
}: ConfigTabsLayoutProps) {
  return (
    <>
      {error}

      <div className="prd-tabs" role="tablist" aria-label="Secciones de configuración">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'list'}
          className={`prd-tab-btn${activeTab === 'list' ? ' prd-tab-btn--active' : ''}`}
          onClick={() => onTabChange('list')}
        >
          <span className="material-symbols-outlined prd-tab-icon">view_list</span>
          Lista
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'editor'}
          className={`prd-tab-btn${activeTab === 'editor' ? ' prd-tab-btn--active' : ''}`}
          onClick={() => onTabChange('editor')}
        >
          <span className="material-symbols-outlined prd-tab-icon">{editorIcon}</span>
          {editorLabel}
        </button>
      </div>

      <div className="cfg-tabs-content">
        {activeTab === 'list'
          ? listContent
          : <div className="cfg-tabs-form-wrap">{editorContent}</div>}
      </div>
    </>
  );
}
