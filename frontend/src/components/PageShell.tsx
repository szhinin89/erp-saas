import './PageShell.css';
import { useI18n } from '../i18n/i18n';

interface Props {
  title: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}

export function PageShell({ title, action, children }: Props) {
  return (
    <div className="page-shell">
      <div className="page-shell-header">
        <h1 className="page-shell-title">{title}</h1>
        {action && <div className="page-shell-action">{action}</div>}
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

export function Badge({ label, variant }: { label: string; variant: 'green' | 'gray' | 'red' | 'blue' }) {
  return <span className={`badge badge--${variant}`}>{label}</span>;
}
