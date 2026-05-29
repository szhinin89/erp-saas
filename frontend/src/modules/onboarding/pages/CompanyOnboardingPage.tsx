import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './onboarding.css';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { LoadingState } from '../../../components/PageShell';
import { onboardingService, type OnboardingContextDto } from '../api/onboardingService';
import { formatApiRequestError } from '../../lib/apiError';

// ── Schema ────────────────────────────────────────────────────────────────────

const schema = z.object({
  taxId:       z.string().min(1, 'El RUC es obligatorio').max(20)
                .refine(v => !v.startsWith('TMP-EC-'), 'Ingrese el RUC real, no el provisional'),
  legalName:   z.string().min(1, 'La razón social es obligatoria').max(200),
  tradeName:   z.string().max(200).optional(),
  mainAddress: z.string().min(1, 'La dirección es obligatoria').max(500)
                .refine(v => v.trim() !== '—', 'Ingrese una dirección real'),
  phone:       z.string().max(40).optional(),
  email:       z.string().email('Email inválido').or(z.literal('')).optional(),
});
type FormValues = z.infer<typeof schema>;

// ── Steps ─────────────────────────────────────────────────────────────────────

type Step = 1 | 2 | 3;

// ── Page ──────────────────────────────────────────────────────────────────────

export function CompanyOnboardingPage() {
  const navigate = useNavigate();
  const [step, setStep]       = useState<Step>(1);
  const [ctx, setCtx]         = useState<OnboardingContextDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving]   = useState(false);
  const [error, setError]     = useState<string | null>(null);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { taxId: '', legalName: '', tradeName: '', mainAddress: '', phone: '', email: '' },
  });

  useEffect(() => {
    onboardingService.getContext()
      .then(c => {
        setCtx(c);
        if (c.onboardingCompleted) {
          navigate('/dashboard', { replace: true });
          return;
        }
        // Pre-fill with current company data (possibly provisional)
        form.reset({
          taxId:       c.isProvisionalTaxId ? '' : c.currentTaxId,
          legalName:   c.currentLegalName === c.subscriberName ? '' : c.currentLegalName,
          tradeName:   '',
          mainAddress: c.currentAddress === '—' ? '' : c.currentAddress,
          phone:       c.currentPhone ?? '',
          email:       c.currentEmail ?? '',
        });
      })
      .catch(e => setError(formatApiRequestError(e, { generic: 'Error al cargar el contexto de onboarding.' })))
      .finally(() => setLoading(false));
  }, []);

  const applySubscriberData = () => {
    if (!ctx) return;
    form.setValue('legalName', ctx.subscriberName ?? '', { shouldDirty: true });
    form.setValue('tradeName', ctx.subscriberName ?? '', { shouldDirty: true });
  };

  const onSubmit = form.handleSubmit(async (values) => {
    setError(null);
    setSaving(true);
    try {
      await onboardingService.complete({
        useSubscriberData:   false,
        taxId:               values.taxId,
        legalName:           values.legalName,
        tradeName:           values.tradeName || null,
        mainAddress:         values.mainAddress,
        phone:               values.phone || null,
        email:               values.email || null,
        createDefaultBranch: true,
      });
      setStep(3);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: 'Error al completar el onboarding. Intente nuevamente.' }));
    } finally {
      setSaving(false);
    }
  });

  if (loading) return <LoadingState />;

  // ── Step 3: Success ─────────────────────────────────────────────────────────

  if (step === 3) {
    return (
      <div className="onboarding-shell">
        <div className="onboarding-card">
          <div style={{ textAlign: 'center', padding: '2rem' }}>
            <div style={{ fontSize: 48, marginBottom: 16 }}>✅</div>
            <h2 style={{ marginBottom: 8 }}>¡Empresa configurada!</h2>
            <p style={{ color: 'var(--text-muted)', marginBottom: 24 }}>
              Tu empresa quedó lista. Se crearon la sucursal, bodega y punto de emisión por defecto.
              Puedes comenzar a usar el ERP.
            </p>
            <ZHBtn variant="primary" size="lg" onClick={() => navigate('/dashboard', { replace: true })}>
              Entrar al ERP →
            </ZHBtn>
          </div>
        </div>
      </div>
    );
  }

  // ── Steps 1–2 ───────────────────────────────────────────────────────────────

  return (
    <div className="onboarding-shell">
      <div className="onboarding-card">
        {/* Header */}
        <div className="onboarding-header">
          <div className="onboarding-logo">🏢</div>
          <h1>Configura tu empresa</h1>
          <p>Completa los datos de tu empresa para comenzar a usar el ERP.</p>
          <div className="onboarding-steps">
            <span className={`onboarding-step ${step >= 1 ? 'active' : ''}`}>1 · Datos</span>
            <span className="onboarding-step-arrow">→</span>
            <span className={`onboarding-step ${step >= 2 ? 'active' : ''}`}>2 · Confirmar</span>
            <span className="onboarding-step-arrow">→</span>
            <span className={`onboarding-step ${step >= 3 ? 'active' : ''}`}>3 · Listo</span>
          </div>
        </div>

        {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

        {/* Step 1 — Data form */}
        {step === 1 && (
          <form onSubmit={e => { e.preventDefault(); setStep(2); }}>
            <div className="pg-section-body">

              {ctx?.subscriberName && (
                <div style={{ marginBottom: 16 }}>
                  <ZHBtn variant="ghost" size="sm" type="button" onClick={applySubscriberData}>
                    Usar nombre de la cuenta: «{ctx.subscriberName}»
                  </ZHBtn>
                </div>
              )}

              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="RUC / Identificación" required error={form.formState.errors.taxId?.message}>
                  <input className="zh-input mono" placeholder="0999999999001" {...form.register('taxId')} />
                </ZHField>

                <ZHField label="Razón Social" required error={form.formState.errors.legalName?.message}>
                  <input className="zh-input" placeholder="Empresa XYZ S.A." {...form.register('legalName')} />
                </ZHField>

                <ZHField label="Nombre Comercial" error={form.formState.errors.tradeName?.message}>
                  <input className="zh-input" placeholder="XYZ (opcional)" {...form.register('tradeName')} />
                </ZHField>

                <ZHField label="Dirección Principal" required error={form.formState.errors.mainAddress?.message}>
                  <input className="zh-input" placeholder="Av. Principal 001, Cuenca" {...form.register('mainAddress')} />
                </ZHField>

                <ZHField label="Teléfono" error={form.formState.errors.phone?.message}>
                  <input className="zh-input" placeholder="0999 999 999" {...form.register('phone')} />
                </ZHField>

                <ZHField label="Email de contacto" error={form.formState.errors.email?.message}>
                  <input className="zh-input" type="email" placeholder="info@empresa.com" {...form.register('email')} />
                </ZHField>
              </div>

              <ZHPageNotice
                variant="info"
                message="Se creará automáticamente: Sucursal Principal, Bodega Principal y Punto de Emisión 001."
              />
            </div>

            <div className="pg-actions-bar">
              <ZHBtn variant="primary" size="md" type="submit">
                Continuar →
              </ZHBtn>
            </div>
          </form>
        )}

        {/* Step 2 — Confirmation */}
        {step === 2 && (
          <div>
            <div className="pg-section-body">
              <h3 style={{ marginBottom: 12 }}>Confirma los datos</h3>
              <table className="table">
                <tbody>
                  <tr><th>RUC</th><td className="mono">{form.getValues('taxId')}</td></tr>
                  <tr><th>Razón Social</th><td>{form.getValues('legalName')}</td></tr>
                  {form.getValues('tradeName') && <tr><th>Nombre Comercial</th><td>{form.getValues('tradeName')}</td></tr>}
                  <tr><th>Dirección</th><td>{form.getValues('mainAddress')}</td></tr>
                  {form.getValues('phone') && <tr><th>Teléfono</th><td>{form.getValues('phone')}</td></tr>}
                  {form.getValues('email') && <tr><th>Email</th><td>{form.getValues('email')}</td></tr>}
                </tbody>
              </table>

              <ZHPageNotice
                variant="warning"
                message="Al confirmar se creará la infraestructura ERP. Esta operación no se puede deshacer fácilmente."
              />
            </div>

            <div className="pg-actions-bar">
              <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={() => setStep(1)}>
                ← Editar
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={onSubmit}>
                {saving ? 'Configurando empresa…' : '✓ Confirmar y activar'}
              </ZHBtn>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
