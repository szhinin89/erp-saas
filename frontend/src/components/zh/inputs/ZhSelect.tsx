import React from "react";
import type { ZhInputDensity } from "./ZhTextInput";

type Props = React.SelectHTMLAttributes<HTMLSelectElement> & {
  /** `compact` = misma densidad que `<ZHField density="compact">`, para uso suelto
   * dentro de celdas de tabla o filtros (`.zh-input--compact`). */
  density?: ZhInputDensity;
};

/**
 * Select nativo normalizado. Compatible con RHF register() via forwardRef.
 * No define opciones — el consumidor las provee como children.
 *
 * @example
 * <ZhSelect {...register('role')}>
 *   <option value="">Seleccione...</option>
 *   <option value="admin">Administrador</option>
 * </ZhSelect>
 * <ZhSelect density="compact" {...register('status')}>...</ZhSelect>
 */
export const ZhSelect = React.forwardRef<HTMLSelectElement, Props>(
  ({ density = "default", className, children, ...props }, ref) => {
    const cls = [className, density === "compact" ? "zh-input--compact" : ""]
      .filter(Boolean)
      .join(" ");

    return (
      <select {...props} ref={ref} className={cls || undefined}>
        {children}
      </select>
    );
  },
);

ZhSelect.displayName = "ZhSelect";
