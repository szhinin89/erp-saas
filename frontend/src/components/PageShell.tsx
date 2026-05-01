import './PageShell.css';
import { useI18n } from '../i18n/i18n';
import { ZHScreenHeading } from './zh/ZHLayout';

interface Props {
  title: string;
  /** Línea superior (módulo / contexto), opcional. */
  kicker?: string;
  subtitle?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}

/** Contenedor de página con encabezado compacto unificado (ZHScreenHeading). */
export function PageShell({ title, kicker, subtitle, action, children }: Props) {
  return (
    <div className="page-shell page-shell--compactHeading">
      <div className="page-shell-heading">
        <ZHScreenHeading kicker={kicker} title={title} subtitle={subtitle} right={action} />
      </div>
      {children}
    </div>
  );
}

export function TableCard({ children }: { children: React.ReactNode }) {
  return <div className="table-card">{children}</div>;
}

export function EmptyState({ message }: { message: string }) {
  return <div className="empty-state">{message}</div>;
}

export function ErrorState({ message }: { message: string }) {
  const { t } = useI18n();
  return <div className="error-state">{t('common.errorPrefix')} {message}</div>;
}

export function LoadingState() {
  const { t } = useI18n();
  return <div className="loading-state">{t('common.loading')}</div>;
}

export function NoAccessPage({ title }: { title: string }) {
  const { t } = useI18n();
  return (
    <PageShell title={title} subtitle={t('common.noAccess')}>
      <TableCard>
        <EmptyState message={t('common.noAccess')} />
      </TableCard>
    </PageShell>
  );
}

export function PageToolbar(props: { children: React.ReactNode }) {
  return <div className="zh-page-toolbar">{props.children}</div>;
}

export function Badge({ label, variant }: { label: string; variant: 'green' | 'gray' | 'red' | 'blue' }) {
  return <span className={`badge badge--${variant}`}>{label}</span>;
}
