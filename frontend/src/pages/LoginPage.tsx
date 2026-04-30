import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { useAuthStore } from '../store/authStore';
import type { AuthResponse, LoginRequest } from '../types/auth';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { accessService } from '../services/accessService';
import { useAccessStore } from '../store/accessStore';
import './LoginPage.css';

export function LoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const setBootstrap = useAccessStore((s) => s.setBootstrap);
  const { t } = useI18n();

  const [form, setForm] = useState<LoginRequest>({
    email: '',
    password: '',
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      // IAM flow: bootstrap login → tenant select → session token
      const bootstrap = await accessService.bootstrapLogin(form);
      setBootstrap(bootstrap);

      if (bootstrap.tenants.length === 1) {
        const session = await accessService.switchTenant(bootstrap.bootstrapToken, {
          tenantId: bootstrap.tenants[0].tenantId,
        });

        const auth: AuthResponse = {
          userId: session.userId,
          fullName: session.fullName,
          email: session.email,
          role: session.role,
          tenantId: session.tenantId,
          token: session.token,
        };
        login(auth);
        navigate('/dashboard');
      } else {
        navigate('/select-tenant');
      }
    } catch {
      // Fallback: legacy auth endpoint (mantiene SuperAdmin / compatibilidad mientras se migra).
      try {
        const { data } = await api.post<ApiResponse<AuthResponse>>('/api/auth/login', form);
        login(data.responseObject);
        navigate(
          data.responseObject.role === 'SuperAdmin' &&
            data.responseObject.tenantId === '00000000-0000-0000-0000-000000000000'
            ? '/superadmin'
            : '/dashboard'
        );
      } catch (err2: unknown) {
        const msg =
          (err2 as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          t('login.error.default');
        setError(msg);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-bg">
      <div className="login-card">
        <div className="login-header">
          <div className="login-logo">ZH</div>
          <h1 className="login-title">{t('login.title')}</h1>
          <p className="login-subtitle">{t('login.subtitle')}</p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="email">{t('login.email.label')}</label>
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
            <label htmlFor="password">{t('login.password.label')}</label>
            <input
              id="password"
              type="password"
              placeholder={t('login.password.placeholder')}
              value={form.password}
              onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
              required
              autoComplete="current-password"
            />
          </div>

          {error && <p className="login-error">{error}</p>}

          <button className="login-btn" type="submit" disabled={loading}>
            {loading ? t('login.button.loading') : t('login.button.submit')}
          </button>

          <button
            type="button"
            onClick={() => navigate('/password-reset')}
            className="login-link"
            style={{ marginTop: 12 }}
          >
            {t('login.forgotPassword')}
          </button>
        </form>
      </div>
    </div>
  );
}
