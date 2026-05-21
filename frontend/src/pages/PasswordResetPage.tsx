import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from 'react-router-dom';
import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { passwordResetFormSchema, type PasswordResetFormValues } from '../schemas/auth/passwordResetSchema';
import './PasswordResetPage.css';

export function PasswordResetPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<PasswordResetFormValues>({
    resolver: zodResolver(passwordResetFormSchema),
    defaultValues: { subscriberId: '', email: '', newPassword: '', confirmPassword: '' },
  });

  const subscriberIdWatched = watch('subscriberId');

  const [subscriberAllowsDirectReset, setSubscriberAllowsDirectReset] = useState<boolean | null>(null);
  const [subscriberCheckError, setSubscriberCheckError] = useState<string>('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const raw = subscriberIdWatched.trim();
    if (!raw) {
      setSubscriberCheckError('');
      setSubscriberAllowsDirectReset(null);
      return;
    }

    const isGuid = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(raw);
    if (!isGuid) {
      setSubscriberCheckError('');
      setSubscriberAllowsDirectReset(null);
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        setSubscriberCheckError('');
        const { data } = await api.get<ApiResponse<{ subscriberId: string; passwordResetMode: number }>>(
          `/api/subscribers/${raw}/public-settings`
        );
        if (cancelled) return;
        setSubscriberAllowsDirectReset(data.responseObject.passwordResetMode === 1);
      } catch {
        if (cancelled) return;
        setSubscriberCheckError(t('reset.subscriberCheck.unavailable'));
        setSubscriberAllowsDirectReset(null);
      }
    })();

    return () => { cancelled = true; };
  }, [subscriberIdWatched, t]);

  const onValid = async (form: PasswordResetFormValues) => {
    setError('');
    setSuccess('');

    if (subscriberAllowsDirectReset === false) {
      setError(t('reset.error.disabled'));
      return;
    }
    if (subscriberAllowsDirectReset === null && subscriberCheckError) {
      setError(t('reset.subscriberCheck.unavailable'));
      return;
    }

    setLoading(true);
    try {
      await api.post<ApiResponse<object>>('/api/auth/password-reset', {
        subscriberId: form.subscriberId,
        email: form.email,
        newPassword: form.newPassword,
      });
      setSuccess(t('reset.success'));
      setTimeout(() => navigate('/login'), 800);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('login.error.default');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const subscriberHint =
    subscriberAllowsDirectReset === true
      ? t('reset.subscriberCheck.enabled')
      : subscriberAllowsDirectReset === false
        ? t('reset.error.disabled')
        : subscriberCheckError || undefined;

  const subscriberHintType =
    subscriberAllowsDirectReset === true
      ? 'success'
      : subscriberAllowsDirectReset === false
        ? 'error'
        : subscriberCheckError
          ? 'info'
          : undefined;

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        {/* Marca */}
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">manage_accounts</span>
          </div>
          <h1 className="pr-brand-name">ZH Technologies</h1>
          <p className="pr-brand-sub">Restablecer Contraseña</p>
        </header>

        {/* Card */}
        <div className="zh-auth-card">
          <div className="zh-auth-card-header">
            <h2 className="zh-auth-card-title">{t('reset.title')}</h2>
            <p className="zh-auth-card-desc">{t('reset.directSubtitle')}</p>
          </div>

          {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}
          {success && <ZHPageNotice variant="success" message={success} />}

          <form className="zh-auth-form" onSubmit={handleSubmit(onValid)} noValidate>
            {/* Subscriber ID */}
            <div className={`zh-auth-field${errors.subscriberId ? ' zh-auth-field--error' : ''}`}>
              <label className="zh-auth-label" htmlFor="subscriberId">
                {t('login.subscriberId.label')}
              </label>
              <div className="zh-auth-input-wrap">
                <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">domain</span>
                <input
                  className="zh-auth-input"
                  id="subscriberId"
                  type="text"
                  placeholder={t('login.subscriberId.placeholder')}
                  autoComplete="off"
                  disabled={loading}
                  {...register('subscriberId')}
                />
              </div>
              {errors.subscriberId?.message ? (
                <span className="zh-auth-field-error">{errors.subscriberId.message}</span>
              ) : subscriberHint ? (
                <span className={`zh-auth-field-hint${subscriberHintType ? ` zh-auth-field-hint--${subscriberHintType}` : ''}`}>
                  {subscriberHint}
                </span>
              ) : null}
            </div>

            {/* Email */}
            <div className={`zh-auth-field${errors.email ? ' zh-auth-field--error' : ''}`}>
              <label className="zh-auth-label" htmlFor="email">
                {t('reset.email.label')}
              </label>
              <div className="zh-auth-input-wrap">
                <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">mail</span>
                <input
                  className="zh-auth-input"
                  id="email"
                  type="email"
                  placeholder={t('login.email.placeholder')}
                  autoComplete="username"
                  disabled={loading}
                  {...register('email')}
                />
              </div>
              {errors.email?.message && (
                <span className="zh-auth-field-error">{errors.email.message}</span>
              )}
            </div>

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
                  disabled={loading}
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
                  disabled={loading}
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
              disabled={loading || subscriberAllowsDirectReset === false}
            >
              <span>{loading ? t('reset.button.loading') : t('reset.button.submit')}</span>
              {!loading && (
                <span className="material-symbols-outlined" aria-hidden="true">check_circle</span>
              )}
            </button>
          </form>

          <button type="button" className="zh-auth-back" onClick={() => navigate('/login')}>
            <span className="material-symbols-outlined zh-auth-back-arrow" aria-hidden="true">arrow_back</span>
            Volver al inicio de sesión
          </button>
        </div>

        <div className="zh-auth-banner" aria-hidden="true">
          <span className="material-symbols-outlined zh-auth-banner-icon">admin_panel_settings</span>
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
