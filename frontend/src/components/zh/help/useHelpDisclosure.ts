import { useId, useRef, useState } from "react";

/** Estado de apertura compartido por ZHFieldHelp/ZHSectionHelp: hover/focus abre, click fija
 * (pinned) para soportar touch, que no dispara hover. */
export function useHelpDisclosure() {
  const [open, setOpen] = useState(false);
  const [pinned, setPinned] = useState(false);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const titleId = useId();

  const openByHover = () => {
    if (!pinned) setOpen(true);
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
