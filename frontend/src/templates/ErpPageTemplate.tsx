import type { ReactNode } from 'react';
import { PageShell } from '../components/PageShell';

/**
 * Plantilla estándar para pantallas ERP (tenant) dentro de AppLayout + LayoutFrame.
 * No incluye navegación ni LayoutFrame (ya los provee el shell).
 *
 * @see docs/frontend-layout-conventions.md
 */
export type ErpPageTemplateProps = {
  title: string;
  kicker?: string;
  subtitle?: string;
  action?: ReactNode;
  /** Envuelve el cuerpo en `.pg-page` (ritmo vertical estándar). Default: true. */
  usePgPage?: boolean;
  /** Clase extra en `.pg-page` (p. ej. prefijo de página `dsh-page`). */
  pageClassName?: string;
  children: ReactNode;
};

export function ErpPageTemplate({
  title,
  kicker,
  subtitle,
  action,
  usePgPage = true,
  pageClassName,
  children,
}: ErpPageTemplateProps) {
  const body = usePgPage ? (
    <div className={['pg-page', pageClassName].filter(Boolean).join(' ')}>{children}</div>
  ) : (
    children
  );

  return (
    <PageShell title={title} kicker={kicker} subtitle={subtitle} action={action}>
      {body}
    </PageShell>
  );
}
