import type { ReactElement } from "react";
import { render, type RenderOptions } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";

/** Real application provider; Testing Library preserves it on rerender. */
export function renderSalesUi(ui: ReactElement, options?: Omit<RenderOptions, "wrapper">) {
  return render(ui, {
    ...options,
    wrapper: ({ children }) => <I18nProvider>{children}</I18nProvider>,
  });
}
