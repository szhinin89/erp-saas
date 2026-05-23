import { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { platformService, type PlatformSubscriber } from '../api/platformService';
import { goToSubscriberDetail } from '../../../navigation/platformSubscriberDetailNav';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import './PlatformSubscribersPage.css';

export function PlatformSubscribersPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const [items, setItems] = useState<PlatformSubscriber[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    platformService
      .getSubscribers()
      .then((list) => setItems(list ?? []))
      .catch(() => setError('No se pudo cargar la lista de suscriptores.'))
      .finally(() => setLoading(false));
  }, []);

  const filtered = useMemo(
    () =>
      items.filter(
        (s) =>
          !search ||
          s.name.toLowerCase().includes(search.toLowerCase()) ||
          s.slug.toLowerCase().includes(search.toLowerCase()) ||
          (s.planCode ?? '').toLowerCase().includes(search.toLowerCase()),
      ),
    [items, search],
  );

  const openSubscriberSheet = (subscriber: PlatformSubscriber) => {
    goToSubscriberDetail(navigate, subscriber.id);
  };

  return (
    <div className="sa-subscribers-page">
      <div className="sa-subscribers-header">
        <div>
          <h2 className="sa-subscribers-title">Suscriptores</h2>
          <p className="sa-subscribers-sub">
            Abre la ficha de un suscriptor para administrarlo o entrar a su tenant.
          </p>
        </div>
        <div className="sa-subscribers-search">
          <span className="material-symbols-outlined">search</span>
          <input
            type="text"
            placeholder="Buscar por nombre, slug o plan…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {error && <ZHPageNotice variant="error" message={error} />}

      {loading ? (
        <div className="sa-subscribers-center">
          <LoadingState />
        </div>
      ) : filtered.length === 0 ? (
        <div className="sa-subscribers-center">
          <EmptyState message={items.length === 0 ? 'Sin suscriptores registrados.' : 'Sin resultados.'} />
        </div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Suscriptor</th>
                <th>Plan</th>
                <th>Usuarios</th>
                <th>Estado</th>
                <th className="pg-th-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((s) => (
                <tr
                  key={s.id}
                  className={`sa-sub-row${!s.isActive ? ' pg-row-inactive' : ''}`}
                  tabIndex={0}
                  role="button"
                  aria-label={`Abrir ficha de ${s.name}`}
                  onClick={() => openSubscriberSheet(s)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      openSubscriberSheet(s);
                    }
                  }}
                >
                  <td>
                    <div className="sa-sub-name">{s.name}</div>
                    <div className="sa-sub-slug subtle">{s.slug}</div>
                  </td>
                  <td>
                    {s.planCode ? (
                      <span className="badge badge--blue badge--md badge--upper">{s.planCode}</span>
                    ) : (
                      <span className="subtle">—</span>
                    )}
                  </td>
                  <td>
                    <span className="mono">{s.activeUsers}</span>
                    <span className="subtle"> / {s.totalUsers}</span>
                  </td>
                  <td>
                    <span className={s.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                      {s.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="pg-td-right">
                    <div className="sa-sub-actions">
                      <button
                        type="button"
                        className="zh-btn zh-btn--primary zh-btn--sm"
                        title={t('platform.subscriberRow.openSheet')}
                        onClick={(e) => {
                          e.stopPropagation();
                          openSubscriberSheet(s);
                        }}
                      >
                        <span className="material-symbols-outlined">description</span>
                        {t('platform.subscriberRow.openSheet')}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="sa-subscribers-footer subtle">
        {filtered.length} de {items.length} suscriptores
      </div>
    </div>
  );
}
