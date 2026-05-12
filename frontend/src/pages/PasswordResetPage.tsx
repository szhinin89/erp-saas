import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormActions } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import { passwordResetFormSchema, type PasswordResetFormValues } from '../schemas/auth/passwordResetSchema';
import './LoginPage.css';

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
    defaultValues: {
      tenantId: '',
      email: '',
      newPassword: '',
      confirmPassword: '',
    },
  });

  const tenantIdWatched = watch('tenantId');

  const [tenantAllowsDirectReset, setTenantAllowsDirectReset] = useState<boolean | null>(null);
  const [tenantCheckError, setTenantCheckError] = useState<string>('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const raw = tenantIdWatched.trim();
    if (!raw) {
      setTenantCheckError('');
      setTenantAllowsDirectReset(null);
      return;
    }

    const isGuid = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(raw);
    if (!isGuid) {
      setTenantCheckError('');
      setTenantAllowsDirectReset(null);
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        setTenantCheckError('');
        const { data } = await api.get<ApiResponse<{ tenantId: string; passwordResetMode: number }>>(
          `/api/tenants/${raw}/public-settings`
        );
        if (cancelled) return;
        setTenantAllowsDirectReset(data.responseObject.passwordResetMode === 1);
      } catch {
        if (cancelled) return;
        setTenantCheckError(t('reset.tenantCheck.unavailable'));
        setTenantAllowsDirectReset(null);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [tenantIdWatched, t]);

  const onValid = async (form: PasswordResetFormValues) => {
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

    setLoading(true);
    try {
      await api.post<ApiResponse<object>>('/api/auth/password-reset', {
        tenantId: form.tenantId,
        email: form.email,
        newPassword: form.newPassword,
      });
      setSuccess(t('reset.success'));
      setTimeout(() => navigate('/login'), 800);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('login.error.default');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const tenantHint =
    tenantAllowsDirectReset === true
      ? t('reset.tenantCheck.enabled')
      : tenantAllowsDirectReset === false
        ? t('reset.error.disabled')
        : tenantCheckError || undefined;
  const tenantHintType =
    tenantAllowsDirectReset === true ? 'success' : tenantAllowsDirectReset === false ? 'error' : tenantCheckError ? 'info' : undefined;

  return (
    <ZHCenteredCard bgClassName="login-bg" cardClassName="login-card">
      <form className="login-form" onSubmit={handleSubmit(onValid)} noValidate>
        <ZHFormHeader title={t('reset.title')} subtitle={t('reset.directSubtitle')} />
        <ZHFormBody>
          <p className="login-form-hint" style={{ marginBottom: '0.75rem', fontSize: '0.875rem' }}>
            <Link to="/forgot-password">{t('login.forgotPassword')}</Link>
          </p>
          {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
          {success ? <ZHPageNotice variant="success" message={success} /> : null}

          <ZHFormSection title={t('reset.title')}>
            <ZHGrid cols={1}>
              <ZHField
                label={t('login.tenantId.label')}
                required
                fieldError={errors.tenantId?.message}
                hint={errors.tenantId ? undefined : tenantHint}
                hintType={errors.tenantId ? undefined : tenantHintType}
              >
                <input
                  id="tenantId"
                  type="text"
                  placeholder={t('login.tenantId.placeholder')}
                  autoComplete="off"
                  disabled={loading}
                  {...register('tenantId')}
                />
              </ZHField>

              <ZHField label={t('reset.email.label')} required fieldError={errors.email?.message}>
                <input
                  id="email"
                  type="email"
                  placeholder={t('login.email.placeholder')}
                  autoComplete="username"
                  disabled={loading}
                  {...register('email')}
                />
              </ZHField>

              <ZHField label={t('reset.newPassword.label')} required fieldError={errors.newPassword?.message}>
                <input
                  id="newPassword"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="new-password"
                  disabled={loading}
                  {...register('newPassword')}
                />
              </ZHField>

              <ZHField label={t('reset.confirmPassword.label')} required fieldError={errors.confirmPassword?.message}>
                <input
                  id="confirmPassword"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="new-password"
                  disabled={loading}
                  {...register('confirmPassword')}
                />
              </ZHField>
            </ZHGrid>
          </ZHFormSection>

          <ZHFormActions
            onCancel={() => navigate('/login')}
            onDraft={undefined}
            onSave={undefined}
            disableDraft
            disableSave={loading || tenantAllowsDirectReset === false}
            saveButtonType="submit"
            labels={{
              cancel: t('tenantSelect.back') ?? t('common.cancel'),
              draft: t('common.saveDraft') ?? 'Guardar borrador',
              save: loading ? t('reset.button.loading') : t('reset.button.submit'),
            }}
          />
        </ZHFormBody>
      </form>
    </ZHCenteredCard>
  );
}
