import { ZHGrid } from './ZHForm';
import './ZHLayout.css';

/** Título de pantalla compacto, pegado al cabezal/menú (sin barra azul ZHFormHeader). */
export function ZHScreenHeading(props: {
  kicker?: string;
  title: string;
  subtitle?: string;
  right?: React.ReactNode;
}) {
  const { kicker, title, subtitle, right } = props;
  return (
    <header className="zh-screen-heading">
      {kicker ? <div className="zh-screen-heading-kicker">{kicker}</div> : null}
      <div className="zh-screen-heading-row">
        <div className="zh-screen-heading-main">
          <span className="zh-screen-heading-chip" aria-hidden="true" />
          <div className="zh-screen-heading-copy">
            <h1 className="zh-screen-heading-title">{title}</h1>
            {subtitle ? <p className="zh-screen-heading-sub">{subtitle}</p> : null}
          </div>
        </div>
        {right ? <div className="zh-screen-heading-right">{right}</div> : null}
      </div>
    </header>
  );
}

export function ZHCardSection(props: { title: string; right?: React.ReactNode; children: React.ReactNode }) {
  const { title, right, children } = props;
  return (
    <div className="zh-card-section">
      <div className="zh-card-section-header">
        <div className="zh-card-section-title">{title}</div>
        {right ? <div className="zh-card-section-right">{right}</div> : null}
      </div>
      <div className="zh-card-section-body">{children}</div>
    </div>
  );
}

export function ZHInlineRow(props: { children: React.ReactNode; className?: string }) {
  return <div className={['zh-inline-row', props.className].filter(Boolean).join(' ')}>{props.children}</div>;
}

export function ZHInlineRowRight(props: { children: React.ReactNode }) {
  return <div className="zh-inline-row-right">{props.children}</div>;
}

export function ZHActionsRow(props: { children: React.ReactNode; className?: string }) {
  return <div className={['zh-flex-end', 'zh-wrap', props.className].filter(Boolean).join(' ')}>{props.children}</div>;
}

export function ZHColSpan(props: { span: 2 | 3; children: React.ReactNode }) {
  return <div className={`zh-col-span-${props.span}`}>{props.children}</div>;
}

export function ZHGridRow(props: { cols: 1 | 2 | 3; children: React.ReactNode; className?: string }) {
  return (
    <div className={['zh-grid-row', props.className].filter(Boolean).join(' ')}>
      <ZHGrid cols={props.cols}>{props.children}</ZHGrid>
    </div>
  );
}

type Spacing = 6 | 8 | 10 | 12 | 14 | 16;

export function ZHSection(props: { top?: Spacing; bottom?: Spacing; children: React.ReactNode; className?: string }) {
  const topClass =
    props.top === 6 ? 'zh-mt-6' :
    props.top === 8 ? 'zh-mt-8' :
    props.top === 10 ? 'zh-mt-10' :
    props.top === 12 ? 'zh-mt-12' :
    props.top === 14 ? 'zh-mt-14' :
    props.top === 16 ? 'zh-mt-16' :
    '';
  const bottomClass =
    props.bottom === 6 ? 'zh-mb-6' :
    props.bottom === 8 ? 'zh-mb-8' :
    props.bottom === 10 ? 'zh-mb-10' :
    props.bottom === 12 ? 'zh-mb-12' :
    props.bottom === 14 ? 'zh-mb-14' :
    props.bottom === 16 ? 'zh-mb-16' :
    '';
  return <div className={[topClass, bottomClass, props.className].filter(Boolean).join(' ')}>{props.children}</div>;
}

export function ZHStack(props: { gap?: Spacing; children: React.ReactNode; className?: string }) {
  const gapClass =
    props.gap === 6 ? 'zh-gap-6' :
    props.gap === 8 ? 'zh-gap-8' :
    props.gap === 10 ? 'zh-gap-10' :
    props.gap === 12 ? 'zh-gap-12' :
    props.gap === 14 ? 'zh-gap-14' :
    props.gap === 16 ? 'zh-gap-16' :
    '';
  return <div className={['zh-stack', gapClass, props.className].filter(Boolean).join(' ')}>{props.children}</div>;
}

