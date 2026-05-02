import { forwardRef } from 'react';
import { TableCard } from '../PageShell';
import { ZHFormBody, ZHFormHeader } from './ZHForm';

export type ZHFormCardProps = {
  title: string;
  subtitle: string;
  /** Sin barra superior ZHFormHeader (útil cuando la página ya define un título compacto). */
  hideHeader?: boolean;
  onSubmit: (e: React.FormEvent) => void;
  children: React.ReactNode;
};

export const ZHFormCard = forwardRef<HTMLFormElement, ZHFormCardProps>(function ZHFormCard(
  { title, subtitle, hideHeader, onSubmit, children },
  ref
) {
  return (
    <TableCard>
      <form ref={ref} onSubmit={onSubmit}>
        {!hideHeader ? <ZHFormHeader title={title} subtitle={subtitle} /> : null}
        <ZHFormBody standalone={!!hideHeader}>{children}</ZHFormBody>
      </form>
    </TableCard>
  );
});

