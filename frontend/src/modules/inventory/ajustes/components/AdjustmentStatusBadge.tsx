import type { AdjustmentStatus, AdjustmentTypeEnum } from '../api/adjustmentService';

const ESTADO_STYLES: Record<AdjustmentStatus, string> = {
  Borrador:  'zh-badge zh-badge--warning',
  Ejecutado: 'zh-badge zh-badge--success',
  Cancelado: 'zh-badge zh-badge--error',
};

export function AjusteEstadoBadge({ estado }: { estado: AdjustmentStatus }) {
  return <span className={ESTADO_STYLES[estado] ?? 'zh-badge'}>{estado}</span>;
}

export function AjusteTipoBadge({ tipo, cantidad }: { tipo: AdjustmentTypeEnum; quantity: number }) {
  const isPositivo = tipo === 'Incremento';
  const cls   = isPositivo ? 'zh-badge zh-badge--success' : 'zh-badge zh-badge--error';
  const signo = isPositivo ? '+' : '';
  return <span className={cls}>{signo}{cantidad}</span>;
}
