import { resolveHelp, type HelpKeyId, type HelpVariables } from "../../../help";
import { ZHHelpIcon } from "./ZHHelpIcon";
import { ZHHelpPopover } from "./ZHHelpPopover";
import { useHelpDisclosure } from "./useHelpDisclosure";

interface ZHFieldHelpProps {
  helpKey: HelpKeyId;
  variables?: HelpVariables;
}

/** Ayuda contextual junto a un campo (ZHField/label): icono "?" + popover corto (short/long). */
export function ZHFieldHelp({ helpKey, variables }: ZHFieldHelpProps) {
  const { open, pinned, triggerRef, titleId, openByHover, toggleByClick, close } =
    useHelpDisclosure();
  const content = resolveHelp(helpKey, variables);
  if (!content) return null;

  return (
    <span className="zh-help-field">
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
    </span>
  );
}
