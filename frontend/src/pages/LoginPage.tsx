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
  const login              = useAuthStore((s) => s.login);
  const clearPermissions   = usePermissionsStore((s) => s.clearPermissions);
  const setPermissionSnapshot = usePermissionsStore((s) => s.setPermissionSnapshot);
  const setBootstrap       = useAccessStore((s) => s.setBootstrap);
  const clearBootstrap     = useAccessStore((s) => s.clearBootstrap);
  const { t } = useI18n();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const [error,        setError]        = useState('');
  const [loading,      setLoading]      = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  /* ── Auth helpers ─────────────────────────────────────────── */

  const enterTenantDashboard = async (auth: AuthResponse) => {
    clearBootstrap();
    clearPermissions();
    login(auth);
    try {
      const perms = await accessService.getMyPermissions();
      setPermissionSnapshot({
        permissions:    perms?.permissions    ?? [],
        planCode:       perms?.planCode       ?? null,
        enabledModules: perms?.enabledModules ?? [],
      });
    } catch {
      // AppLayout re-fetches permissions if empty after redirect.
    }
    navigate('/dashboard', { replace: true });
  };

  const onValid = async (form: LoginFormValues) => {
    setError('');
    setLoading(true);

    const credentials = {
      email:    form.email.trim().toLowerCase(),
      password: form.password,
    };

    try {
      /* ── 1. Direct login (identity user or legacy single-tenant) ── */
      try {
        const { data } = await api.post<ApiResponse<AuthResponse>>(
          '/api/auth/login',
          credentials,
        );
        const payload = data.responseObject;

        if (payload?.token) {
          const isGlobalSuperAdmin =
            payload.role === 'SuperAdmin' &&
            normalizeUuid(payload.tenantId) === normalizeUuid(GLOBAL_TENANT_ID);

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
        // Wrong credentials or multi-tenant user → try bootstrap.
      }

      /* ── 2. Bootstrap (multi-tenant flow) ── */
      const bootstrap = await accessService.bootstrapLogin(credentials);
      setBootstrap(bootstrap);

      if (bootstrap.tenants.length === 0) {
        setError(t('login.error.default'));
        return;
      }

      /* Single tenant → enter directly */
      if (bootstrap.tenants.length === 1) {
        const session = await accessService.switchTenant(bootstrap.bootstrapToken, {
          tenantId: bootstrap.tenants[0].tenantId,
        });
        const auth: AuthResponse = {
          userId:         session.userId,
          fullName:       session.fullName,
          email:          session.email,
          role:           session.role,
          tenantId:       session.tenantId,
          token:          session.token,
          planCode:       session.planCode,
          enabledModules: session.enabledModules ?? [],
        };
        await enterTenantDashboard(auth);
        return;
      }

      /* Multiple tenants → tenant selector */
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

  /* ── Render ───────────────────────────────────────────────── */

  return (
    <div className="zh-auth-bg">
      {/* Decorative background */}
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">

        {/* ── Brand ── */}
        <div className="lp-brand">
          <div className="lp-brand-row">
            <div className="lp-brand-icon" aria-hidden="true">
              <span className="material-symbols-outlined">dashboard</span>
            </div>
            <h1 className="lp-brand-name">ZH Technologies</h1>
          </div>
          <p className="lp-brand-sub">Acceso al Portal ERP Corporativo</p>
        </div>

        {/* ── Login card ── */}
        <div className="lp-card">
          <div className="lp-card-body">

            {/* Error alert */}
            {error && (
              <div className="lp-error" role="alert">
                <span className="material-symbols-outlined" aria-hidden="true">error</span>
                <span>{error}</span>
              </div>
            )}

            {/* Form */}
            <form className="lp-form" onSubmit={handleSubmit(onValid)} noValidate>

              {/* Email */}
              <div className={`zh-auth-field${errors.email ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="lp-email">
                  {t('login.email.label')}
                </label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">
                    mail
                  </span>
                  <input
                    className="zh-auth-input"
                    id="lp-email"
                    type="email"
                    placeholder={t('login.email.placeholder')}
                    autoComplete="username"
                    disabled={loading}
                    {...register('email')}
                  />
                </div>
                {errors.email?.message && (
                  <span className="zh-auth-field-error" role="alert">
                    {errors.email.message}
                  </span>
                )}
              </div>

              {/* Password */}
              <div className={`zh-auth-field${errors.password ? ' zh-auth-field--error' : ''}`}>
                <div className="lp-field-header">
                  <label className="zh-auth-label" htmlFor="lp-password">
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
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">
                    lock
                  </span>
                  <input
                    className="zh-auth-input lp-input-password"
                    id="lp-password"
                    type={showPassword ? 'text' : 'password'}
                    placeholder={t('login.password.placeholder')}
                    autoComplete="current-password"
                    disabled={loading}
                    {...register('password')}
                  />
                  <button
                    type="button"
                    className="lp-password-toggle"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                  >
                    <span className="material-symbols-outlined">
                      {showPassword ? 'visibility_off' : 'visibility'}
                    </span>
                  </button>
                </div>
                {errors.password?.message && (
                  <span className="zh-auth-field-error" role="alert">
                    {errors.password.message}
                  </span>
                )}
              </div>

              {/* Remember me */}
              <div className="lp-remember">
                <input type="checkbox" id="lp-remember" />
                <label className="lp-remember-label" htmlFor="lp-remember">
                  Recordar sesión en este equipo
                </label>
              </div>

              {/* Submit */}
              <button
                type="submit"
                className={`lp-submit${loading ? ' lp-submit--loading' : ''}`}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="lp-submit-spinner" aria-hidden="true" />
                    <span>{t('login.button.loading')}</span>
                  </>
                ) : (
                  <>
                    <span>{t('login.button.submit')}</span>
                    <span className="material-symbols-outlined" aria-hidden="true">login</span>
                  </>
                )}
              </button>

            </form>
          </div>

          {/* Card footer */}
          <div className="lp-card-footer">
            <span className="lp-footer-copy">© 2024 ZH Technologies</span>
            <nav className="lp-footer-links" aria-label="Vínculos legales">
              <a className="lp-footer-link" href="#">Soporte</a>
              <a className="lp-footer-link" href="#">Legal</a>
            </nav>
          </div>
        </div>

        {/* Security badges */}
        <div className="lp-security" aria-label="Certificaciones de seguridad">
          <div className="lp-security-badge">
            <span className="material-symbols-outlined" aria-hidden="true">verified_user</span>
            <span className="lp-security-text">AES-256 Encrypted</span>
          </div>
          <span className="lp-security-dot" aria-hidden="true" />
          <div className="lp-security-badge">
            <span className="material-symbols-outlined" aria-hidden="true">shield</span>
            <span className="lp-security-text">SOC2 Compliant</span>
          </div>
        </div>

      </div>
    </div>
  );
}
