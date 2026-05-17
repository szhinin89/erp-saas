import { useEffect, useRef, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAuthStore } from '../../../../store/authStore';
import { useAsync } from '../../../../hooks/useAsync';
import { billingSettingsService } from '../../../../services/billingSettingsService';
import { formatApiError } from '../../../lib/formatApiError';
import {
  billingSettingsSchema,
  type BillingSettingsValues,
} from '../../../../schemas/configuracion/billingSettingsSchema';

const RECEIPT_WIDTHS = [
  { value: 80, label: '80 mm (estándar)' },
  { value: 58, label: '58 mm (compacto)' },
];

export function BillingSettingsPage() {
  const hasPerm = usePermissionsStore((s) => s.has);
  const role    = useAuthStore((s) => s.user?.role ?? '');

  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const canView  = isAdmin || hasPerm('ventas.configuracion.view');
  const canEdit  = isAdmin || hasPerm('ventas.configuracion.edit');

  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved,     setSaved]     = useState(false);
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const settingsState = useAsync(() => billingSettingsService.get(), canView);

  const {
    register,
    handleSubmit,
    reset,
    control,
    setValue,
    formState: { errors, isDirty },
  } = useForm<BillingSettingsValues>({
    resolver: zodResolver(billingSettingsSchema),
    defaultValues: {
      legalName:          '',
      tradeName:          '',
      ruc:                '',
      mainAddress:        '',
      phone:              '',
      email:              '',
      requiresAccounting: false,
      specialTaxpayer:    '',
      footerText:         '',
      receiptWidth:       80,
      logoBase64:         null,
    },
  });

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    reset({
      legalName:          d.legalName ?? '',
      tradeName:          d.tradeName ?? '',
      ruc:                d.ruc ?? '',
      mainAddress:        d.mainAddress ?? '',
      phone:              d.phone ?? '',
      email:              d.email ?? '',
      requiresAccounting: d.requiresAccounting ?? false,
      specialTaxpayer:    d.specialTaxpayer ?? '',
      footerText:         d.additionalNote ?? '',
      receiptWidth:       d.receiptWidth ?? 80,
      logoBase64:         d.logoBase64 ?? null,
    });
    setLogoPreview(d.logoBase64 ?? null);
  }, [settingsState.data, reset]);

  const handleLogoFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (file.size > 200 * 1024) {
      alert('El logo no debe superar 200 KB.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      const b64 = reader.result as string;
      setValue('logoBase64', b64, { shouldDirty: true });
      setLogoPreview(b64);
    };
    reader.readAsDataURL(file);
  };

  const handleRemoveLogo = () => {
    setValue('logoBase64', null, { shouldDirty: true });
    setLogoPreview(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await billingSettingsService.upsert({
        legalName:          values.legalName,
        tradeName:          values.tradeName,
        ruc:                values.ruc,
        mainAddress:        values.mainAddress,
        phone:              values.phone,
        email:              values.email || null,
        requiresAccounting: values.requiresAccounting,
        specialTaxpayer:    values.specialTaxpayer || null,
        logoBase64:         values.logoBase64 ?? null,
        footerText:         values.footerText || null,
        receiptWidth:       values.receiptWidth,
      });
      setSaved(true);
      settingsState.refetch();
    } catch (err) {
      setSaveError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const d = settingsState.data;
    if (d) {
      reset({
        legalName:          d.legalName ?? '',
        tradeName:          d.tradeName ?? '',
        ruc:                d.ruc ?? '',
        mainAddress:        d.mainAddress ?? '',
        phone:              d.phone ?? '',
        email:              d.email ?? '',
        requiresAccounting: d.requiresAccounting ?? false,
        specialTaxpayer:    d.specialTaxpayer ?? '',
        footerText:         d.additionalNote ?? '',
        receiptWidth:       d.receiptWidth ?? 80,
        logoBase64:         d.logoBase64 ?? null,
      });
      setLogoPreview(d.logoBase64 ?? null);
    }
  };

  if (!canView) return <NoAccessPage title="Configuración RIDE / Facturación" />;
  if (settingsState.loading) return <LoadingState />;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">Configuración</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Representación Impresa (RIDE)</span>
          </nav>
          <h1 className="pg-title">Configuración RIDE</h1>
          <p className="pg-subtitle">
            Datos que aparecen en la representación impresa de comprobantes electrónicos (facturas, notas de crédito, retenciones).
          </p>
        </div>
      </div>

      {saveError && (
        <ZHPageNotice variant="error" message="Error al guardar." detail={saveError} />
      )}
      {saved && (
        <ZHPageNotice variant="success" message="Configuración RIDE guardada correctamente." />
      )}

      <form onSubmit={onSubmit}>

        {/* ── 1. Datos del emisor ───────────────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">badge</span>
              <p className="pg-section-label">Datos del Emisor</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="RUC" required error={errors.ruc?.message}>
                <input
                  className="zh-input mono"
                  placeholder="0000000000001"
                  maxLength={13}
                  disabled={saving || !canEdit}
                  {...register('ruc')}
                />
              </ZHField>

              <ZHField label="Razón Social" required error={errors.legalName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre legal registrado en el SRI"
                  disabled={saving || !canEdit}
                  {...register('legalName')}
                />
              </ZHField>

              <ZHField label="Nombre Comercial" required error={errors.tradeName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre visible en documentos"
                  disabled={saving || !canEdit}
                  {...register('tradeName')}
                />
              </ZHField>

              <ZHField label="Dirección Matriz" required error={errors.mainAddress?.message}>
                <input
                  className="zh-input"
                  placeholder="Dirección registrada en el SRI"
                  disabled={saving || !canEdit}
                  {...register('mainAddress')}
                />
              </ZHField>

              <ZHField label="Teléfono" required error={errors.phone?.message}>
                <input
                  className="zh-input"
                  placeholder="+593 2 000-0000"
                  disabled={saving || !canEdit}
                  {...register('phone')}
                />
              </ZHField>

              <ZHField label="Correo Electrónico" error={errors.email?.message}>
                <input
                  className="zh-input"
                  type="email"
                  placeholder="contacto@empresa.com"
                  disabled={saving || !canEdit}
                  {...register('email')}
                />
              </ZHField>

              <ZHField label="N° Resolución Contribuyente Especial" error={errors.specialTaxpayer?.message}>
                <input
                  className="zh-input mono"
                  placeholder="Dejar vacío si no aplica"
                  disabled={saving || !canEdit}
                  {...register('specialTaxpayer')}
                />
              </ZHField>

              <ZHField label="Obligado a Llevar Contabilidad">
                <div style={{ display: 'flex', alignItems: 'center', paddingTop: 6 }}>
                  <Controller
                    name="requiresAccounting"
                    control={control}
                    render={({ field }) => (
                      <label className="zh-inline-check">
                        <input
                          type="checkbox"
                          checked={field.value}
                          onChange={(e) => field.onChange(e.target.checked)}
                          disabled={saving || !canEdit}
                        />
                        <span>Sí, obligado a llevar contabilidad</span>
                      </label>
                    )}
                  />
                </div>
              </ZHField>
            </div>
          </div>
        </div>

        {/* ── 2. Logo ───────────────────────────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">image</span>
              <p className="pg-section-label">Logotipo</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHPageNotice
              variant="info"
              message="Formato PNG o JPG, máximo 200 KB. Se incrusta en Base64 en el RIDE."
            />
            <div style={{ marginTop: 'var(--space-4)', display: 'flex', gap: 'var(--space-6)', alignItems: 'flex-start', flexWrap: 'wrap' }}>
              {logoPreview ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', alignItems: 'center' }}>
                  <img
                    src={logoPreview}
                    alt="Logo empresa"
                    style={{ maxWidth: 200, maxHeight: 100, objectFit: 'contain', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', padding: 8, background: '#fff' }}
                  />
                  {canEdit && (
                    <ZHBtn variant="ghost" size="sm" type="button" onClick={handleRemoveLogo} disabled={saving}>
                      <span className="material-symbols-outlined" style={{ fontSize: 16 }}>delete</span>
                      Quitar logo
                    </ZHBtn>
                  )}
                </div>
              ) : (
                <div style={{
                  width: 200, height: 100, border: '2px dashed var(--color-border)',
                  borderRadius: 'var(--radius-md)', display: 'flex', alignItems: 'center',
                  justifyContent: 'center', color: 'var(--color-text-secondary)', fontSize: 13,
                }}>
                  Sin logotipo
                </div>
              )}
              {canEdit && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/png,image/jpeg"
                    style={{ display: 'none' }}
                    onChange={handleLogoFile}
                  />
                  <ZHBtn
                    variant="ghost"
                    size="md"
                    type="button"
                    disabled={saving}
                    onClick={() => fileInputRef.current?.click()}
                  >
                    <span className="material-symbols-outlined">upload</span>
                    {logoPreview ? 'Cambiar logo' : 'Subir logo'}
                  </ZHBtn>
                  <p style={{ fontSize: 11, color: 'var(--color-text-secondary)' }}>PNG / JPG · máx. 200 KB</p>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* ── 3. Configuración de tirilla ───────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">receipt</span>
              <p className="pg-section-label">Tirilla / Nota al Pie</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Ancho de Tirilla Térmica" error={errors.receiptWidth?.message}>
                <Controller
                  name="receiptWidth"
                  control={control}
                  render={({ field }) => (
                    <select
                      className="zh-input"
                      value={field.value}
                      onChange={(e) => field.onChange(Number(e.target.value))}
                      disabled={saving || !canEdit}
                    >
                      {RECEIPT_WIDTHS.map((w) => (
                        <option key={w.value} value={w.value}>{w.label}</option>
                      ))}
                    </select>
                  )}
                />
              </ZHField>
            </div>

            <ZHField label="Leyenda / Nota al pie" error={errors.footerText?.message} style={{ marginTop: 'var(--space-4)' }}>
              <textarea
                className="zh-input"
                rows={3}
                placeholder="Texto adicional que aparece al final del comprobante impreso (opcional)"
                disabled={saving || !canEdit}
                style={{ resize: 'vertical', minHeight: 72 }}
                {...register('footerText')}
              />
            </ZHField>
          </div>
        </div>

        {/* ── Barra de acciones ─────────────────────────────── */}
        <div className="pg-actions-bar">
          <div className="pg-actions-info">
            <span className="material-symbols-outlined">info</span>
            El logo se almacena en Base64. Para mejor rendimiento usa imágenes menores a 100 KB.
          </div>
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={saving || !isDirty}
              onClick={handleDiscard}
            >
              Descartar
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={saving || !canEdit || !isDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {saving ? 'Guardando…' : 'Guardar RIDE'}
            </ZHBtn>
          </div>
        </div>

      </form>
    </div>
  );
}
