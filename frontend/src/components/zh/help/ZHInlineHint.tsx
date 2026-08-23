import { resolveHelp, type HelpKeyId, type HelpVariables } from "../../../help";

interface ZHInlineHintProps {
  helpKey: HelpKeyId;
  variables?: HelpVariables;
  className?: string;
}

/** Texto de ayuda siempre visible (sin icono/popover), para cuando la información debe verse
 * sin interacción. Usa los tokens --text-help-* reservados para este caso. */
export function ZHInlineHint({ helpKey, variables, className }: ZHInlineHintProps) {
  const content = resolveHelp(helpKey, variables);
  if (!content) return null;

  return (
    <p className={["zh-help-inline-hint", className].filter(Boolean).join(" ")}>
      {content.short}
    </p>
  );
}
