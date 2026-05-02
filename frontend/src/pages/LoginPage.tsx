import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from 'react-router-dom';
import { api } from '../modules/lib/api';
import { useAuthStore } from '../store/authStore';
import type { AuthResponse } from '../types/auth';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { accessService } from '../services/accessService';
import { useAccessStore } from '../store/accessStore';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHFormActions } from '../components/zh/ZHForm';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import { loginSchema, type LoginFormValues } from '../schemas/auth/loginSchema';
import './LoginPage.css';

export function LoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const setBootstrap = useAccessStore((s) => s.setBootstrap);
  const { t } = useI18n();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const onValid = async (form: LoginFormValues) => {
    setError('');
    setLoading(true);

    try {
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
          planCode: session.planCode,
          enabledModules: session.enabledModules ?? [],
        };
        login(auth);
        navigate('/dashboard');
      } else {
        navigate('/select-tenant');
      }
    } catch {
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
      <form className="login-form" onSubmit={handleSubmit(onValid)} noValidate>
        <ZHFormHeader title={t('login.title')} subtitle={t('login.subtitle')} />
        <ZHFormBody>
          {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}

          <ZHFormSection title={t('login.title')}>
            <ZHGrid cols={1}>
              <ZHField label={t('login.email.label')} required fieldError={errors.email?.message}>
                <input
                  id="email"
                  type="email"
                  placeholder={t('login.email.placeholder')}
                  autoComplete="username"
                  disabled={loading}
                  {...register('email')}
                />
              </ZHField>

              <ZHField label={t('login.password.label')} required fieldError={errors.password?.message}>
                <input
                  id="password"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="current-password"
                  disabled={loading}
                  {...register('password')}
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
