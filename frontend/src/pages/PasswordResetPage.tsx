import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import './LoginPage.css';

type PasswordResetRequest = {
  tenantId: string;
  email: string;
  newPassword: string;
};

export function PasswordResetPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const [tenantAllowsDirectReset, setTenantAllowsDirectReset] = useState<boolean | null>(null);
  const [tenantCheckError, setTenantCheckError] = useState<string>('');
  const [form, setForm] = useState<PasswordResetRequest>({
    tenantId: '',
    email: '',
    newPassword: '',
  });
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const raw = form.tenantId.trim();
    if (!raw) {
      Promise.resolve().then(() => setTenantCheckError(''));
      Promise.resolve().then(() => setTenantAllowsDirectReset(null));
      return;
    }

    // Only check when it looks like a GUID to avoid noisy calls.
    const isGuid = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(raw);
    if (!isGuid) {
      Promise.resolve().then(() => setTenantCheckError(''));
      Promise.resolve().then(() => setTenantAllowsDirectReset(null));
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        Promise.resolve().then(() => setTenantCheckError(''));
        const { data } = await api.get<ApiResponse<{ tenantId: string; passwordResetMode: number }>>(
          `/api/tenants/${raw}/public-settings`
        );
        if (cancelled) return;
        Promise.resolve().then(() => setTenantAllowsDirectReset(data.responseObject.passwordResetMode === 1));
      } catch {
        if (cancelled) return;
        // If we can't verify (network / env), do not hard-block the flow.
        Promise.resolve().then(() => setTenantCheckError(t('reset.tenantCheck.unavailable')));
        Promise.resolve().then(() => setTenantAllowsDirectReset(null));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [form.tenantId, t]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    if (tenantAllowsDirectReset === false) {
      setError(t('reset.error.disabled'));
      return;
    }

    if (tenantAllowsDirectReset === null && tenantCheckError) {
      setError(t('reset.tenantCheck.unavailable'));
      return;
    }

    if (form.newPassword !== confirmPassword) {
      setError(t('reset.error.mismatch'));
      return;
    }

    setLoading(true);
    try {
      await api.post<ApiResponse<object>>('/api/auth/password-reset', form);
      setSuccess(t('reset.success'));
      setTimeout(() => navigate('/login'), 800);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })
          ?.response?.data?.message ?? t('login.error.default');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-bg">
      <div className="login-card">
        <div className="login-header">
          <div className="login-logo">ZH</div>
          <h1 className="login-title">{t('reset.title')}</h1>
          <p className="login-subtitle">{t('reset.subtitle')}</p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="tenantId">{t('login.tenantId.label')}</label>
            <input
              id="tenantId"
              type="text"
              placeholder={t('login.tenantId.placeholder')}
              value={form.tenantId}
              onChange={(e) => setForm((f) => ({ ...f, tenantId: e.target.value }))}
              required
              autoComplete="off"
            />
            {tenantAllowsDirectReset === true && (
              <small style={{ color: '#065f46' }}>{t('reset.tenantCheck.enabled')}</small>
            )}
            {tenantAllowsDirectReset === false && (
              <small style={{ color: '#b91c1c' }}>{t('reset.error.disabled')}</small>
            )}
            {tenantAllowsDirectReset === null && tenantCheckError && (
              <small style={{ color: '#b45309' }}>{tenantCheckError}</small>
            )}
          </div>

          <div className="field">
            <label htmlFor="email">{t('reset.email.label')}</label>
            <input
              id="email"
              type="email"
              placeholder={t('login.email.placeholder')}
              value={form.email}
              onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
              required
              autoComplete="username"
            />
          </div>

          <div className="field">
            <label htmlFor="newPassword">{t('reset.newPassword.label')}</label>
            <input
              id="newPassword"
              type="password"
              placeholder={t('login.password.placeholder')}
              value={form.newPassword}
              onChange={(e) => setForm((f) => ({ ...f, newPassword: e.target.value }))}
              required
              autoComplete="new-password"
            />
          </div>

          <div className="field">
            <label htmlFor="confirmPassword">{t('reset.confirmPassword.label')}</label>
            <input
              id="confirmPassword"
              type="password"
              placeholder={t('login.password.placeholder')}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              autoComplete="new-password"
            />
          </div>

          {error && <p className="login-error">{error}</p>}
          {success && <p style={{ color: '#065f46', margin: 0 }}>{success}</p>}

          <button className="login-btn" type="submit" disabled={loading || tenantAllowsDirectReset === false}>
            {loading ? t('reset.button.loading') : t('reset.button.submit')}
          </button>
        </form>
      </div>
    </div>
  );
}

