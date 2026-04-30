import './PageShell.css';

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
  return <div className="error-state">Error: {message}</div>;
}

export function LoadingState() {
  return <div className="loading-state">Cargando...</div>;
}

export function Badge({ label, variant }: { label: string; variant: 'green' | 'gray' | 'red' | 'blue' }) {
  return <span className={`badge badge--${variant}`}>{label}</span>;
}
