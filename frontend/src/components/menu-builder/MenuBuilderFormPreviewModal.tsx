import type { FuncionalidadArbolDto } from '../../modules/superadmin/api/superAdminService';

type Props = {
  previewForm: FuncionalidadArbolDto;
  onClose: () => void;
};

export function MenuBuilderFormPreviewModal({ previewForm, onClose }: Props) {
  return (
    <div
      className="zh-modal-overlay menu-builder-form-preview-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label="Mockup de formulario"
      onClick={onClose}
    >
      <div className="menu-builder-form-preview-card" onClick={(e) => e.stopPropagation()}>
        <div className="menu-builder-form-preview-head">
          <h4>Mockup de formulario</h4>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--xs" onClick={onClose} aria-label="Cerrar mockup">
            ✕
          </button>
        </div>
        <p className="menu-builder-form-preview-subtle">
          Vista previa visual de <strong>{previewForm.name}</strong>
        </p>
        <div className="menu-builder-form-preview-meta">
          <span>
            <strong>Ruta:</strong> {previewForm.path?.trim() || '—'}
          </span>
          <span>
            <strong>Permiso:</strong> {previewForm.permission?.trim() || '—'}
          </span>
        </div>
        <div className="menu-builder-form-preview-body">
          <div className="menu-builder-form-preview-field">
            <label>Campo principal</label>
            <input className="zh-input" value="" readOnly placeholder="Ejemplo..." />
          </div>
          <div className="menu-builder-form-preview-field">
            <label>Descripción</label>
            <textarea className="zh-input" value="" readOnly placeholder="Contenido de ejemplo..." />
          </div>
          <div className="menu-builder-form-preview-actions">
            <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" disabled>
              Guardar
            </button>
            <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" disabled>
              Cancelar
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
