import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useLocation, useNavigate } from 'react-router-dom';
import { authService } from '../api/authService';
import { useAuthStore } from '../../../store/authStore';
import { completeLoginNavigation } from '../completeLoginNavigation';
import { useI18n } from '../../../i18n/i18n';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import {
  completePasswordResetFormSchema,
  type CompletePasswordResetFormValues,
} from '../../../schemas/auth/completePasswordResetSchema';
import { formatApiRequestError, readApiErrorMessage } from '../../lib/apiError';
import './CompletePasswordResetPage.css';

type LocationState = { passwordResetToken?: string } | null;

/**
 * Fase H — paso final del primer inicio de sesión. Llega desde LoginPage con el
 * PasswordResetToken en el estado de navegación (nunca en la URL — token de un solo uso, 5
 * minutos de vigencia, no debe quedar en el historial del navegador). Sin ese estado (recarga,
 * URL directa) no hay nada que hacer aquí — vuelve a /login.
 */
export function CompletePasswordResetPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const login = useAuthStore((s) => s.login);
  const { t } = useI18n();

  const token = ((location.state as LocationState)?.passwordResetToken ?? '').trim();
  const missingToken = !token;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CompletePasswordResetFormValues>({
    resolver: zodResolver(completePasswordResetFormSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const onValid = async (form: CompletePasswordResetFormValues) => {
    setError('');
    if (missingToken) return;

    setLoading(true);
    try {
      const payload = await authService.completePasswordReset({
        passwordResetToken: token,
        newPassword: form.newPassword,
      });
      completeLoginNavigation(payload, login, navigate);
    } catch (err: unknown) {
      const fromApi = readApiErrorMessage(err);
      setError(
        fromApi ||
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
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">lock_reset</span>
          </div>
          <h1 className="cpr-brand-name">ZH Technologies</h1>
          <p className="cpr-brand-sub">{t('completePasswordReset.subtitle', 'Nueva contraseña')}</p>
        </header>

        <div className="zh-auth-card">
          <div className="zh-auth-card-header">
            <h2 className="zh-auth-card-title">{t('completePasswordReset.title', 'Establece tu contraseña')}</h2>
            <p className="zh-auth-card-desc">
              {t(
                'completePasswordReset.description',
                'Debes cambiar tu contraseña temporal antes de continuar.',
              )}
            </p>
          </div>

          {missingToken && (
            <ZHPageNotice
              variant="error"
              message={t('common.errorPrefix')}
              detail={t(
                'completePasswordReset.missingToken',
                'No hay un token de restablecimiento activo. Vuelve a iniciar sesión.',
              )}
            />
          )}
          {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

          <form className="zh-auth-form" onSubmit={handleSubmit(onValid)} noValidate>
            <div className={`zh-auth-field${errors.newPassword ? ' zh-auth-field--error' : ''}`}>
              <label className="zh-auth-label" htmlFor="cpr-newPassword">
                {t('reset.newPassword.label')}
              </label>
              <div className="zh-auth-input-wrap">
                <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">lock</span>
                <input
                  className="zh-auth-input"
                  id="cpr-newPassword"
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

            <div className={`zh-auth-field${errors.confirmPassword ? ' zh-auth-field--error' : ''}`}>
              <label className="zh-auth-label" htmlFor="cpr-confirmPassword">
                {t('reset.confirmPassword.label')}
              </label>
              <div className="zh-auth-input-wrap">
                <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">lock_open</span>
                <input
                  className="zh-auth-input"
                  id="cpr-confirmPassword"
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

            <button type="submit" className="zh-auth-submit" disabled={loading || missingToken}>
              <span>
                {loading
                  ? t('reset.button.loading')
                  : t('completePasswordReset.button.submit', 'Guardar y continuar')}
              </span>
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
                  <li>Mínimo 8 caracteres, una mayúscula y un número.</li>
                  <li>Ambos campos deben coincidir exactamente.</li>
                  <li>Este enlace vence en pocos minutos por seguridad.</li>
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
      </div>
    </div>
  );
}
