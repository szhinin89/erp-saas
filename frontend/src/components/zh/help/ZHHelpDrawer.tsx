import type { ReactNode } from "react";
import { ZHDrawer } from "../ZHDrawer";

interface ZHHelpDrawerProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
  children: ReactNode;
}

/** Panel lateral de ayuda extendida (manual de módulo, guía paso a paso). Envuelve ZHDrawer sin
 * reimplementar overlay/Escape/foco. Preparado para uso futuro — no se integra en ninguna
 * pantalla todavía. */
export function ZHHelpDrawer({ open, onClose, title, subtitle, children }: ZHHelpDrawerProps) {
  return (
    <ZHDrawer open={open} onClose={onClose} size="md" title={title} subtitle={subtitle}>
      {children}
    </ZHDrawer>
  );
}
