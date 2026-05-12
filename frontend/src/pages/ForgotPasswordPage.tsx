import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormActions } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import { forgotPasswordEmailSchema, type ForgotPasswordFormValues } from '../schemas/auth/forgotPasswordSchema';
import { formatApiRequestError, readApiErrorMessage } from '../modules/lib/apiError';
import './LoginPage.css';

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordEmailSchema),
    defaultValues: { email: '' },
  });

  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  const onValid = async (form: ForgotPasswordFormValues) => {
    setError('');
    setSuccess('');
    setLoading(true);
    try {
      await api.post<ApiResponse<object>>('/api/auth/forgot-password', {
        email: form.email.trim().toLowerCase(),
      });
      setSuccess(t('forgot.success'));
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
    <ZHCenteredCard bgClassName="login-bg" cardClassName="login-card">
      <form className="login-form" onSubmit={handleSubmit(onValid)} noValidate>
        <ZHFormHeader title={t('forgot.title')} subtitle={t('forgot.subtitle')} />
        <ZHFormBody>
          {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
          {success ? <ZHPageNotice variant="success" message={success} /> : null}

          <ZHFormSection title={t('forgot.title')}>
            <ZHGrid cols={1}>
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
            </ZHGrid>
          </ZHFormSection>

          <p className="login-form-hint" style={{ marginTop: '0.5rem', fontSize: '0.875rem' }}>
            <Link to="/password-reset">{t('forgot.linkDirectReset')}</Link>
          </p>

          <ZHFormActions
            onCancel={() => navigate('/login')}
            onDraft={undefined}
            onSave={undefined}
            disableDraft
            disableSave={loading}
            saveButtonType="submit"
            labels={{
              cancel: t('tenantSelect.back') ?? t('common.cancel'),
              draft: t('common.saveDraft') ?? 'Guardar borrador',
              save: loading ? t('forgot.button.loading') : t('forgot.button.submit'),
            }}
          />
        </ZHFormBody>
      </form>
    </ZHCenteredCard>
  );
}
