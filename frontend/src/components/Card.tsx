import type { ReactNode } from 'react';

interface CardProps {
  title?: ReactNode;
  actions?: ReactNode;
  className?: string;
  bodyClassName?: string;
  children: ReactNode;
}

/** Contenedor `.card` de `zh-ui.css`. */
export function Card({ title, actions, className = '', bodyClassName = '', children }: CardProps) {
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
