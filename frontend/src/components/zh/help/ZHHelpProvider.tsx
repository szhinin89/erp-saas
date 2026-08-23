import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import type { HelpMode } from "../../../help";

interface HelpModeContextValue {
  mode: HelpMode;
  setMode: (mode: HelpMode) => void;
}

const HelpModeContext = createContext<HelpModeContextValue | null>(null);

/** Preferencia de densidad de ayuda (compact/guided/expert). Opcional: sin Provider, los
 * componentes de ayuda usan "guided" por defecto — no fuerza integración global. */
export function ZHHelpProvider({
  children,
  defaultMode = "guided",
}: {
  children: ReactNode;
  defaultMode?: HelpMode;
}) {
  const [mode, setMode] = useState<HelpMode>(defaultMode);
  const value = useMemo(() => ({ mode, setMode }), [mode]);
  return (
    <HelpModeContext.Provider value={value}>
      {children}
    </HelpModeContext.Provider>
  );
}

export function useHelpMode(): HelpModeContextValue {
  const ctx = useContext(HelpModeContext);
  if (ctx) return ctx;
  return { mode: "guided", setMode: () => {} };
}
