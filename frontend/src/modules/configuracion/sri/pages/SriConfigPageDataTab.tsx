import { Controller, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { SRI_WSDL_DEFAULTS, type SriConfigValues } from '../../../../schemas/configuracion/sriConfigSchema';
import { SRI_ENV_OPTIONS } from './useSriConfigPage';
import './sri-config-page.css';

type SriConfigPageDataTabProps = {
  register: UseFormRegister<SriConfigValues>;
  control: Control<SriConfigValues>;
  errors: FieldErrors<SriConfigValues>;
  saving: boolean;
  canEdit: boolean;
  showPass: boolean;
  setShowPass: (value: boolean | ((prev: boolean) => boolean)) => void;
  hasExistingConfig: boolean;
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
  setWsdlUrl,
}: SriConfigPageDataTabProps) {
  return (
    <>
      <div className="pg-section sri-section-mb">
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
              <div className="sri-check-row">
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

      <div className="pg-section sri-section-mb">
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
          <div className="sri-env-stack">
            <Controller
              name="environment"
              control={control}
              render={({ field }) => (
                <>
                  {SRI_ENV_OPTIONS.map((opt) => (
                    <label
                      key={opt.value}
                      className={`sri-env-option ${field.value === opt.value ? 'sri-env-option--selected' : ''}`}
                    >
                      <input
                        type="radio"
                        value={opt.value}
                        checked={field.value === opt.value}
                        onChange={() => field.onChange(opt.value)}
                        disabled={saving || !canEdit}
                      />
                      <div>
                        <div className="sri-env-option-title">{opt.label}</div>
                        <div className="sri-env-option-desc">{opt.description}</div>
                      </div>
                    </label>
                  ))}
                </>
              )}
            />
            {errors.environment && <p className="sri-field-error">{errors.environment.message}</p>}
          </div>
          <div className="sri-block-mt">
            <ZHField label="Tipo de Emisión">
              <input
                className="zh-input sri-input-readonly-muted"
                value="1 — Normal (único tipo permitido por la ficha técnica SRI)"
                readOnly
                disabled
              />
            </ZHField>
          </div>
        </div>
      </div>

      <div className="pg-section sri-section-mb">
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
          <div className="pg-form-grid pg-form-grid--2 sri-form-grid-mt">
            <ZHField label="Ruta del Certificado (.p12)" required error={errors.certP12Path?.message}>
              <input className="zh-input mono" placeholder="/certs/empresa.p12" disabled={saving || !canEdit} {...register('certP12Path')} />
            </ZHField>
            <ZHField
              label={hasExistingConfig ? 'Nueva Contraseña (dejar vacío para no cambiar)' : 'Contraseña del Certificado'}
              error={errors.certPassword?.message}
            >
              <div className="sri-pass-wrap">
                <input
                  className="zh-input sri-pass-input"
                  type={showPass ? 'text' : 'password'}
                  placeholder={hasExistingConfig ? '••••••• (sin cambios)' : 'Contraseña del .p12'}
                  disabled={saving || !canEdit}
                  {...register('certPassword')}
                />
                <button
                  type="button"
                  className="sri-pass-toggle"
                  onClick={() => setShowPass((p) => !p)}
                  tabIndex={-1}
                  aria-label={showPass ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                >
                  <span className="material-symbols-outlined pg-icon-18">
                    {showPass ? 'visibility_off' : 'visibility'}
                  </span>
                </button>
              </div>
            </ZHField>
          </div>
        </div>
      </div>

      <div className="pg-section sri-section-mb">
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
          <div className="sri-wsdl-actions">
            <ZHBtn variant="ghost" size="sm" type="button" disabled={saving || !canEdit} onClick={() => setWsdlUrl(SRI_WSDL_DEFAULTS.pruebas)}>
              Usar URL Pruebas
            </ZHBtn>
            <ZHBtn variant="ghost" size="sm" type="button" disabled={saving || !canEdit} onClick={() => setWsdlUrl(SRI_WSDL_DEFAULTS.produccion)}>
              Usar URL Producción
            </ZHBtn>
          </div>
          <div className="sri-wsdl-hint">
            <strong>Pruebas:</strong> <span className="mono">{SRI_WSDL_DEFAULTS.pruebas}</span>
            <br />
            <strong>Producción:</strong> <span className="mono">{SRI_WSDL_DEFAULTS.produccion}</span>
          </div>
        </div>
      </div>
    </>
  );
}
