import type { FieldErrors, UseFormRegister, UseFormSetValue } from 'react-hook-form';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { CatalogItem, ProductCategoryListItem } from '../api/catalogService';
import type { CatalogStructureModalForm } from './catalogStructureTypes';
import './CatalogStructurePage.css';

type CatalogStructureModalProps = {
  t: (key: string) => string;
  title: string;
  saving: boolean;
  error: string;
  showLineSelector: boolean;
  showCategorySelector: boolean;
  lines: CatalogItem[];
  modalCats: ProductCategoryListItem[];
  watchedLineId: string;
  register: UseFormRegister<CatalogStructureModalForm>;
  setValue: UseFormSetValue<CatalogStructureModalForm>;
  errors: FieldErrors<CatalogStructureModalForm>;
  onClose: () => void;
  onSubmit: (e?: React.BaseSyntheticEvent) => Promise<void>;
};

export function CatalogStructureModal({
  t,
  title,
  saving,
  error,
  showLineSelector,
  showCategorySelector,
  lines,
  modalCats,
  watchedLineId,
  register,
  setValue,
  errors,
  onClose,
  onSubmit,
}: CatalogStructureModalProps) {
  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="zh-modal pg-modal--480">
        <div className="zh-modal-header">
          <h2 className="pg-modal-title-text">{title}</h2>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={onClose} aria-label={t('common.close')}>
            <span className="material-symbols-outlined">close</span>
          </button>
        </div>

        <form onSubmit={onSubmit}>
          <div className="zh-modal-body">
            {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

            <div className="pg-form-grid--2">
              <ZHField label={t('common.code')} required error={errors.code?.message}>
                <input className="zh-input" {...register('code', { required: t('common.required') })} disabled={saving} />
              </ZHField>
              <ZHField label={t('common.name')} required error={errors.name?.message}>
                <input className="zh-input" {...register('name', { required: t('common.required') })} disabled={saving} />
              </ZHField>
            </div>

            {showLineSelector && (
              <ZHField label={t('catalog.categories.line')} required error={errors.lineId?.message}>
                <select
                  className="zh-input"
                  disabled={saving}
                  {...register('lineId', {
                    required: t('common.required'),
                    onChange: () => setValue('categoryId', ''),
                  })}
                >
                  <option value="">{t('common.select')}</option>
                  {lines.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.code} — {l.name}
                    </option>
                  ))}
                </select>
              </ZHField>
            )}

            {showCategorySelector && (
              <ZHField label={t('catalog.subcategories.category')} required error={errors.categoryId?.message}>
                <select
                  className="zh-input"
                  disabled={saving || !watchedLineId}
                  {...register('categoryId', { required: t('common.required') })}
                >
                  <option value="">{t('common.select')}</option>
                  {modalCats.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
              </ZHField>
            )}
          </div>

          <div className="pg-actions-bar">
            <ZHBtn variant="ghost" size="md" type="button" onClick={onClose} disabled={saving}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
              {saving ? t('common.saving') : t('common.saveChanges')}
            </ZHBtn>
          </div>
        </form>
      </div>
    </div>
  );
}
