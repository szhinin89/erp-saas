// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseActionReason } from "./PurchaseActionReason";

afterEach(cleanup);
it("exposes a blocked action's reason on demand without hover or enabling the action", () => {
  render(<I18nProvider><button disabled>Aplicar</button>
    <PurchaseActionReason action="Aplicar" reason="Corrija la cantidad." /></I18nProvider>);
  const trigger = screen.getByRole("button", { name: "Ver motivo: Aplicar" });
  expect(screen.queryByText("Corrija la cantidad.")).toBeNull();
  trigger.focus();
  expect(document.activeElement).toBe(trigger);
  fireEvent.click(trigger, { detail: 0 });
  expect(trigger.getAttribute("aria-expanded")).toBe("true");
  expect(document.getElementById(trigger.getAttribute("aria-controls")!)?.textContent).toBe("Corrija la cantidad.");
  expect((screen.getByRole("button", { name: "Aplicar" }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(trigger);
  expect(screen.queryByText("Corrija la cantidad.")).toBeNull();
});
