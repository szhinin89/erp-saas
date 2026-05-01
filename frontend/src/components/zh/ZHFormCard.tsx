import { TableCard } from '../PageShell';
import { ZHFormBody, ZHFormHeader } from './ZHForm';

export function ZHFormCard(props: {
  title: string;
  subtitle: string;
  /** Sin barra superior ZHFormHeader (útil cuando la página ya define un título compacto). */
  hideHeader?: boolean;
  onSubmit: (e: React.FormEvent) => void;
  children: React.ReactNode;
}) {
  const { title, subtitle, hideHeader, onSubmit, children } = props;
  return (
    <TableCard>
      <form onSubmit={onSubmit}>
        {!hideHeader ? <ZHFormHeader title={title} subtitle={subtitle} /> : null}
        <ZHFormBody standalone={!!hideHeader}>{children}</ZHFormBody>
      </form>
    </TableCard>
  );
}

