import React from "react";
import {
  allowsLettersKey,
  allowsAlphanumericKey,
} from "../../../lib/validators/textValidators";
import {
  sanitizeLetters,
  sanitizeAlphanumeric,
  uppercase,
} from "../../../lib/sanitizers";
import { setProgrammaticInputValue } from "../../../lib/inputUtils";
import { useZHFieldA11y } from "../zhFieldContext";

export type ZhTextMode = "text" | "letters" | "alphanumeric" | "uppercase";
export type ZhInputDensity = "default" | "compact";
/** Tipos de `<input>` de texto que `ZhTextInput` admite (el default sigue siendo `"text"`). */
export type ZhTextInputType = "text" | "email" | "password" | "search" | "tel" | "url";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> & {
  mode?: ZhTextMode;
  /** Tipo nativo del input; por defecto `"text"` (sin cambio para los consumidores existentes). */
  type?: ZhTextInputType;
  /** `compact` = misma densidad que `<ZHField density="compact">`, para uso suelto
   * dentro de celdas de tabla (`.zh-input--compact`). */
  density?: ZhInputDensity;
};

/**
 * Input de texto con filtrado de caracteres configurable. Compatible con RHF register().
 *
 * - `text`        — sin restricciones (default)
 * - `letters`     — solo letras, acentos, espacios y caracteres de nombre ('. -)
 * - `alphanumeric`— solo letras y dígitos
 * - `uppercase`   — solo letras/dígitos, fuerza mayúsculas en paste
 *
 * @example
 * <ZhTextInput {...register('name')} mode="letters" />
 * <ZhTextInput {...register('sku')} mode="uppercase" />
 * <ZhTextInput {...register('notes')} density="compact" />
 */
export const ZhTextInput = React.forwardRef<HTMLInputElement, Props>(
  (
    { mode = "text", type = "text", density = "default", onKeyDown, onPaste, className, ...props },
    ref,
  ) => {
    const a11y = useZHFieldA11y(props);
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (mode === "letters" && !allowsLettersKey(e)) e.preventDefault();
      if (
        (mode === "alphanumeric" || mode === "uppercase") &&
        !allowsAlphanumericKey(e)
      )
        e.preventDefault();
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      if (mode === "text") {
        onPaste?.(e);
        return;
      }
      e.preventDefault();
      const raw = e.clipboardData.getData("text");
      let clean = raw;
      if (mode === "letters") clean = sanitizeLetters(raw);
      if (mode === "alphanumeric") clean = sanitizeAlphanumeric(raw);
      if (mode === "uppercase") clean = uppercase(sanitizeAlphanumeric(raw));
      if (clean !== "") setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    const cls = [className, density === "compact" ? "zh-input--compact" : ""]
      .filter(Boolean)
      .join(" ");

    return (
      <input
        {...props}
        {...a11y}
        ref={ref}
        type={type}
        className={cls || undefined}
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
      />
    );
  },
);

ZhTextInput.displayName = "ZhTextInput";
