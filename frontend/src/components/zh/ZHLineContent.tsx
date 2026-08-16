import type { ReactNode } from "react";

interface ZHLineContentProps {
  children?: ReactNode;
  /** Si es `true`, muestra placeholders animados en lugar de `children`. */
  skeleton?: boolean;
  className?: string;
}

/** Área de contenido de una fila de ítem, con estado de carga (skeleton) — tokens `zh-ui.css` — `.zh-line-content`. */
export function ZHLineContent({ children, skeleton = false, className = "" }: ZHLineContentProps) {
  return (
    <div className={`zh-line-content ${className}`.trim()}>
      {skeleton ? (
        <div className="zh-line-content__skeleton" aria-hidden="true">
          <span className="zh-line-content__skeleton-bar zh-line-content__skeleton-bar--title" />
          <span className="zh-line-content__skeleton-bar zh-line-content__skeleton-bar--sub" />
          <span className="zh-line-content__skeleton-bar zh-line-content__skeleton-bar--meta" />
        </div>
      ) : (
        children
      )}
    </div>
  );
}
