import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { accessService } from '../services/accessService';
import { useAccessStore } from '../store/accessStore';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import type { AuthResponse } from '../types/auth';
import { useI18n } from '../i18n/i18n';
import './TenantSelectPage.css';

export function TenantSelectPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const bootstrapToken = useAccessStore((s) => s.bootstrapToken);
  const bootstrapUser = useAccessStore((s) => s.bootstrapUser);
  const tenants = useAccessStore((s) => s.tenants);
  const clearBootstrap = useAccessStore((s) => s.clearBootstrap);
  const login = useAuthStore((s) => s.login);
  const setPermissions = usePermissionsStore((s) => s.setPermissions);

  const [q, setQ] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return tenants;
    return tenants.filter((x) =>
      `${x.name} ${x.slug} ${x.tenantId} ${x.role}`.toLowerCase().includes(query)
    );
  }, [q, tenants]);

  if (!bootstrapToken || tenants.length === 0) {
    return (
      <div className="tenant-bg">
        <div className="tenant-card">
          <h1 className="tenant-title">{t('tenantSelect.title')}</h1>
          <p className="tenant-subtitle">{t('tenantSelect.missing')}</p>
          <button className="tenant-btn" onClick={() => navigate('/login')}>
            {t('tenantSelect.back')}
          </button>
        </div>
      </div>
    );
  }

  const choose = async (tenantId: string) => {
    setError('');
    setLoading(true);
    try {
      const session = await accessService.switchTenant(bootstrapToken, { tenantId });
      const auth: AuthResponse = {
        userId: session.userId,
        fullName: session.fullName,
        email: session.email,
        role: session.role,
        tenantId: session.tenantId,
        token: session.token,
      };
      login(auth);
      const perms = await accessService.getMyPermissions();
      setPermissions(perms?.permissions ?? []);
      clearBootstrap();
      navigate('/dashboard');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('tenantSelect.error.default');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="tenant-bg">
      <div className="tenant-card">
        <div className="tenant-header">
          <div className="tenant-logo">ZH</div>
          <div>
            <h1 className="tenant-title">{t('tenantSelect.title')}</h1>
            <p className="tenant-subtitle">
              {t('tenantSelect.subtitle')} {bootstrapUser?.email ?? ''}
            </p>
          </div>
        </div>

        <input
          className="tenant-search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={t('tenantSelect.search')}
          disabled={loading}
        />

        {error && <p className="tenant-error">{error}</p>}

        <div className="tenant-list">
          {filtered.map((x) => (
            <button
              key={x.tenantId}
              className="tenant-item"
              disabled={loading}
              onClick={() => choose(x.tenantId)}
            >
              <div className="tenant-item-name">{x.name}</div>
              <div className="tenant-item-meta">
                <span className="tenant-pill">{x.role}</span>
                <span className="tenant-muted">{x.slug}</span>
                <span className="tenant-mono">{x.tenantId}</span>
              </div>
            </button>
          ))}
        </div>

        <div className="tenant-footer">
          <button className="tenant-link" onClick={() => navigate('/login')} disabled={loading}>
            {t('tenantSelect.back')}
          </button>
        </div>
      </div>
    </div>
  );
}

