import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import type { ApiResponse } from '../types/api';
import { useI18n } from '../i18n/i18n';
import { ZHFormHeader, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHFormActions } from '../components/zh/ZHForm';
import { ZHCenteredCard } from '../components/zh/ZHCenteredCard';
import './LoginPage.css';

type PasswordResetRequest = {
  tenantId: string;
  email: string;
  newPassword: string;
};

export function PasswordResetPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const [tenantAllowsDirectReset, setTenantAllowsDirectReset] = useState<boolean | null>(null);
  const [tenantCheckError, setTenantCheckError] = useState<string>('');
  const [form, setForm] = useState<PasswordResetRequest>({
    tenantId: '',
    email: '',
    newPassword: '',
  });
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const raw = form.tenantId.trim();
    if (!raw) {
      Promise.resolve().then(() => setTenantCheckError(''));
      Promise.resolve().then(() => setTenantAllowsDirectReset(null));
      return;
    }

    // Only check when it looks like a GUID to avoid noisy calls.
    const isGuid = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(raw);
    if (!isGuid) {
      Promise.resolve().then(() => setTenantCheckError(''));
      Promise.resolve().then(() => setTenantAllowsDirectReset(null));
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        Promise.resolve().then(() => setTenantCheckError(''));
        const { data } = await api.get<ApiResponse<{ tenantId: string; passwordResetMode: number }>>(
          `/api/tenants/${raw}/public-settings`
        );
        if (cancelled) return;
        Promise.resolve().then(() => setTenantAllowsDirectReset(data.responseObject.passwordResetMode === 1));
      } catch {
        if (cancelled) return;
        // If we can't verify (network / env), do not hard-block the flow.
        Promise.resolve().then(() => setTenantCheckError(t('reset.tenantCheck.unavailable')));
        Promise.resolve().then(() => setTenantAllowsDirectReset(null));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [form.tenantId, t]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
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

    if (form.newPassword !== confirmPassword) {
      setError(t('reset.error.mismatch'));
      return;
    }

    setLoading(true);
    try {
      await api.post<ApiResponse<object>>('/api/auth/password-reset', form);
      setSuccess(t('reset.success'));
      setTimeout(() => navigate('/login'), 800);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })
          ?.response?.data?.message ?? t('login.error.default');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <ZHCenteredCard bgClassName="login-bg" cardClassName="login-card">
      <form className="login-form" onSubmit={handleSubmit}>
          <ZHFormHeader title={t('reset.title')} subtitle={t('reset.subtitle')} />
          <ZHFormBody>
            {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}
            {success ? <ZHFormAlert type="success" message={success} /> : null}

            <ZHFormSection title={t('reset.title')}>
              <ZHGrid cols={1}>
                <ZHField
                  label={t('login.tenantId.label')}
                  required
                  hint={
                    tenantAllowsDirectReset === true
                      ? t('reset.tenantCheck.enabled')
                      : tenantAllowsDirectReset === false
                        ? t('reset.error.disabled')
                        : tenantCheckError || undefined
                  }
                  hintType={
                    tenantAllowsDirectReset === true ? 'success' : tenantAllowsDirectReset === false ? 'error' : tenantCheckError ? 'info' : undefined
                  }
                >
                  <input
                    id="tenantId"
                    type="text"
                    placeholder={t('login.tenantId.placeholder')}
                    value={form.tenantId}
                    onChange={(e) => setForm((f) => ({ ...f, tenantId: e.target.value }))}
                    required
                    autoComplete="off"
                    disabled={loading}
                  />
                </ZHField>

                <ZHField label={t('reset.email.label')} required>
                  <input
                    id="email"
                    type="email"
                    placeholder={t('login.email.placeholder')}
                    value={form.email}
                    onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                    required
                    autoComplete="username"
                    disabled={loading}
                  />
                </ZHField>

                <ZHField label={t('reset.newPassword.label')} required>
                  <input
                    id="newPassword"
                    type="password"
                    placeholder={t('login.password.placeholder')}
                    value={form.newPassword}
                    onChange={(e) => setForm((f) => ({ ...f, newPassword: e.target.value }))}
                    required
                    autoComplete="new-password"
                    disabled={loading}
                  />
                </ZHField>

                <ZHField label={t('reset.confirmPassword.label')} required>
                  <input
                    id="confirmPassword"
                    type="password"
                    placeholder={t('login.password.placeholder')}
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                    autoComplete="new-password"
                    disabled={loading}
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

