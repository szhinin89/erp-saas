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
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { loginSchema, type LoginFormValues } from '../schemas/auth/loginSchema';
import { useDeployment } from '../deployment/DeploymentContext';
import { GLOBAL_TENANT_ID } from '../constants/tenantIds';
import { formatApiRequestError } from '../modules/lib/apiError';
import './LoginPage.css';

/* Quitar imports de ZHForm que ya no se usan en este componente */

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
            navigate('/superadmin/overview', { replace: true });
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
    <div className="lp-bg">
      {/* Decoración de fondo */}
      <div className="lp-bg-orb lp-bg-orb--tr" aria-hidden="true" />
      <div className="lp-bg-orb lp-bg-orb--bl" aria-hidden="true" />
      <div className="lp-bg-grid" aria-hidden="true" />

      <div className="lp-wrapper">
        {/* Marca */}
        <header className="lp-brand">
          <div className="lp-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">dashboard</span>
          </div>
          <h1 className="lp-brand-name">ZH Technologies</h1>
          <p className="lp-brand-sub">Acceso al Portal ERP Corporativo</p>
        </header>

        {/* Card */}
        <div className="lp-card">
          <div className="lp-card-body">

            {/* Selector de rol (visual) */}
            <div>
              <span className="lp-roles-label">Seleccionar Rol de Acceso</span>
              <div className="lp-roles-grid" role="group" aria-label="Tipo de acceso">
                <button type="button" className="lp-role lp-role--active">
                  <span className="lp-role-icon material-symbols-outlined">admin_panel_settings</span>
                  <span className="lp-role-label">SuperAdmin</span>
                </button>
                <button type="button" className="lp-role">
                  <span className="lp-role-icon material-symbols-outlined">domain</span>
                  <span className="lp-role-label">Admin Empresa</span>
                </button>
                <button type="button" className="lp-role">
                  <span className="lp-role-icon material-symbols-outlined">person</span>
                  <span className="lp-role-label">Usuario</span>
                </button>
              </div>
            </div>

            {/* Error de autenticación */}
            {error && (
              <div className="lp-error" role="alert">
                <span className="material-symbols-outlined" aria-hidden="true">error</span>
                <span>{error}</span>
              </div>
            )}

            {/* Formulario */}
            <form className="lp-form" onSubmit={handleSubmit(onValid)} noValidate>

              {/* Email */}
              <div className={`lp-field${errors.email ? ' lp-field--error' : ''}`}>
                <label className="lp-label" htmlFor="email">
                  {t('login.email.label')}
                </label>
                <div className="lp-input-wrap">
                  <span className="lp-input-icon material-symbols-outlined" aria-hidden="true">mail</span>
                  <input
                    className="lp-input"
                    id="email"
                    type="email"
                    placeholder={t('login.email.placeholder')}
                    autoComplete="username"
                    disabled={loading}
                    {...register('email')}
                  />
                </div>
                {errors.email?.message && (
                  <span className="lp-field-error">{errors.email.message}</span>
                )}
              </div>

              {/* Contraseña */}
              <div className={`lp-field${errors.password ? ' lp-field--error' : ''}`}>
                <div className="lp-field-header">
                  <label className="lp-label" htmlFor="password">
                    {t('login.password.label')}
                  </label>
                  <button
                    type="button"
                    className="lp-forgot"
                    onClick={() => navigate('/forgot-password')}
                  >
                    {t('login.forgotPassword')}
                  </button>
                </div>
                <div className="lp-input-wrap">
                  <span className="lp-input-icon material-symbols-outlined" aria-hidden="true">lock</span>
                  <input
                    className="lp-input"
                    id="password"
                    type="password"
                    placeholder={t('login.password.placeholder')}
                    autoComplete="current-password"
                    disabled={loading}
                    {...register('password')}
                  />
                </div>
                {errors.password?.message && (
                  <span className="lp-field-error">{errors.password.message}</span>
                )}
              </div>

              {/* Botón */}
              <button type="submit" className="lp-submit" disabled={loading}>
                <span>{loading ? t('login.button.loading') : t('login.button.submit')}</span>
                {!loading && (
                  <span className="material-symbols-outlined" aria-hidden="true">login</span>
                )}
              </button>

            </form>
          </div>

          {/* Footer del card */}
          <div className="lp-card-footer">
            <span className="lp-footer-copy">© 2024 ZH Technologies</span>
            <nav className="lp-footer-links" aria-label="Vínculos legales">
              <a className="lp-footer-link" href="#">Soporte</a>
              <a className="lp-footer-link" href="#">Legal</a>
            </nav>
          </div>
        </div>

        {/* Badges de seguridad */}
        <div className="lp-security" aria-label="Certificaciones de seguridad">
          <span className="material-symbols-outlined" aria-hidden="true">verified_user</span>
          <span className="lp-security-text">AES-256 Encrypted</span>
          <span className="lp-security-dot" aria-hidden="true" />
          <span className="material-symbols-outlined" aria-hidden="true">shield</span>
          <span className="lp-security-text">SOC2 Compliant</span>
        </div>
      </div>
    </div>
  );
}
