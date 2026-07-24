import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { apiPost, apiGet } from '../modules/lib/apiEnvelope';
import { formatApiRequestError } from '../modules/lib/apiError';
import '../modules/auth/pages/LoginPage.css';

const setupSchema = z.object({
  username: z
    .string()
    .min(3, 'Mínimo 3 caracteres')
    .regex(/^[a-zA-Z0-9][a-zA-Z0-9._-]{1,48}[a-zA-Z0-9]$/, 'Solo letras, números, punto, guion o guion bajo'),
  firstName: z.string().min(1, 'Requerido'),
  lastName: z.string().min(1, 'Requerido'),
  email: z.union([z.string().email('Email inválido'), z.literal('')]).optional(),
  password: z
    .string()
    .min(8, 'Mínimo 8 caracteres')
    .regex(/[A-Z]/, 'Debe contener al menos una mayúscula')
    .regex(/[0-9]/, 'Debe contener al menos un número'),
  setupToken: z.string().min(1, 'Token requerido'),
});

type SetupFormValues = z.infer<typeof setupSchema>;

type SetupStatus = { isInitialized: boolean; adminEmail?: string | null };

export function SetupPage() {
  const navigate = useNavigate();
  const [checking, setChecking] = useState(true);
  const [alreadyDone, setAlreadyDone] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SetupFormValues>({
    resolver: zodResolver(setupSchema),
    defaultValues: { username: '', firstName: '', lastName: '', email: '', password: '', setupToken: '' },
  });

  useState(() => {
    void apiGet<SetupStatus>('/api/v1/setup/status')
      .then((status) => {
        if (status?.isInitialized) {
          setAlreadyDone(true);
        }
      })
      .catch(() => {/* ignore — show form anyway */})
      .finally(() => setChecking(false));
  });

  const onValid = async (form: SetupFormValues) => {
    setError('');
    try {
      await apiPost('/api/v1/setup/admin', {
        username: form.username.trim().toLowerCase(),
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email?.trim() ? form.email.trim().toLowerCase() : null,
        password: form.password,
        setupToken: form.setupToken.trim(),
      });
      setSuccess(true);
      setTimeout(() => navigate('/login', { replace: true }), 2000);
    } catch (err: unknown) {
      setError(
        formatApiRequestError(err, {
          offline: 'No se puede conectar con el servidor.',
          generic: 'Error al crear el administrador.',
        }),
      );
    }
  };

  if (checking) {
    return (
      <div className="zh-auth-bg">
        <div className="zh-auth-wrapper" style={{ textAlign: 'center', paddingTop: 80 }}>
          Verificando estado del sistema…
        </div>
      </div>
    );
  }

  if (alreadyDone) {
    return (
      <div className="zh-auth-bg">
        <div className="zh-auth-wrapper">
          <div className="lp-brand">
            <h1 className="lp-brand-name">ZH Technologies</h1>
            <p className="lp-brand-sub">El sistema ya fue configurado.</p>
          </div>
          <div className="lp-card">
            <div className="lp-card-body">
              <p style={{ textAlign: 'center' }}>
                El administrador inicial ya existe.{' '}
                <button
                  type="button"
                  className="lp-forgot"
                  onClick={() => navigate('/login', { replace: true })}
                >
                  Ir al login
                </button>
              </p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (success) {
    return (
      <div className="zh-auth-bg">
        <div className="zh-auth-wrapper">
          <div className="lp-brand">
            <h1 className="lp-brand-name">ZH Technologies</h1>
            <p className="lp-brand-sub">Configuración completada</p>
          </div>
          <div className="lp-card">
            <div className="lp-card-body" style={{ textAlign: 'center' }}>
              Administrador creado correctamente. Redirigiendo al login…
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        <div className="lp-brand">
          <div className="lp-brand-row">
            <div className="lp-brand-icon" aria-hidden="true">
              <span className="material-symbols-outlined">settings</span>
            </div>
            <h1 className="lp-brand-name" data-testid="setup-title">ZH Technologies</h1>
          </div>
          <p className="lp-brand-sub">Configuración inicial del sistema</p>
        </div>

        <div className="lp-card">
          <div className="lp-card-body">
            {error && (
              <div className="lp-error" role="alert">
                <span className="material-symbols-outlined" aria-hidden="true">error</span>
                <span>{error}</span>
              </div>
            )}

            <form className="lp-form" onSubmit={handleSubmit(onValid)} noValidate>
              <div className={`zh-auth-field${errors.username ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="setup-username">Usuario</label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">badge</span>
                  <input className="zh-auth-input" id="setup-username" type="text" placeholder="administrador" autoComplete="username" {...register('username')} />
                </div>
                {errors.username?.message && <span className="zh-auth-field-error" role="alert">{errors.username.message}</span>}
              </div>

              <div className={`zh-auth-field${errors.firstName ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="setup-first">Nombre</label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">person</span>
                  <input className="zh-auth-input" id="setup-first" type="text" placeholder="Nombre" {...register('firstName')} />
                </div>
                {errors.firstName?.message && <span className="zh-auth-field-error" role="alert">{errors.firstName.message}</span>}
              </div>

              <div className={`zh-auth-field${errors.lastName ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="setup-last">Apellido</label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">person</span>
                  <input className="zh-auth-input" id="setup-last" type="text" placeholder="Apellido" {...register('lastName')} />
                </div>
                {errors.lastName?.message && <span className="zh-auth-field-error" role="alert">{errors.lastName.message}</span>}
              </div>

              <div className={`zh-auth-field${errors.email ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="setup-email">Email (opcional)</label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">mail</span>
                  <input className="zh-auth-input" id="setup-email" type="email" placeholder="admin@empresa.com" autoComplete="email" {...register('email')} />
                </div>
                {errors.email?.message && <span className="zh-auth-field-error" role="alert">{errors.email.message}</span>}
              </div>

              <div className={`zh-auth-field${errors.password ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="setup-password">Contraseña</label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">lock</span>
                  <input className="zh-auth-input" id="setup-password" type="password" placeholder="Mínimo 8 caracteres, 1 mayúscula, 1 número" autoComplete="new-password" {...register('password')} />
                </div>
                {errors.password?.message && <span className="zh-auth-field-error" role="alert">{errors.password.message}</span>}
              </div>

              <div className={`zh-auth-field${errors.setupToken ? ' zh-auth-field--error' : ''}`}>
                <label className="zh-auth-label" htmlFor="setup-token">Token de configuración</label>
                <div className="zh-auth-input-wrap">
                  <span className="zh-auth-input-icon material-symbols-outlined" aria-hidden="true">key</span>
                  <input className="zh-auth-input" id="setup-token" type="password" placeholder="Token de appsettings.json" {...register('setupToken')} />
                </div>
                {errors.setupToken?.message && <span className="zh-auth-field-error" role="alert">{errors.setupToken.message}</span>}
              </div>

              <button
                type="submit"
                className={`lp-submit${isSubmitting ? ' lp-submit--loading' : ''}`}
                disabled={isSubmitting}
              >
                {isSubmitting ? (
                  <>
                    <span className="lp-submit-spinner" aria-hidden="true" />
                    <span>Creando administrador…</span>
                  </>
                ) : (
                  <>
                    <span>Crear administrador inicial</span>
                    <span className="material-symbols-outlined" aria-hidden="true">arrow_forward</span>
                  </>
                )}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}
