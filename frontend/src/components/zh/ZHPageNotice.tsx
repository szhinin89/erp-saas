import { ZHFormAlert, type ZHFormAlertType } from './ZHForm';
import './ZHPageNotice.css';

export type ZHPageNoticeVariant = ZHFormAlertType;

export type ZHPageNoticeProps = {
  /** success = operación OK, error = fallo, warning = aviso sin bloquear, info = informativo. */
  variant: ZHPageNoticeVariant;
  /** Mensaje principal (título corto). Si solo hay detalle, puede ir vacío y usarse `detail` como cuerpo. */
  message: string;
  /** Texto secundario (p. ej. mensaje del API o detalle técnico). */
  detail?: string | null;
  className?: string;
};

/**
 * Aviso reutilizable para pantallas tras guardar, crear o fallar.
 * Mismo formato visual en todo el ERP (icono + colores por variante).
 *
 * @example Éxito tras guardar
 * `<ZHPageNotice variant="success" message={t('platform.navigationMenu.saved')} />`
 *
 * @example Error con prefijo + detalle API
 * `<ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />`
 *
 * @example Solo mensaje de aviso
 * `<ZHPageNotice variant="warning" message={t('branches.error.geography')} />`
 */
export function ZHPageNotice(props: ZHPageNoticeProps) {
  const { variant, message, detail, className } = props;
  const msg = message.trim();
  const det = (detail ?? '').trim();
  if (!msg && !det) return null;

  const primary = msg || det;
  const secondary = msg && det ? det : undefined;
  const live: 'polite' | 'assertive' = variant === 'success' || variant === 'info' ? 'polite' : 'assertive';
  const role = variant === 'error' || variant === 'warning' ? 'alert' : 'status';

  return (
    <div className={['zh-page-notice', className].filter(Boolean).join(' ')} role={role} aria-live={live}>
      <ZHFormAlert type={variant} message={primary} detail={secondary} />
    </div>
  );
}
