import type { LabelHTMLAttributes, ReactNode } from "react";

export type ZHFieldLabelSize = "sm" | "md";

export type ZHFieldLabelProps = LabelHTMLAttributes<HTMLLabelElement> & {
  children: ReactNode;
  size?: ZHFieldLabelSize;
  required?: boolean;
  optional?: boolean;
  /** Texto del indicador "opcional" — permite traducirlo (i18n) desde el consumidor.
   * Sin este prop, mantiene el fallback en español `"(opcional)"` por compatibilidad. */
  optionalLabel?: ReactNode;
};

const DEFAULT_OPTIONAL_LABEL = "(opcional)";

/** Label standalone del Design System ZH — reutiliza la tipografía oficial de
 * `.zh-field-label` sin depender del wrapper completo de `ZHField`. `size="sm"` cubre
 * labels compactos de líneas/cards/métricas (antes `sf-*`/`pdl-*` por módulo); `size="md"`
 * cubre formularios normales. El componente no resuelve i18n por sí mismo: `optionalLabel`
 * deja que el consumidor pase el texto ya traducido (p. ej. vía `useI18n().t(...)`); sin él,
 * usa el fallback fijo en español. */
export function ZHFieldLabel({
  children,
  size = "md",
  required,
  optional,
  optionalLabel,
  className,
  ...rest
}: ZHFieldLabelProps) {
  const cls = [
    "zh-field-label",
    `zh-field-label--${size}`,
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <label {...rest} className={cls}>
      {children}
      {required ? (
        <span className="zh-field-label__required">*</span>
      ) : optional ? (
        <span className="zh-field-label__optional">
          {optionalLabel ?? DEFAULT_OPTIONAL_LABEL}
        </span>
      ) : null}
    </label>
  );
}
