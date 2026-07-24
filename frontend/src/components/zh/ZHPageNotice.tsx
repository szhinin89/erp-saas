import { ZHFormAlert, type ZHFormAlertType } from './ZHForm';

export type ZHPageNoticeVariant = ZHFormAlertType;

export type ZHPageNoticeProps = {
  /** success = operación OK, error = fallo, warning = aviso sin bloquear, info = informativo, attention = intermedio/naranja, neutral = inactivo/gris. */
  variant: ZHPageNoticeVariant;
  /** Mensaje principal (título corto). Si solo hay detalle, puede ir vacío y usarse `detail` como cuerpo. */
  message: string;
  /** Texto secundario (p. ej. mensaje del API o detalle técnico). */
  detail?: string | null;
  /** Material Symbol que sobrescribe el icono por defecto de la variante. */
  icon?: string;
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
  const { variant, message, detail, icon, className } = props;
  const msg = message.trim();
  const det = (detail ?? '').trim();
  if (!msg && !det) return null;

  const primary = msg || det;
  const secondary = msg && det ? det : undefined;
  const live: 'polite' | 'assertive' = variant === 'success' || variant === 'info' || variant === 'neutral' ? 'polite' : 'assertive';
  const role = variant === 'error' || variant === 'warning' || variant === 'attention' ? 'alert' : 'status';

  return (
    <div className={['zh-page-notice', className].filter(Boolean).join(' ')} role={role} aria-live={live}>
      <ZHFormAlert type={variant} message={primary} detail={secondary} icon={icon} />
    </div>
  );
}
