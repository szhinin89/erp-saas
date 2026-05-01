import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { useAuthStore } from '../store/authStore';
import type { AuthResponse, LoginRequest } from '../types/auth';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { accessService } from '../services/accessService';
import { useAccessStore } from '../store/accessStore';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHFormActions } from '../components/zh/ZHForm';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
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
      // 0) SuperAdmin global login: entra directo al Panel Global.
      // Si no es SuperAdmin, este endpoint responde 401 y seguimos con el flujo IAM normal.
      try {
        const { data } = await api.post<ApiResponse<AuthResponse>>('/api/auth/superadmin-login', {
          email: form.email,
          password: form.password,
        });
        login(data.responseObject);
        navigate('/superadmin');
        return;
      } catch {
        // noop
      }

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
    <ZHCenteredCard bgClassName="login-bg" cardClassName="login-card">
      <form className="login-form" onSubmit={handleSubmit}>
          <ZHFormHeader
            title={t('login.title')}
            subtitle={t('login.subtitle')}
          />
          <ZHFormBody>
            {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}

            <ZHFormSection title={t('login.title')}>
              <ZHGrid cols={1}>
                <ZHField label={t('login.email.label')} required>
                  <input
                    id="email"
                    type="email"
                    placeholder={t('login.email.placeholder')}
                    value={form.email}
                    onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                    required
                    autoComplete="username"
                    disabled={loading}
                  />
                </ZHField>

                <ZHField label={t('login.password.label')} required>
                  <input
                    id="password"
                    type="password"
                    placeholder={t('login.password.placeholder')}
                    value={form.password}
                    onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
                    required
                    autoComplete="current-password"
                    disabled={loading}
                  />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>

            <ZHFormActions
              onCancel={() => navigate('/password-reset')}
              onDraft={undefined}
              onSave={undefined}
              hideDraft
              disableDraft
              disableSave={loading}
              saveButtonType="submit"
              labels={{
                cancel: t('login.forgotPassword'),
                draft: t('common.saveDraft') ?? 'Guardar borrador',
                save: loading ? t('login.button.loading') : t('login.button.submit'),
              }}
            />
          </ZHFormBody>
        </form>
    </ZHCenteredCard>
  );
}
