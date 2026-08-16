/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, type ReactNode } from "react";

export type ZHLocaleContextValue = {
  locale: string;
};

const ZHLocaleContext = createContext<ZHLocaleContextValue | null>(null);

/** Provider global del Design System para el locale regional (formato numérico/monetario)
 * configurado por empresa — distinto del `I18nProvider` (`i18n/i18n.tsx`), que resuelve el
 * idioma de textos de la UI (`es`/`en`/`qu`), no el formato regional de números. Envolver la
 * app (o el árbol donde se necesite) una vez que exista la configuración de empresa real;
 * sin este provider, los consumidores (p. ej. `ZHMoneyValue`) usan su fallback propio. */
export function ZHLocaleProvider({
  locale,
  children,
}: {
  locale: string;
  children: ReactNode;
}) {
  return (
    <ZHLocaleContext.Provider value={{ locale }}>
      {children}
    </ZHLocaleContext.Provider>
  );
}

/** Locale regional efectivo del Design System, o `undefined` si no hay `ZHLocaleProvider`
 * en el árbol — los consumidores deben aplicar su propio fallback. */
export function useZHLocale(): string | undefined {
  const ctx = useContext(ZHLocaleContext);
  return ctx?.locale;
}
