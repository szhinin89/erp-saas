import { ZHModal } from './zh/ZHModal';
import { ZHBtn } from './zh/ZHForm';
import { ZHPageNotice } from './zh/ZHPageNotice';
import { useI18n } from '../i18n/i18n';
import type { SessionBranchDto } from '../types/session';

interface Props {
  open: boolean;
  loading: boolean;
  options: SessionBranchDto[];
  error: string;
  switching: boolean;
  onSelect: (branchId: string) => void;
  onRetry: () => void;
}

/**
 * Selector de sucursal post-login — bloqueante: sin botón de cerrar ni cierre por backdrop
 * (closeOnBackdrop=false, sin footer/close visible vía onClose no-op), montado por
 * useBranchGate mientras no exista una sucursal activa resoluble automáticamente
 * (LoginMode.AskBranch, o DirectToDefault con la sucursal por defecto ya revocada).
 */
export function BranchSelectorModal({ open, loading, options, error, switching, onSelect, onRetry }: Props) {
  const { t } = useI18n();

  return (
    <ZHModal
      open={open}
      onClose={() => {}}
      closeOnBackdrop={false}
      size="sm"
      title={t('session.branchSelector.title', 'Seleccione una sucursal')}
      subtitle={t('session.branchSelector.subtitle', 'Elija la sucursal en la que va a operar.')}
    >
      {loading ? (
        <p className="ts-empty">{t('session.branchSelector.loading', 'Cargando sucursales disponibles…')}</p>
      ) : (
        <div className="ts-list" role="list">
          {options.map((branch) => (
            <div key={branch.id} className="zh-entity-item" role="listitem">
              <div className="zh-entity-item-info">
                <span className="zh-entity-item-name">{branch.name}</span>
                {branch.isMainBranch && <span className="zh-entity-item-sub">Matriz</span>}
              </div>
              <div className="zh-entity-item-right">
                <ZHBtn
                  variant="primary"
                  size="sm"
                  disabled={switching}
                  onClick={() => onSelect(branch.id)}
                >
                  {t('session.branchSelector.confirm', 'Ingresar')}
                </ZHBtn>
              </div>
            </div>
          ))}

          {options.length === 0 && !error && (
            <p className="ts-empty">{t('session.branchSelector.empty', 'No tiene sucursales asignadas. Contacte a un administrador.')}</p>
          )}
        </div>
      )}

      {error ? (
        <>
          <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />
          <ZHBtn variant="secondary" size="sm" disabled={loading} onClick={onRetry}>
            {t('session.branchSelector.retry', 'Reintentar')}
          </ZHBtn>
        </>
      ) : null}
    </ZHModal>
  );
}
