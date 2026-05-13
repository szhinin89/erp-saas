import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormActions } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import { resetWithTokenFormSchema, type ResetWithTokenFormValues } from '../schemas/auth/resetWithTokenSchema';
import { formatApiRequestError, readApiErrorMessage } from '../modules/lib/apiError';
import './LoginPage.css';

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
    <ZHCenteredCard bgClassName="login-bg" cardClassName="login-card">
      <form className="login-form" onSubmit={handleSubmit(onValid)} noValidate>
        <ZHFormHeader title={t('resetWithToken.title')} subtitle={t('resetWithToken.subtitle')} />
        <ZHFormBody>
          {missingToken ? (
            <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={t('resetWithToken.missingToken')} />
          ) : null}
          {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
          {success ? <ZHPageNotice variant="success" message={success} /> : null}

          <ZHFormSection title={t('resetWithToken.title')}>
            <ZHGrid cols={1}>
              <ZHField label={t('reset.newPassword.label')} required fieldError={errors.newPassword?.message}>
                <input
                  id="newPassword"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="new-password"
                  disabled={loading || missingToken}
                  {...register('newPassword')}
                />
              </ZHField>

              <ZHField label={t('reset.confirmPassword.label')} required fieldError={errors.confirmPassword?.message}>
                <input
                  id="confirmPassword"
                  type="password"
                  placeholder={t('login.password.placeholder')}
                  autoComplete="new-password"
                  disabled={loading || missingToken}
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
            disableSave={loading || missingToken}
            saveButtonType="submit"
            labels={{
              cancel: t('tenantSelect.back') ?? t('common.cancel'),
              draft: t('common.saveDraft') ?? 'Guardar borrador',
              save: loading ? t('reset.button.loading') : t('resetWithToken.button.submit'),
            }}
          />
        </ZHFormBody>
      </form>
    </ZHCenteredCard>
  );
}
