import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SuperAdminPageTemplate } from '../../components/superadmin/SuperAdminPageTemplate';
import { SuperAdminMenuBuilderSection } from '../../components/superadmin/SuperAdminMenuBuilderSection';
import { useI18n } from '../../i18n/i18n';
import './menu-plans-hub.css';

type HubTab = 'menuBuilder' | 'planes' | 'auditoriaGlobal';

function parseHubTab(raw: string | null): HubTab {
  const v = (raw ?? '').trim().toLowerCase();
  if (v === 'planes' || v === 'plans') return 'planes';
  if (v === 'auditoriaglobal' || v === 'auditoria-global' || v === 'audit') return 'auditoriaGlobal';
  return 'menuBuilder';
}

/**
 * Pantalla única SuperAdmin: constructor del menú maestro + activación por plan + catálogo de planes.
 */
export function SuperAdminMenuPlansHubPage() {
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();

  const tab = useMemo(() => parseHubTab(searchParams.get('tab')), [searchParams]);

  const setTab = useCallback(
    (next: HubTab) => {
      setSearchParams(next === 'menuBuilder' ? {} : { tab: next }, { replace: true });
    },
    [setSearchParams],
  );

  return (
    <SuperAdminPageTemplate title={t('superadmin.menuPlansHub.title')} subtitle={t('superadmin.menuPlansHub.subtitle')}>
      <div className="menu-plans-hub">
        <div className="menu-plans-hub__tabs" role="tablist" aria-label="Pestañas de panel SuperAdmin">
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'menuBuilder'}
            id="menu-plans-tab-menu-builder"
            className={`menu-plans-hub__tab${tab === 'menuBuilder' ? ' is-active' : ''}`}
            onClick={() => setTab('menuBuilder')}
          >
            📁 Constructor de Menús
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'planes'}
            id="menu-plans-tab-planes"
            className={`menu-plans-hub__tab${tab === 'planes' ? ' is-active' : ''}`}
            onClick={() => setTab('planes')}
          >
            📊 Planes y Suscripciones
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'auditoriaGlobal'}
            id="menu-plans-tab-auditoria-global"
            className={`menu-plans-hub__tab${tab === 'auditoriaGlobal' ? ' is-active' : ''}`}
            onClick={() => setTab('auditoriaGlobal')}
          >
            📜 Auditoría Global
          </button>
        </div>

        <div
          className="menu-plans-hub__panel"
          role="tabpanel"
          aria-labelledby={
            tab === 'menuBuilder'
              ? 'menu-plans-tab-menu-builder'
              : tab === 'planes'
                ? 'menu-plans-tab-planes'
                : 'menu-plans-tab-auditoria-global'
          }
        >
          {tab === 'menuBuilder' ? (
            <SuperAdminMenuBuilderSection crmWorkspace />
          ) : null}

          {tab === 'planes' ? (
            <section className="menu-plans-hub__placeholderCard" aria-labelledby="menu-plans-catalog-heading">
              <h2 id="menu-plans-catalog-heading">📊 Gestión de Planes y Suscripciones</h2>
              <p className="subtle">Aquí se mostrará la administración de planes, precios, ciclos de facturación y suscripciones de clientes.</p>
              <div className="menu-plans-hub__placeholderNote">
                <span aria-hidden>ℹ️</span> Módulo en construcción.
              </div>
            </section>
          ) : null}

          {tab === 'auditoriaGlobal' ? (
            <section className="menu-plans-hub__placeholderCard" aria-labelledby="menu-plans-audit-heading">
              <h2 id="menu-plans-audit-heading">📜 Auditoría Global del Sistema</h2>
              <p className="subtle">Registro de todas las acciones de SuperAdmin, cambios en menús, asignaciones de planes, etc.</p>
              <div className="menu-plans-hub__placeholderNote">
                <span aria-hidden>ℹ️</span> Módulo en construcción.
              </div>
            </section>
          ) : null}
        </div>
      </div>
    </SuperAdminPageTemplate>
  );
}
