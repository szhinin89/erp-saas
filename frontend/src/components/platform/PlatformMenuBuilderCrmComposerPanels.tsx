import type { ReactNode } from 'react';
import { ZHBtn } from '../zh/ZHForm';
import { MENU_BUILDER_SCHEMA_VERSION, planEmoji, type CatalogNodeType, type CatalogSearchField } from './platformMenuBuilderUtils';
import type { UsePlatformMenuBuilderReturn } from './usePlatformMenuBuilder';

export type CrmComposerPanelsProps = Pick<
  UsePlatformMenuBuilderReturn,
  | 'planId'
  | 'setPlanId'
  | 'setErr'
  | 'catalogSearch'
  | 'setCatalogSearch'
  | 'catalogSearchField'
  | 'setCatalogSearchField'
  | 'catalogNodeType'
  | 'setCatalogNodeType'
  | 'catalogOnlyWithRoute'
  | 'setCatalogOnlyWithRoute'
  | 'busy'
  | 'savingAuto'
  | 'crmPlans'
  | 'setPreviewLayout'
  | 'undoEditorTree'
  | 'redoEditorTree'
  | 'canUndoEditorTree'
  | 'canRedoEditorTree'
  | 'syncCatalog'
  | 'reloadArbol'
  | 'savePlan'
  | 'setInheritPlanConfirmOpen'
  | 'setResetPlanConfirmOpen'
>;

