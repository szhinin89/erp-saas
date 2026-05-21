import { Controller, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { SRI_WSDL_DEFAULTS, type SriConfigValues } from '../../../../schemas/configuracion/sriConfigSchema';
import { SRI_ENV_OPTIONS } from './useSriConfigPage';

type SriConfigPageDataTabProps = {
  register: UseFormRegister<SriConfigValues>;
  control: Control<SriConfigValues>;
  errors: FieldErrors<SriConfigValues>;
  saving: boolean;
  canEdit: boolean;
  showPass: boolean;
  setShowPass: (value: boolean | ((prev: boolean) => boolean)) => void;
  hasExistingConfig: boolean;
  currentSequential?: number;
  setWsdlUrl: (url: string) => void;
};

export function SriConfigPageDataTab({
  register,
  control,
  errors,
  saving,
  canEdit,
  showPass,
  setShowPass,
  hasExistingConfig,
  currentSequential,
  setWsdlUrl,
}: SriConfigPageDataTabProps) {
  return (
    <>
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
              <input className="zh-input mono" placeholder="0000000000001" maxLength={13} disabled={saving || !canEdit} {...register('ruc')} />
            </ZHField>
            <ZHField label="Razón Social" required error={errors.legalName?.message}>
              <input className="zh-input" placeholder="Nombre legal registrado en el SRI" disabled={saving || !canEdit} {...register('legalName')} />
            </ZHField>
            <ZHField label="Nombre Comercial" error={errors.tradeName?.message}>
              <input className="zh-input" placeholder="Nombre visible en documentos (opcional)" disabled={saving || !canEdit} {...register('tradeName')} />
            </ZHField>
            <ZHField label="Dirección Matriz" required error={errors.mainAddress?.message}>
              <input className="zh-input" placeholder="Dirección registrada en el SRI" disabled={saving || !canEdit} {...register('mainAddress')} />
            </ZHField>
            <ZHField label="N° Resolución Contribuyente Especial" error={errors.specialTaxpayer?.message}>
              <input className="zh-input mono" placeholder="Ej: 001 — dejar vacío si no aplica" disabled={saving || !canEdit} {...register('specialTaxpayer')} />
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
                  {SRI_ENV_OPTIONS.map((opt) => (
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
                        <div style={{ fontSize: 12, color: 'var(--color-text-secondary)', marginTop: 2 }}>{opt.description}</div>
                      </div>
                    </label>
                  ))}
                </>
              )}
            />
            {errors.environment && <p style={{ color: 'var(--color-error)', fontSize: 12 }}>{errors.environment.message}</p>}
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

      <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">store</span>
            <p className="pg-section-label">Establecimiento y Punto de Emisión</p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHPageNotice variant="info" message="Estos códigos forman parte de la clave de acceso y el número de comprobante (001-001-000000001)." />
          <div className="pg-form-grid pg-form-grid--2" style={{ marginTop: 'var(--space-4)' }}>
            <ZHField label="Código de Establecimiento" required error={errors.estabCode?.message}>
              <input className="zh-input mono" placeholder="001" maxLength={3} disabled={saving || !canEdit} {...register('estabCode')} />
            </ZHField>
            <ZHField label="Código de Punto de Emisión" required error={errors.emPointCode?.message}>
              <input className="zh-input mono" placeholder="001" maxLength={3} disabled={saving || !canEdit} {...register('emPointCode')} />
            </ZHField>
          </div>
          {hasExistingConfig && currentSequential != null && (
            <p style={{ marginTop: 'var(--space-3)', fontSize: 12, color: 'var(--color-text-secondary)' }}>
              Secuencial actual: <strong className="mono">{String(currentSequential).padStart(9, '0')}</strong>
            </p>
          )}
        </div>
      </div>

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
            <ZHField label="Ruta del Certificado (.p12)" required error={errors.certP12Path?.message}>
              <input className="zh-input mono" placeholder="/certs/empresa.p12" disabled={saving || !canEdit} {...register('certP12Path')} />
            </ZHField>
            <ZHField
              label={hasExistingConfig ? 'Nueva Contraseña (dejar vacío para no cambiar)' : 'Contraseña del Certificado'}
              error={errors.certPassword?.message}
            >
              <div style={{ position: 'relative' }}>
                <input
                  className="zh-input"
                  type={showPass ? 'text' : 'password'}
                  placeholder={hasExistingConfig ? '••••••• (sin cambios)' : 'Contraseña del .p12'}
                  disabled={saving || !canEdit}
                  style={{ paddingRight: 40 }}
                  {...register('certPassword')}
                />
                <button
                  type="button"
                  onClick={() => setShowPass((p) => !p)}
                  style={{
                    position: 'absolute',
                    right: 10,
                    top: '50%',
                    transform: 'translateY(-50%)',
                    background: 'none',
                    border: 'none',
                    cursor: 'pointer',
                    color: 'var(--color-text-secondary)',
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

      <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">api</span>
            <p className="pg-section-label">URL del Webservice SRI</p>
          </div>
        </div>
        <div className="pg-section-body">
          <div className="pg-form-grid pg-form-grid--1">
            <ZHField label="URL de Autorización (WSDL)" required error={errors.wsdlUrl?.message}>
              <input className="zh-input mono" disabled={saving || !canEdit} {...register('wsdlUrl')} />
            </ZHField>
          </div>
          <div style={{ marginTop: 'var(--space-3)', display: 'flex', gap: 'var(--space-2)', flexWrap: 'wrap' }}>
            <ZHBtn variant="ghost" size="sm" type="button" disabled={saving || !canEdit} onClick={() => setWsdlUrl(SRI_WSDL_DEFAULTS.pruebas)}>
              Usar URL Pruebas
            </ZHBtn>
            <ZHBtn variant="ghost" size="sm" type="button" disabled={saving || !canEdit} onClick={() => setWsdlUrl(SRI_WSDL_DEFAULTS.produccion)}>
              Usar URL Producción
            </ZHBtn>
          </div>
          <div style={{ marginTop: 'var(--space-3)', fontSize: 11, color: 'var(--color-text-secondary)' }}>
            <strong>Pruebas:</strong> <span className="mono">{SRI_WSDL_DEFAULTS.pruebas}</span>
            <br />
            <strong>Producción:</strong> <span className="mono">{SRI_WSDL_DEFAULTS.produccion}</span>
          </div>
        </div>
      </div>
    </>
  );
}
