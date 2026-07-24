import { useEffect } from 'react';
import { ZHPageNotice } from './ZHPageNotice';
import { ELECTRONIC_INVOICING_STATUS_REGISTRY } from './electronicInvoicingStatusRegistry';
import { useI18n } from '../../i18n/i18n';
import { useElectronicInvoicingStatusStore } from '../../store/electronicInvoicingStatusStore';

/**
 * Indicador transversal del estado operativo de facturación electrónica — infraestructura
 * cerrada del ERP (ver `docs/frontend/ELECTRONIC_ENVIRONMENT_BANNER.md`). Sin props: se
 * autoabastece del store cacheado `useElectronicInvoicingStatusStore` (poblado una vez al
 * iniciar sesión, cambiar de empresa, o guardar la configuración SRI — nunca por pantalla).
 *
 * El único campo que decide la representación es `status` (calculado por el backend); este
 * componente jamás combina booleanos para inferir un estado — solo hace
 * `ELECTRONIC_INVOICING_STATUS_REGISTRY[status]` y renderiza. Reutilizable en cualquier pantalla
 * emisora de documentos electrónicos con solo `<ZHElectronicEnvironmentBanner />`.
 */
export function ZHElectronicEnvironmentBanner() {
  const { t } = useI18n();
  const status = useElectronicInvoicingStatusStore((s) => s.status);
  const isLoaded = useElectronicInvoicingStatusStore((s) => s.isLoaded);
  const isLoading = useElectronicInvoicingStatusStore((s) => s.isLoading);
  const refresh = useElectronicInvoicingStatusStore((s) => s.refresh);

  useEffect(() => {
    if (!isLoaded && !isLoading) void refresh();
  }, [isLoaded, isLoading, refresh]);

  if (!status) return null;

  const visual = ELECTRONIC_INVOICING_STATUS_REGISTRY[status.status];
  const kicker = t('common.electronicEnvironmentBanner.kicker');
  const message = t(visual.messageKey);
  const detail = t(visual.detailKey);

  return (
    <div className="zh-mb-8">
      <p className="zh-text-muted zh-text-xs">{kicker}</p>
      <ZHPageNotice variant={visual.variant} icon={visual.icon} message={message} detail={detail} />
    </div>
  );
}
