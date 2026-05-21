import type { ReactNode } from 'react';

interface ZHCardProps {
  title?: ReactNode;
  actions?: ReactNode;
  className?: string;
  bodyClassName?: string;
  children: ReactNode;
}

/** Contenedor card estándar (tokens `zh-ui.css` — `.card`). */
export function ZHCard({ title, actions, className = '', bodyClassName = '', children }: ZHCardProps) {
  const cardClassName = `card ${className}`.trim();
  const cardBodyClassName = `card-body ${bodyClassName}`.trim();
  return (
    <div className={cardClassName}>
      {(title || actions) && (
        <div className="card-header">
          {title && <span>{title}</span>}
          {actions && <div>{actions}</div>}
        </div>
      )}
      <div className={cardBodyClassName}>{children}</div>
    </div>
  );
}
