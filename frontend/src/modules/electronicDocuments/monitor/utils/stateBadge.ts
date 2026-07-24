import type { BadgeVariant } from '../../../../components/PageShell';

/** Variante del componente `Badge` (`PageShell.tsx`) por estado — estados reales del motor, no inventados. */
export function electronicDocumentStateBadgeVariant(state: string): BadgeVariant {
  switch (state) {
    case 'Draft':
    case 'XmlGenerated':
      return 'gray';
    case 'Signed':
    case 'Sent':
    case 'Received':
      return 'blue';
    case 'Authorized':
      return 'green';
    case 'Rejected':
    case 'DeadLetter':
    case 'Failed':
      return 'red';
    case 'Cancelled':
      return 'orange';
    default:
      return 'gray';
  }
}

/** Ícono Material Symbols por estado — acompaña el badge para reconocimiento visual rápido. */
export function electronicDocumentStateIcon(state: string): string {
  switch (state) {
    case 'Draft': return 'schedule';
    case 'XmlGenerated': return 'description';
    case 'Signed': return 'draw';
    case 'Sent': return 'send';
    case 'Received': return 'hourglass_top';
    case 'Authorized': return 'check_circle';
    case 'Rejected': return 'cancel';
    case 'Failed': return 'error';
    case 'DeadLetter': return 'report';
    case 'Cancelled': return 'block';
    default: return 'help';
  }
}
