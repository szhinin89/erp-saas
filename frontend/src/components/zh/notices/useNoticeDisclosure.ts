import { useId, useRef, useState } from "react";
import type { NoticeIntent } from "../../../notices";

/** Estado de apertura para avisos compactos. Igual que useHelpDisclosure (hover abre, click fija)
 * salvo para intent "blocking"/"confirmation": ahí solo click abre — un aviso de bloqueo no debe
 * autoabrirse por hover ni cerrarse por un simple mouseleave. */
export function useNoticeDisclosure(intent: NoticeIntent) {
  const [open, setOpen] = useState(false);
  const [pinned, setPinned] = useState(false);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const titleId = useId();
  const hoverAllowed = intent !== "blocking" && intent !== "confirmation";

  const openByHover = () => {
    if (hoverAllowed && !pinned) setOpen(true);
  };
  const toggleByClick = () => {
    setPinned((p) => {
      const next = !p;
      setOpen(next);
      return next;
    });
  };
  const close = () => {
    setOpen(false);
    setPinned(false);
  };

  return {
    open,
    pinned,
    triggerRef,
    titleId,
    openByHover,
    toggleByClick,
    close,
  };
}
