import { resolveHelp, type HelpKeyId, type HelpVariables } from "../../../help";
import { ZHHelpIcon } from "./ZHHelpIcon";
import { ZHHelpPopover } from "./ZHHelpPopover";
import { useHelpDisclosure } from "./useHelpDisclosure";

interface ZHSectionHelpProps {
  helpKey: HelpKeyId;
  variables?: HelpVariables;
  className?: string;
}

/** Ayuda contextual de sección: título + icono "?" que abre un popover corto (short/long), en
 * lugar de una tarjeta informativa permanente. Reemplaza al patrón ZHPageNotice variant="info"
 * usado como aviso fijo no bloqueante. */
export function ZHSectionHelp({ helpKey, variables, className }: ZHSectionHelpProps) {
  const { open, pinned, triggerRef, titleId, openByHover, toggleByClick, close } =
    useHelpDisclosure();
  const content = resolveHelp(helpKey, variables);
  if (!content) return null;

  return (
    <div className={["zh-help-section", className].filter(Boolean).join(" ")}>
      <span className="zh-help-section__title">{content.title}</span>
      <ZHHelpIcon
        ariaLabel={content.title}
        expanded={open}
        describedById={titleId}
        triggerRef={triggerRef}
        onClick={toggleByClick}
        onMouseEnter={openByHover}
      />
      <ZHHelpPopover
        open={open}
        onClose={close}
        anchorRef={triggerRef}
        titleId={titleId}
        title={content.title}
        closeOnMouseLeave={!pinned}
      >
        <p className="zh-help-popover__short">{content.short}</p>
        {content.long && <p className="zh-help-popover__long">{content.long}</p>}
      </ZHHelpPopover>
    </div>
  );
}
