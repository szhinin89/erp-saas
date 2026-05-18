import { useEffect, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAuthStore } from '../../../../store/authStore';
import { useAsync } from '../../../../hooks/useAsync';
import { sriService } from '../../../../services/sriService';
import { formatApiError } from '../../../lib/formatApiError';
import {
  sriConfigSchema,
  SRI_WSDL_DEFAULTS,
  type SriConfigValues,
} from '../../../../schemas/configuracion/sriConfigSchema';

const ENV_OPTIONS = [
  { value: 2, label: 'Pruebas (2)', description: 'Ambiente de certificación SRI — no genera comprobantes válidos' },
  { value: 1, label: 'Producción (1)', description: 'Ambiente real — los comprobantes tienen validez tributaria' },
];

export function SriConfigPage() {
  const hasPerm = usePermissionsStore((s) => s.has);
  const role    = useAuthStore((s) => s.user?.role ?? '');

  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const canView  = isAdmin || hasPerm('settings.company.view');
  const canEdit  = isAdmin || hasPerm('settings.company.edit');

  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved,     setSaved]     = useState(false);
  const [showPass,  setShowPass]  = useState(false);

  const sriState = useAsync(() => sriService.get(), canView);

  const {
    register,
    handleSubmit,
    reset,
    watch,
    control,
    setValue,
    getValues,
    formState: { errors, isDirty },
  } = useForm<SriConfigValues>({
    resolver: zodResolver(sriConfigSchema),
    defaultValues: {
      ruc:                '',
      legalName:          '',
      tradeName:          '',
      mainAddress:        '',
      requiresAccounting: false,
      specialTaxpayer:    '',
      estabCode:          '001',
      emPointCode:        '001',
      certP12Path:        '',
      certPassword:       '',
      environment:        2,
      emissionType:       1,
      wsdlUrl:            SRI_WSDL_DEFAULTS.pruebas,
    },
  });

  const envValue = watch('environment');

  useEffect(() => {
    const d = sriState.data;
    if (!d) return;
    reset({
      ruc:                d.companyRuc ?? '',
      legalName:          d.legalName ?? '',
      tradeName:          d.tradeName ?? '',
      mainAddress:        d.mainAddress ?? '',
      requiresAccounting: d.requiresAccounting ?? false,
      specialTaxpayer:    d.specialTaxpayer ?? '',
      estabCode:          d.estabCode ?? '001',
      emPointCode:        d.emPointCode ?? '001',
      certP12Path:        d.certificateP12Path ?? '',
      certPassword:       '',
      environment:        d.environment ?? 2,
      emissionType:       d.emissionType ?? 1,
      wsdlUrl:            d.sriAuthorizationUrl ?? SRI_WSDL_DEFAULTS.pruebas,
    });
  }, [sriState.data, reset]);

  /* Autocompletar URL WSDL al cambiar ambiente */
  useEffect(() => {
    const currentUrl = getValues('wsdlUrl');
    const isDefault  = Object.values(SRI_WSDL_DEFAULTS).includes(currentUrl);
    if (isDefault || !currentUrl) {
      setValue(
        'wsdlUrl',
        envValue === 1 ? SRI_WSDL_DEFAULTS.produccion : SRI_WSDL_DEFAULTS.pruebas,
        { shouldDirty: true },
      );
    }
  }, [envValue, getValues, setValue]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await sriService.upsert({
        ruc:                values.ruc,
        legalName:          values.legalName,
        tradeName:          values.tradeName || null,
        mainAddress:        values.mainAddress,
        requiresAccounting: values.requiresAccounting,
        specialTaxpayer:    values.specialTaxpayer || null,
        estabCode:          values.estabCode,
        emPointCode:        values.emPointCode,
        certP12Path:        values.certP12Path,
        certPassword:       values.certPassword ?? '',
        environment:        values.environment,
        emissionType:       1,
        wsdlUrl:            values.wsdlUrl,
      });
      setSaved(true);
      sriState.refetch();
    } catch (err) {
      setSaveError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const d = sriState.data;
    if (d) {
      reset({
        ruc:                d.companyRuc ?? '',
        legalName:          d.legalName ?? '',
        tradeName:          d.tradeName ?? '',
        mainAddress:        d.mainAddress ?? '',
        requiresAccounting: d.requiresAccounting ?? false,
        specialTaxpayer:    d.specialTaxpayer ?? '',
        estabCode:          d.estabCode ?? '001',
        emPointCode:        d.emPointCode ?? '001',
        certP12Path:        d.certificateP12Path ?? '',
        certPassword:       '',
        environment:        d.environment ?? 2,
        emissionType:       d.emissionType ?? 1,
        wsdlUrl:            d.sriAuthorizationUrl ?? SRI_WSDL_DEFAULTS.pruebas,
      });
    }
  };

  if (!canView) return <NoAccessPage title="Configuración SRI" />;
  if (sriState.loading) return <LoadingState />;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">Configuración</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Facturación Electrónica SRI</span>
          </nav>
          <h1 className="pg-title">Configuración SRI</h1>
          <p className="pg-subtitle">
            Parámetros obligatorios para emitir comprobantes electrónicos según la ficha técnica del SRI Ecuador.
          </p>
        </div>
        {sriState.data && (
          <div className="pg-header-right">
            <span className={`badge badge--md ${sriState.data.environment === 1 ? 'badge--green' : 'badge--orange'}`}>
              {sriState.data.environment === 1 ? 'Producción' : 'Pruebas'}
            </span>
          </div>
        )}
      </div>

      {!sriState.data && !sriState.loading && (
        <ZHPageNotice
          variant="warning"
          message="Esta empresa no tiene configuración SRI todavía."
          detail="Completa el formulario para habilitar la facturación electrónica."
        />
      )}
      {sriState.error && (
        <ZHPageNotice variant="error" message="Error al cargar la configuración SRI." detail={sriState.error} />
      )}
      {saveError && (
        <ZHPageNotice variant="error" message="Error al guardar." detail={saveError} />
      )}
      {saved && (
        <ZHPageNotice variant="success" message="Configuración SRI guardada correctamente." />
      )}

      <form onSubmit={onSubmit}>

        {/* ── 1. Datos de la empresa ─────────────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">business</span>
              <p className="pg-section-label">Datos de la Empresa</p>
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

              <ZHField label="Nombre Comercial" error={errors.tradeName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre visible en documentos (opcional)"
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

              <ZHField label="N° Resolución Contribuyente Especial" error={errors.specialTaxpayer?.message}>
                <input
                  className="zh-input mono"
                  placeholder="Ej: 001 — dejar vacío si no aplica"
                  disabled={saving || !canEdit}
                  {...register('specialTaxpayer')}
                />
              </ZHField>

              <ZHField label="Obligado a Llevar Contabilidad">
                <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', paddingTop: 6 }}>
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
                        <span>Sí, esta empresa está obligada a llevar contabilidad</span>
                      </label>
                    )}
                  />
                </div>
              </ZHField>

            </div>
          </div>
        </div>

        {/* ── 2. Ambiente y tipo de emisión ─────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">cloud</span>
              <p className="pg-section-label">Ambiente y Tipo de Emisión</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHPageNotice
              variant="warning"
              message="Inicia siempre en ambiente de Pruebas. Cambia a Producción solo cuando el SRI haya aprobado tu certificado."
            />
            <div style={{ marginTop: 'var(--space-4)', display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
              <Controller
                name="environment"
                control={control}
                render={({ field }) => (
                  <>
                    {ENV_OPTIONS.map((opt) => (
                      <label
                        key={opt.value}
                        style={{
                          display: 'flex',
                          alignItems: 'flex-start',
                          gap: 'var(--space-3)',
                          padding: 'var(--space-3) var(--space-4)',
                          border: `2px solid ${field.value === opt.value ? 'var(--color-primary)' : 'var(--color-border)'}`,
                          borderRadius: 'var(--radius-md)',
                          cursor: canEdit && !saving ? 'pointer' : 'default',
                          background: field.value === opt.value ? 'var(--color-primary-subtle, #f0f4ff)' : 'transparent',
                          transition: 'border-color 0.15s, background 0.15s',
                        }}
                      >
                        <input
                          type="radio"
                          value={opt.value}
                          checked={field.value === opt.value}
                          onChange={() => field.onChange(opt.value)}
                          disabled={saving || !canEdit}
                          style={{ marginTop: 2 }}
                        />
                        <div>
                          <div style={{ fontWeight: 600, fontSize: 14 }}>{opt.label}</div>
                          <div style={{ fontSize: 12, color: 'var(--color-text-secondary)', marginTop: 2 }}>
                            {opt.description}
                          </div>
                        </div>
                      </label>
                    ))}
                  </>
                )}
              />
              {errors.environment && (
                <p style={{ color: 'var(--color-error)', fontSize: 12 }}>{errors.environment.message}</p>
              )}
            </div>
            <div style={{ marginTop: 'var(--space-4)' }}>
              <ZHField label="Tipo de Emisión">
                <input
                  className="zh-input"
                  value="1 — Normal (único tipo permitido por la ficha técnica SRI)"
                  readOnly
                  disabled
                  style={{ color: 'var(--color-text-secondary)' }}
                />
              </ZHField>
            </div>
          </div>
        </div>

        {/* ── 3. Establecimiento y Punto de Emisión ─────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">store</span>
              <p className="pg-section-label">Establecimiento y Punto de Emisión</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHPageNotice
              variant="info"
              message="Estos códigos forman parte de la clave de acceso y el número de comprobante (001-001-000000001)."
            />
            <div className="pg-form-grid pg-form-grid--2" style={{ marginTop: 'var(--space-4)' }}>
              <ZHField
                label="Código de Establecimiento"
                required
                error={errors.estabCode?.message}
              >
                <input
                  className="zh-input mono"
                  placeholder="001"
                  maxLength={3}
                  disabled={saving || !canEdit}
                  {...register('estabCode')}
                />
              </ZHField>

              <ZHField
                label="Código de Punto de Emisión"
                required
                error={errors.emPointCode?.message}
              >
                <input
                  className="zh-input mono"
                  placeholder="001"
                  maxLength={3}
                  disabled={saving || !canEdit}
                  {...register('emPointCode')}
                />
              </ZHField>
            </div>
            {sriState.data && (
              <p style={{ marginTop: 'var(--space-3)', fontSize: 12, color: 'var(--color-text-secondary)' }}>
                Secuencial actual: <strong className="mono">{String(sriState.data.currentSequential).padStart(9, '0')}</strong>
              </p>
            )}
          </div>
        </div>

        {/* ── 4. Certificado Digital ────────────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">verified_user</span>
              <p className="pg-section-label">Certificado Digital (.p12)</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHPageNotice
              variant="info"
              message="El certificado debe ser emitido por el Banco Central del Ecuador o Security Data. La contraseña se almacena cifrada."
            />
            <div className="pg-form-grid pg-form-grid--2" style={{ marginTop: 'var(--space-4)' }}>
              <ZHField
                label="Ruta del Certificado (.p12)"
                required
                error={errors.certP12Path?.message}
              >
                <input
                  className="zh-input mono"
                  placeholder="/certs/empresa.p12"
                  disabled={saving || !canEdit}
                  {...register('certP12Path')}
                />
              </ZHField>

              <ZHField
                label={sriState.data ? 'Nueva Contraseña (dejar vacío para no cambiar)' : 'Contraseña del Certificado'}
                error={errors.certPassword?.message}
              >
                <div style={{ position: 'relative' }}>
                  <input
                    className="zh-input"
                    type={showPass ? 'text' : 'password'}
                    placeholder={sriState.data ? '••••••• (sin cambios)' : 'Contraseña del .p12'}
                    disabled={saving || !canEdit}
                    style={{ paddingRight: 40 }}
                    {...register('certPassword')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPass((p) => !p)}
                    style={{
                      position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)',
                      background: 'none', border: 'none', cursor: 'pointer', color: 'var(--color-text-secondary)',
                    }}
                    tabIndex={-1}
                    aria-label={showPass ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                  >
                    <span className="material-symbols-outlined" style={{ fontSize: 18 }}>
                      {showPass ? 'visibility_off' : 'visibility'}
                    </span>
                  </button>
                </div>
              </ZHField>
            </div>
          </div>
        </div>

        {/* ── 5. URL Webservice SRI ─────────────────────────── */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">api</span>
              <p className="pg-section-label">URL del Webservice SRI</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--1">
              <ZHField
                label="URL de Autorización (WSDL)"
                required
                error={errors.wsdlUrl?.message}
              >
                <input
                  className="zh-input mono"
                  disabled={saving || !canEdit}
                  {...register('wsdlUrl')}
                />
              </ZHField>
            </div>
            <div style={{ marginTop: 'var(--space-3)', display: 'flex', gap: 'var(--space-2)', flexWrap: 'wrap' }}>
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                disabled={saving || !canEdit}
                onClick={() => setValue('wsdlUrl', SRI_WSDL_DEFAULTS.pruebas, { shouldDirty: true })}
              >
                Usar URL Pruebas
              </ZHBtn>
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                disabled={saving || !canEdit}
                onClick={() => setValue('wsdlUrl', SRI_WSDL_DEFAULTS.produccion, { shouldDirty: true })}
              >
                Usar URL Producción
              </ZHBtn>
            </div>
            <div style={{ marginTop: 'var(--space-3)', fontSize: 11, color: 'var(--color-text-secondary)' }}>
              <strong>Pruebas:</strong> <span className="mono">{SRI_WSDL_DEFAULTS.pruebas}</span><br />
              <strong>Producción:</strong> <span className="mono">{SRI_WSDL_DEFAULTS.produccion}</span>
            </div>
          </div>
        </div>

        {/* ── Barra de acciones ─────────────────────────────── */}
        <div className="pg-actions-bar">
          <div className="pg-actions-info">
            <span className="material-symbols-outlined">info</span>
            La contraseña del certificado se almacena cifrada. El secuencial no se resetea al actualizar.
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
              {saving ? 'Guardando…' : 'Guardar Configuración SRI'}
            </ZHBtn>
          </div>
        </div>

      </form>
    </div>
  );
}
