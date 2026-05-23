import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { useI18n } from '../../../i18n/i18n';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';

type BrandFormValues = {
  code: string;
  name: string;
  manufacturer?: string;
  countryOfOrigin?: string;
};

interface Props {
  editingId: string | null;
  saving: boolean;
  saveError: string;
  register: UseFormRegister<BrandFormValues>;
  errors: FieldErrors<BrandFormValues>;
  onSave: () => void;
  onCancel: () => void;
}

export function BrandFormTab({ editingId, saving, saveError, register, errors, onSave, onCancel }: Props) {
  const { t } = useI18n();

  return (
    <div className="cat-brand-form-tab prd-fadein">

      {/* Panel header */}
      <div className="cat-brand-form-head">
        <span className="cat-brand-form-head__icon material-symbols-outlined">
          {editingId ? 'edit' : 'add_circle'}
        </span>
        <h3 className="cat-brand-form-head__title">
          {editingId
            ? t('brands.form.title.edit', 'Editar Marca')
            : t('brands.form.title.new', 'Nueva Marca')}
        </h3>
      </div>

      {saveError && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix', 'Error:')} detail={saveError} />
      )}

      <div className="cat-brand-form-grid">
        <ZHField label={t('common.code', 'Código')} required error={errors.code?.message}>
          <input
            className="zh-input"
            placeholder="MRC-001"
            disabled={saving}
            aria-required="true"
            {...register('code')}
          />
        </ZHField>

        <ZHField label={t('common.name', 'Nombre')} required error={errors.name?.message}>
          <input
            className="zh-input"
            placeholder={t('brands.form.namePlaceholder', 'Ej. Nike, Bosch, Samsung')}
            disabled={saving}
            aria-required="true"
            {...register('name')}
          />
        </ZHField>

        <ZHField label={t('brands.form.manufacturer', 'Fabricante')} error={errors.manufacturer?.message}>
          <input
            className="zh-input"
            placeholder={t('brands.form.manufacturerPlaceholder', 'Nombre del fabricante')}
            disabled={saving}
            {...register('manufacturer')}
          />
        </ZHField>

        <ZHField label={t('brands.form.countryOfOrigin', 'País de origen')} error={errors.countryOfOrigin?.message}>
          <input
            className="zh-input"
            placeholder={t('brands.form.countryPlaceholder', 'Ej. Alemania, EE.UU.')}
            disabled={saving}
            {...register('countryOfOrigin')}
          />
        </ZHField>
      </div>

      <div className="cat-brand-form-footer">
        <div />
        <div className="cat-brand-form-footer__actions">
          <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={onCancel}>
            {t('common.cancel', 'Cancelar')}
          </ZHBtn>
          <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={onSave}>
            <span className="material-symbols-outlined">save</span>
            {saving
              ? t('common.saving', 'Guardando...')
              : editingId
                ? t('brands.form.btn.update', 'Actualizar Marca')
                : t('brands.form.btn.save', 'Guardar Marca')}
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}