export function buildCrmComposerPanels(props: CrmComposerPanelsProps): {
  crmToolbar: ReactNode;
  crmMasterStack: ReactNode;
  crmLibraryStack: ReactNode;
  crmMasterFooter: ReactNode;
} {
  const {
    planId,
    setPlanId,
    setErr,
    catalogSearch,
    setCatalogSearch,
    catalogSearchField,
    setCatalogSearchField,
    catalogNodeType,
    setCatalogNodeType,
    catalogOnlyWithRoute,
    setCatalogOnlyWithRoute,
    busy,
    savingAuto,
    crmPlans,
    setPreviewLayout,
    undoEditorTree,
    redoEditorTree,
    canUndoEditorTree,
    canRedoEditorTree,
    syncCatalog,
    reloadArbol,
    savePlan,
    setInheritPlanConfirmOpen,
    setResetPlanConfirmOpen,
  } = props;

  const crmToolbar: ReactNode = (
    <>
      <button
        type="button"
        className="zh-btn zh-btn--ghost zh-btn--sm"
        onClick={() => undoEditorTree()}
        disabled={!canUndoEditorTree}
        aria-label="Deshacer último cambio del árbol"
        title="Deshacer"
      >
        ↺
      </button>
      <button
        type="button"
        className="zh-btn zh-btn--ghost zh-btn--sm"
        onClick={() => redoEditorTree()}
        disabled={!canRedoEditorTree}
        aria-label="Rehacer cambio deshecho del árbol"
        title="Rehacer"
      >
        ↻
      </button>
      <span className="subtle mono smb-toolbar-schema" title="Versión del esquema de menú">
        v{MENU_BUILDER_SCHEMA_VERSION}
      </span>
    </>
  );

  const crmMasterStack: ReactNode = (
    <div className="zh-form-tabs smb-plan-tabs" role="tablist" aria-label="Planes comerciales">
      {crmPlans.map((p) => (
        <button
          key={p.id}
          type="button"
          role="tab"
          aria-selected={planId === p.id}
          className={[planId === p.id ? 'is-active' : '', !p.isPubliclyVisible ? 'smb-plan-tab--internal' : ''].filter(Boolean).join(' ')}
          onClick={() => {
            setErr('');
            setPlanId(p.id);
            setPreviewLayout(p.layout);
          }}
          disabled={busy || savingAuto}
          title={!p.isPubliclyVisible ? `${p.code} — plan interno (no visible al público)` : p.code}
        >
          <span className="smb-plan-tab-emoji" aria-hidden>
            {planEmoji(p.code)}
          </span>
          {p.code}
        </button>
      ))}
    </div>
  );

  const crmLibraryStack: ReactNode = (
    <div className="menu-plan-composer__masterStack">
      <div className="pg-table-controls smb-catalog-controls">
        <div className="pg-search smb-catalog-search">
          <span className="material-symbols-outlined">search</span>
          <input
            className="zh-input"
            type="search"
            autoComplete="off"
            placeholder="Buscar nodo…"
            value={catalogSearch}
            onChange={(e) => setCatalogSearch(e.target.value)}
            disabled={busy || savingAuto}
            aria-label="Buscar en el catálogo de formularios"
          />
        </div>
        <div className="smb-catalog-actions">
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            onClick={() => {
              setCatalogSearch('');
              setCatalogSearchField('all');
              setCatalogNodeType('all');
              setCatalogOnlyWithRoute(false);
            }}
            disabled={
              busy ||
              savingAuto ||
              (!catalogSearch.trim() && catalogSearchField === 'all' && catalogNodeType === 'all' && !catalogOnlyWithRoute)
            }
            aria-label="Limpiar búsqueda del catálogo"
          >
            Limpiar
          </ZHBtn>
          <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void syncCatalog()} disabled={busy || savingAuto} aria-label="Sincronizar catálogo desde controladores">
            Sync API
          </ZHBtn>
          <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void reloadArbol()} disabled={busy || savingAuto} aria-label="Refrescar lista de formularios">
            <span className="material-symbols-outlined pg-icon-16">refresh</span>
          </ZHBtn>
        </div>
      </div>
      <div className="menu-plan-composer__advancedFilters" aria-label="Filtros avanzados del catálogo">
        <label className="menu-plan-composer__advancedFilterItem">
          <span>Campo</span>
          <select
            className="zh-input"
            value={catalogSearchField}
            onChange={(e) => setCatalogSearchField(e.target.value as CatalogSearchField)}
            disabled={busy || savingAuto}
            title="Limita la búsqueda por nombre, permiso o ruta."
          >
            <option value="all">Todo</option>
            <option value="name">Nombre</option>
            <option value="perm">Permiso</option>
            <option value="route">Ruta</option>
          </select>
        </label>
        <label className="menu-plan-composer__advancedFilterItem">
          <span>Tipo</span>
          <select
            className="zh-input"
            value={catalogNodeType}
            onChange={(e) => setCatalogNodeType(e.target.value as CatalogNodeType)}
            disabled={busy || savingAuto}
            title="Filtra por carpetas o formularios."
          >
            <option value="all">Todos</option>
            <option value="folders">Solo carpetas</option>
            <option value="forms">Solo formularios</option>
          </select>
        </label>
        <label className="zh-inline-check" title="Muestra solo nodos que tengan ruta configurada.">
          <input
            type="checkbox"
            checked={catalogOnlyWithRoute}
            onChange={(e) => setCatalogOnlyWithRoute(e.target.checked)}
            disabled={busy || savingAuto}
          />
          <span>Solo con ruta</span>
        </label>
        <span className="subtle smb-filter-tip" title="Tip: combina búsqueda + tipo para encontrar nodos más rápido.">
          💡 Filtro avanzado
        </span>
      </div>
    </div>
  );

  const crmMasterFooter: ReactNode = (
    <div className="pg-actions-bar">
      <div className="pg-actions-buttons">
        <ZHBtn
          variant="primary"
          size="md"
          type="button"
          onClick={() => void savePlan()}
          disabled={busy || savingAuto || !planId}
          aria-label="Guardar armado del menú en el plan actual"
        >
          💾 Guardar menú
        </ZHBtn>
        <ZHBtn
          variant="ghost"
          size="md"
          type="button"
          onClick={() => setInheritPlanConfirmOpen(true)}
          disabled={busy || savingAuto}
          aria-label="Heredar y guardar menú desde plan anterior"
        >
          📄 Heredar ahora
        </ZHBtn>
        <ZHBtn variant="ghost" size="md" type="button" onClick={() => setResetPlanConfirmOpen(true)} disabled={busy || savingAuto || !planId} aria-label="Resetear plan actual">
          🔄 Resetear plan
        </ZHBtn>
      </div>
      <div className="menu-plan-composer__callout menu-plan-composer__callout--info" role="note">
        Arrastra nodos para reordenar o cambiar de carpeta. Usa las flechas del bloque de acciones para subir, bajar o cambiar de nivel. Deshacer y rehacer solo afectan esta sesión; los
        cambios válidos se guardan solos en el plan.
      </div>
    </div>
  );

  return { crmToolbar, crmMasterStack, crmLibraryStack, crmMasterFooter };
}
