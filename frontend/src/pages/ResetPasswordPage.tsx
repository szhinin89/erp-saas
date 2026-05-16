import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { resetWithTokenFormSchema, type ResetWithTokenFormValues } from '../schemas/auth/resetWithTokenSchema';
import { formatApiRequestError, readApiErrorMessage } from '../modules/lib/apiError';
import './ResetPasswordPage.css';

const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { t } = useI18n();

  const token = useMemo(() => (searchParams.get('token') ?? '').trim(), [searchParams]);
  const tenantIdRaw = useMemo(() => (searchParams.get('tenantId') ?? '').trim(), [searchParams]);
  const tenantId =
    tenantIdRaw.length > 0 && guidRegex.test(tenantIdRaw) ? tenantIdRaw : undefined;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ResetWithTokenFormValues>({
    resolver: zodResolver(resetWithTokenFormSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  const missingToken = !token;

  const onValid = async (form: ResetWithTokenFormValues) => {
    setError('');
    setSuccess('');
    if (missingToken) return;

    setLoading(true);
    try {
      const body: { token: string; newPassword: string; tenantId?: string } = {
        token,
        newPassword: form.newPassword,
      };
      if (tenantId) body.tenantId = tenantId;

      await api.post<ApiResponse<object>>('/api/auth/reset-password', body);
      setSuccess(t('reset.success'));
      setTimeout(() => navigate('/login'), 900);
    } catch (err: unknown) {
      const fromApi = readApiErrorMessage(err);
      if (fromApi) {
        setError(fromApi);
      } else {
        setError(
          formatApiRequestError(err, {
            offline: t('common.apiUnreachable'),
            generic: t('login.error.default'),
          }),
        );
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        {/* Marca */}
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">lock_reset</span>
          </div>
          <h1 className="rp-brand-name">ZH Technologies</h1>
          <p className="rp-brand-sub">Restablecer Contraseña</p>
        </header>

        {/* Card */}
        <div className="zh-auth-card">
          <div className="zh-auth-card-header">
            <h2 className="zh-auth-card-title">{t('resetWithToken.title')}</h2>
            <p className="zh-auth-card-desc">{t('resetWithToken.subtitle')}</p>
          </div>

          {missingToken && (
            <ZHPageNotice
              variant="error"
              message={t('common.errorPrefix')}
              detail={t('resetWithToken.missingToken')}
            />
          )}
          {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}
          {success && <ZHPageNotice variant="success" message={success} />}

          <form className="zh-auth-form" onSubmit={handleSubmit(onValid)} noValidate>
            {/* Nueva contraseña */}
            <div className={`zh-auth-field${errors.newPassword ? ' zh-auth-field--error' : ''}`}>
              <label className="zh-auth-label" htmlFor="newPassword">
                {t('reset.newPassword.label')}
              </label>
              <div className="zh-auth-input-wrap">
                <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">lock</span>
                <input
                  className="zh-auth-input"
                  id="newPassword"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="new-password"
                  disabled={loading || missingToken}
                  {...register('newPassword')}
                />
              </div>
              {errors.newPassword?.message && (
                <span className="zh-auth-field-error">{errors.newPassword.message}</span>
              )}
            </div>

            {/* Confirmar contraseña */}
            <div className={`zh-auth-field${errors.confirmPassword ? ' zh-auth-field--error' : ''}`}>
              <label className="zh-auth-label" htmlFor="confirmPassword">
                {t('reset.confirmPassword.label')}
              </label>
              <div className="zh-auth-input-wrap">
                <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">lock_open</span>
                <input
                  className="zh-auth-input"
                  id="confirmPassword"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="new-password"
                  disabled={loading || missingToken}
                  {...register('confirmPassword')}
                />
              </div>
              {errors.confirmPassword?.message && (
                <span className="zh-auth-field-error">{errors.confirmPassword.message}</span>
              )}
            </div>

            <button
              type="submit"
              className="zh-auth-submit"
              disabled={loading || missingToken}
            >
              <span>{loading ? t('reset.button.loading') : t('resetWithToken.button.submit')}</span>
              {!loading && (
                <span className="material-symbols-outlined" aria-hidden="true">check_circle</span>
              )}
            </button>
          </form>

          <div className="zh-auth-divider">
            <div className="zh-auth-info-box">
              <span className="material-symbols-outlined zh-auth-info-icon" aria-hidden="true">info</span>
              <div>
                <p className="zh-auth-info-title">Requisitos de contraseña</p>
                <ul className="zh-auth-info-list">
                  <li>Mínimo 8 caracteres de longitud.</li>
                  <li>Ambos campos deben coincidir exactamente.</li>
                  <li>El enlace de restablecimiento es de un solo uso.</li>
                </ul>
              </div>
            </div>
          </div>

          <button type="button" className="zh-auth-back" onClick={() => navigate('/login')}>
            <span className="material-symbols-outlined zh-auth-back-arrow" aria-hidden="true">arrow_back</span>
            Volver al inicio de sesión
          </button>
        </div>

        <div className="zh-auth-banner" aria-hidden="true">
          <span className="material-symbols-outlined zh-auth-banner-icon">verified_user</span>
          <div className="zh-auth-banner-overlay" />
        </div>

        <footer className="zh-auth-footer">
          <div className="zh-auth-footer-links">
            <a href="#" className="zh-auth-footer-link">Centro de ayuda</a>
            <span className="zh-auth-footer-sep" aria-hidden="true">|</span>
            <a href="#" className="zh-auth-footer-link">Privacidad</a>
          </div>
          <p className="zh-auth-footer-copy">© 2024 ZH Technologies. Todos los derechos reservados.</p>
        </footer>
      </div>
    </div>
  );
}
