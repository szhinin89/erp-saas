import React from "react";
import type { ZhInputDensity } from "./ZhTextInput";

type Props = React.TextareaHTMLAttributes<HTMLTextAreaElement> & {
  /** `compact` = misma densidad que `<ZHField density="compact">`, para uso suelto
   * dentro de celdas de tabla o filtros (`.zh-input--compact`). */
  density?: ZhInputDensity;
};

/**
 * Textarea nativo normalizado. Compatible con RHF register() via forwardRef.
 *
 * @example
 * <ZhTextarea rows={3} {...register('notes')} />
 * <ZhTextarea density="compact" rows={2} {...register('closeNotes')} />
 */
export const ZhTextarea = React.forwardRef<HTMLTextAreaElement, Props>(
  ({ density = "default", className, ...props }, ref) => {
    const cls = [className, density === "compact" ? "zh-input--compact" : ""]
      .filter(Boolean)
      .join(" ");

    return <textarea {...props} ref={ref} className={cls || undefined} />;
  },
);

ZhTextarea.displayName = "ZhTextarea";
