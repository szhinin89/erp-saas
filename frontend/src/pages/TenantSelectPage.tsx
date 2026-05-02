import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { accessService } from '../services/accessService';
import { useAccessStore } from '../store/accessStore';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import type { AuthResponse } from '../types/auth';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHFormAlert } from '../components/zh/ZHForm';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import './TenantSelectPage.css';

export function TenantSelectPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const bootstrapToken = useAccessStore((s) => s.bootstrapToken);
  const bootstrapUser = useAccessStore((s) => s.bootstrapUser);
  const tenants = useAccessStore((s) => s.tenants);
  const clearBootstrap = useAccessStore((s) => s.clearBootstrap);
  const login = useAuthStore((s) => s.login);
  const setPermissionSnapshot = usePermissionsStore((s) => s.setPermissionSnapshot);

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
      <ZHCenteredCard bgClassName="tenant-bg" cardClassName="tenant-card">
        <h1 className="tenant-title">{t('tenantSelect.title')}</h1>
        <p className="tenant-subtitle">{t('tenantSelect.missing')}</p>
        <ZHBtn variant="primary" size="md" type="button" onClick={() => navigate('/login')}>
          {t('tenantSelect.back')}
        </ZHBtn>
      </ZHCenteredCard>
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
        planCode: session.planCode,
        enabledModules: session.enabledModules ?? [],
      };
      login(auth);
      const perms = await accessService.getMyPermissions();
      setPermissionSnapshot({
        permissions: perms?.permissions ?? [],
        planCode: perms?.planCode ?? null,
        enabledModules: perms?.enabledModules ?? [],
      });
      clearBootstrap();
      navigate('/dashboard');
    } catch (err: unknown) {
      const ax = err as { response?: { status?: number; data?: { message?: string } } };
      const status = ax?.response?.status;
      const apiMsg = ax?.response?.data?.message;

      // Si el bootstrap token expira o es inválido, el policy "Bootstrap" suele responder 401 sin cuerpo.
      // En ese caso, hay que volver al login para re-bootstrap.
      if (status === 401) {
        clearBootstrap();
        setError(t('tenantSelect.missing'));
        navigate('/login');
        return;
      }

      setError(apiMsg ?? t('tenantSelect.error.default'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <ZHCenteredCard bgClassName="tenant-bg" cardClassName="tenant-card">
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

        {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}

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
              </div>
            </button>
          ))}
        </div>

        <div className="tenant-footer">
          <ZHBtn variant="ghost" size="sm" type="button" onClick={() => navigate('/login')} disabled={loading}>
            {t('tenantSelect.back')}
          </ZHBtn>
        </div>
    </ZHCenteredCard>
  );
}

