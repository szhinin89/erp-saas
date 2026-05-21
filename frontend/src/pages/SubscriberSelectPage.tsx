import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { accessService } from '../services/accessService';
import { syncSessionEntitlements } from '../lib/syncSessionEntitlements';
import { useAccessStore } from '../store/accessStore';
import { useAuthStore } from '../store/authStore';
import type { AuthResponse } from '../types/auth';
import { useI18n } from '../i18n/i18n';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import './SubscriberSelectPage.css';

const AVATAR_VARIANTS = ['primary', 'secondary', 'tertiary'] as const;

export function SubscriberSelectPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const bootstrapToken = useAccessStore((s) => s.bootstrapToken);
  const subscribers = useAccessStore((s) => s.subscribers);
  const clearBootstrap = useAccessStore((s) => s.clearBootstrap);
  const login = useAuthStore((s) => s.login);

  const [q, setQ] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return subscribers;
    return subscribers.filter((x) =>
      `${x.name} ${x.slug} ${x.subscriberId} ${x.role}`.toLowerCase().includes(query)
    );
  }, [q, subscribers]);

  if (!bootstrapToken || subscribers.length === 0) {
    return (
      <div className="zh-auth-bg">
        <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
        <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
        <div className="zh-auth-bg-grid" aria-hidden="true" />
        <div className="zh-auth-wrapper ts-wrapper">
          <div className="ts-card">
            <div className="ts-card-body">
              <h2 className="ts-title">{t('subscriberSelect.title')}</h2>
              <p style={{ fontSize: '13px', color: 'var(--color-text-secondary)' }}>
                {t('subscriberSelect.missing')}
              </p>
              <button className="zh-btn zh-btn--primary" onClick={() => navigate('/login')}>
                {t('subscriberSelect.back')}
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  const choose = async (subscriberId: string) => {
    setError('');
    setLoading(true);
    try {
      const session = await accessService.switchSubscriber(bootstrapToken, { subscriberId });
      const auth: AuthResponse = {
        userId: session.userId,
        fullName: session.fullName,
        email: session.email,
        role: session.role,
        subscriberId: session.subscriberId,
        companyId: session.companyId,
        token: session.token,
        planCode: session.planCode,
        enabledModules: session.enabledModules ?? [],
      };
      login(auth);
      clearBootstrap();
      await syncSessionEntitlements();
      navigate('/saas/overview', { replace: true });
    } catch (err: unknown) {
      const ax = err as { response?: { status?: number; data?: { message?: string } } };
      const status = ax?.response?.status;
      const apiMsg = ax?.response?.data?.message;

      if (status === 401) {
        clearBootstrap();
        setError(t('subscriberSelect.missing'));
        navigate('/login');
        return;
      }

      setError(apiMsg ?? t('subscriberSelect.error.default'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper ts-wrapper">
        {/* Marca */}
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">dashboard</span>
          </div>
          <h1 className="zh-auth-brand-name">ZH Technologies</h1>
          <p className="zh-auth-brand-sub">Portal de Gestión Multi-empresa</p>
        </header>

        {/* Card */}
        <div className="ts-card">
          <div className="ts-card-body">
            {/* Encabezado */}
            <div className="ts-card-head">
              <h2 className="ts-title">{t('subscriberSelect.title')}</h2>
              <span className="ts-count">
                {subscribers.length} {subscribers.length === 1 ? 'empresa' : 'empresas'}
              </span>
            </div>

            {/* Búsqueda */}
            <input
              className="ts-search"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t('subscriberSelect.search')}
              disabled={loading}
            />

            {/* Error */}
            {error && (
              <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />
            )}

            {/* Lista de empresas */}
            <div className="ts-list" role="list">
              {filtered.map((x, i) => (
                <div key={x.subscriberId} className="zh-entity-item" role="listitem">
                  <div
                    className={`zh-avatar zh-avatar--${AVATAR_VARIANTS[i % AVATAR_VARIANTS.length]}`}
                    aria-hidden="true"
                  >
                    {x.name.charAt(0).toUpperCase()}
                  </div>
                  <div className="zh-entity-item-info">
                    <span className="zh-entity-item-name">{x.name}</span>
                    <span className="zh-entity-item-sub mono">{x.subscriberId}</span>
                  </div>
                  <div className="zh-entity-item-right">
                    <span className="zh-status zh-status--active">Activo</span>
                    <button
                      className="zh-btn zh-btn--primary zh-btn--sm"
                      disabled={loading}
                      onClick={() => choose(x.subscriberId)}
                    >
                      Entrar
                    </button>
                  </div>
                </div>
              ))}
              {filtered.length === 0 && (
                <p className="ts-empty">No se encontraron empresas</p>
              )}
            </div>

            {/* Pie del card */}
            <div className="ts-card-footer">
              <span>¿No encuentra su empresa?</span>
              <a href="#" className="ts-card-footer-link">Solicitar acceso</a>
            </div>
          </div>
        </div>

        {/* Pie de página */}
        <div className="ts-bottom">
          <button
            className="zh-btn zh-btn--ghost zh-btn--sm"
            disabled={loading}
            onClick={() => { clearBootstrap(); navigate('/login'); }}
          >
            <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>logout</span>
            {t('subscriberSelect.back')}
          </button>
          <nav className="ts-footer-nav" aria-label="Vínculos">
            <a href="#" className="ts-footer-link">Términos</a>
            <a href="#" className="ts-footer-link">Soporte</a>
          </nav>
        </div>
      </div>
    </div>
  );
}
