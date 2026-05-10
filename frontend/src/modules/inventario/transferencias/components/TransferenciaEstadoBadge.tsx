import type { EstadoTransferencia } from '../api/transferenciaService';

const ESTADO_STYLES: Record<EstadoTransferencia, string> = {
  Borrador:   'zh-badge zh-badge--warning',
  Confirmado: 'zh-badge zh-badge--success',
  Cancelado:  'zh-badge zh-badge--error',
};

const ESTADO_LABEL: Record<EstadoTransferencia, string> = {
  Borrador:   'Borrador',
  Confirmado: 'Confirmado',
  Cancelado:  'Cancelado',
};

interface Props {
  estado: EstadoTransferencia;
}

export function TransferenciaEstadoBadge({ estado }: Props) {
  const cls   = ESTADO_STYLES[estado] ?? 'zh-badge';
  const label = ESTADO_LABEL[estado]  ?? estado;
  return <span className={cls}>{label}</span>;
}
