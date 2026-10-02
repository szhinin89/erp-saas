// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { RetentionElectronicStatusBadge } from "./RetentionElectronicStatusBadge";
import type { RetentionElectronicStatus } from "../api/retentionsService";

/** ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — presentación compacta del estado calculado por el backend. */
describe("RetentionElectronicStatusBadge", () => {
  afterEach(() => cleanup());

  const cases: Array<[RetentionElectronicStatus, string, string]> = [
    ["Pending", "Pendiente", "badge--neutral"],
    ["Processing", "Procesando", "badge--info"],
    ["Authorized", "Autorizado", "badge--success"],
    ["Rejected", "Rechazado", "badge--error"],
    ["RequiresReconciliation", "Requiere conciliación", "badge--warning"],
    ["Discarded", "Descartado", "badge--neutral"],
  ];

  it.each(cases)("muestra %s como '%s'", (status, label, cls) => {
    render(
      <I18nProvider>
        <RetentionElectronicStatusBadge status={status} />
      </I18nProvider>,
    );

    const badge = screen.getByText(label);
    expect(badge.className).toContain(cls);
  });

  it("no muestra nada si el backend no informó el estado", () => {
    const { container } = render(
      <I18nProvider>
        <RetentionElectronicStatusBadge status={null} />
      </I18nProvider>,
    );

    expect(container.textContent).toBe("");
  });
});
