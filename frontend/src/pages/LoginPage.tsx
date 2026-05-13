import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from 'react-router-dom';
import { api } from '../modules/lib/api';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import type { AuthResponse } from '../types/auth';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { accessService } from '../services/accessService';
import { useAccessStore } from '../store/accessStore';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormActions } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import { loginSchema, type LoginFormValues } from '../schemas/auth/loginSchema';
import { useDeployment } from '../deployment/DeploymentContext';
import { GLOBAL_TENANT_ID } from '../constants/tenantIds';
import { formatApiRequestError } from '../modules/lib/apiError';
import './LoginPage.css';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

export function LoginPage() {
  const navigate = useNavigate();
  const { superAdminPanelEnabled } = useDeployment();
  const login = useAuthStore((s) => s.login);
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);
  const setPermissionSnapshot = usePermissionsStore((s) => s.setPermissionSnapshot);
  const setBootstrap = useAccessStore((s) => s.setBootstrap);
  const clearBootstrap = useAccessStore((s) => s.clearBootstrap);
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

  /** Sesión con tenant operativo: permisos + dashboard de esa empresa (misma idea que TenantSelectPage). */
  const enterTenantDashboard = async (auth: AuthResponse) => {
    clearBootstrap();
    clearPermissions();
    login(auth);
    try {
      const perms = await accessService.getMyPermissions();
      setPermissionSnapshot({
        permissions: perms?.permissions ?? [],
        planCode: perms?.planCode ?? null,
        enabledModules: perms?.enabledModules ?? [],
      });
    } catch {
      // AppLayout vuelve a pedir permisos si siguen vacíos.
    }
    navigate('/dashboard', { replace: true });
  };

  const onValid = async (form: LoginFormValues) => {
    setError('');
    setLoading(true);

    const credentials = { email: form.email.trim().toLowerCase(), password: form.password };

    try {
      // Un solo POST: el backend ya resuelve SuperAdmin → JWT global; luego identity/legacy tenant.
      // Evitar un segundo POST superadmin-login: el 401 disparaba el interceptor global (logout + reload).
      // Sesión directa: identity_users + 1 empresa (admin creado al alta) y usuarios legacy en `users`.
      try {
        const { data } = await api.post<ApiResponse<AuthResponse>>('/api/auth/login', credentials);
        const payload = data.responseObject;
        if (payload?.token) {
          const isGlobalSuperAdmin =
            payload.role === 'SuperAdmin' && normalizeUuid(payload.tenantId) === normalizeUuid(GLOBAL_TENANT_ID);
          if (superAdminPanelEnabled && isGlobalSuperAdmin) {
            clearBootstrap();
            clearPermissions();
            login(payload);
            navigate('/superadmin', { replace: true });
            return;
          }
          await enterTenantDashboard(payload);
          return;
        }
      } catch {
        // Credenciales incorrectas, multi-tenant (login devuelve error), etc. → intentar bootstrap.
      }

      const bootstrap = await accessService.bootstrapLogin(credentials);
      setBootstrap(bootstrap);

      if (bootstrap.tenants.length === 0) {
        setError(t('login.error.default'));
        return;
      }

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
        await enterTenantDashboard(auth);
        return;
      }

      navigate('/select-tenant', { replace: true });
    } catch (err: unknown) {
      setError(
        formatApiRequestError(err, {
          offline: t('common.apiUnreachable'),
          generic: t('login.error.default'),
        }),
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <ZHCenteredCard bgClassName="login-bg" cardClassName="login-card">
      <form className="login-form" onSubmit={handleSubmit(onValid)} noValidate>
        <ZHFormHeader title={t('login.title')} subtitle={t('login.subtitle')} />
        <ZHFormBody>
          {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

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
            onCancel={() => navigate('/forgot-password')}
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
