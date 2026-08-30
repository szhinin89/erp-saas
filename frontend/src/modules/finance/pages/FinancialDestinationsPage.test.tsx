// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { FinancialDestinationsPage } from "./FinancialDestinationsPage";
import {
  financialDestinationService,
  type CompanyFinancialDestinationDto,
} from "../api/financialDestinationService";
import { apiGet } from "../../lib/apiEnvelope";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "Cambiar cuenta contable de destino financiero":
 * confirma antes de guardar cuando cambia AccountingAccountId, mostrando cuenta anterior/nueva y
 * aclarando que no modifica asientos históricos. Si cancela, no guarda. No cambia posting ni
 * reglas contables.
 */

vi.mock("../api/financialDestinationService", () => ({
  financialDestinationService: {
    list: vi.fn(),
    create: vi.fn(),
    rename: vi.fn(),
    changeAccountingAccount: vi.fn(),
    setActive: vi.fn(),
  },
}));

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const DESTINATION: CompanyFinancialDestinationDto = {
  id: "dest-1",
  code: "CAJA-01",
  name: "Caja principal",
  destinationTypeCode: "CashRegister",
  accountingAccountId: "acc-old",
  currencyCode: "USD",
  cashRegisterId: "cr-1",
  bankInstitutionCode: null,
  bankAccountIdentifierNormalized: null,
  isActive: true,
};

const ACCOUNTS = [
  { id: "acc-old", code: "1.1.01", name: "Caja general", allowsPosting: true, isActive: true },
  { id: "acc-new", code: "1.1.02", name: "Caja secundaria", allowsPosting: true, isActive: true },
];

function mockApiGetByUrl() {
  vi.mocked(apiGet).mockImplementation((url: string) => {
    if (url.includes("/accounting/accounts")) return Promise.resolve(ACCOUNTS);
    if (url.includes("/cash-registers")) return Promise.resolve([]);
    return Promise.resolve([]);
  });
}

function renderLastConfirmMessage() {
  const calls = vi.mocked(message.confirm).mock.calls;
  render(
    React.createElement(
      React.Fragment,
      null,
      calls[calls.length - 1][0].message,
    ),
  );
}

async function openEditor() {
  render(
    <I18nProvider>
      <FinancialDestinationsPage />
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.getByText("Caja principal")).toBeTruthy());
  const editBtn = screen
    .getAllByRole("button")
    .find((b) => b.textContent === "edit");
  if (!editBtn) throw new Error("Edit button not found");
  fireEvent.click(editBtn);
  await waitFor(() => expect(screen.getByRole("combobox")).toBeTruthy());
}

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(financialDestinationService.list).mockResolvedValue([DESTINATION]);
  mockApiGetByUrl();
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("FinancialDestinationsPage — cambio de cuenta contable: confirmación y feedback", () => {
  it("pide confirmación mostrando cuenta anterior y nueva cuando cambia la cuenta contable", async () => {
    vi.mocked(financialDestinationService.changeAccountingAccount).mockResolvedValue({
      ...DESTINATION,
      accountingAccountId: "acc-new",
    });
    await openEditor();

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "acc-new" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(financialDestinationService.changeAccountingAccount).toHaveBeenCalledWith(
        "dest-1",
        "acc-new",
      );
    });
    renderLastConfirmMessage();
    expect(screen.getByText(/1\.1\.01/)).toBeTruthy();
    expect(screen.getByText(/1\.1\.02/)).toBeTruthy();
    expect(screen.getByText(/no se modifican/i)).toBeTruthy();
  });

  it("si se cancela, no guarda el cambio de cuenta contable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await openEditor();

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "acc-new" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(financialDestinationService.changeAccountingAccount).not.toHaveBeenCalled();
  });

  it("no pide confirmación si la cuenta contable no cambia", async () => {
    await openEditor();

    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() => expect(message.success).toHaveBeenCalledWith(
      "Destino financiero actualizado correctamente.",
    ));
    expect(message.confirm).not.toHaveBeenCalled();
    expect(financialDestinationService.changeAccountingAccount).not.toHaveBeenCalled();
  });

  it("al confirmar exitosamente muestra message.success", async () => {
    vi.mocked(financialDestinationService.changeAccountingAccount).mockResolvedValue({
      ...DESTINATION,
      accountingAccountId: "acc-new",
    });
    await openEditor();

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "acc-new" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Destino financiero actualizado correctamente."),
    );
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(financialDestinationService.changeAccountingAccount).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La cuenta contable no admite posteo." } },
      },
    });
    await openEditor();

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "acc-new" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() =>
      expect(screen.getByText("La cuenta contable no admite posteo.")).toBeTruthy(),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(financialDestinationService.changeAccountingAccount).mockResolvedValue({
      ...DESTINATION,
      accountingAccountId: "acc-new",
    });

    await openEditor();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "acc-new" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));
    await waitFor(() =>
      expect(financialDestinationService.changeAccountingAccount).toHaveBeenCalled(),
    );

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});

describe("FinancialDestinationsPage — FINANCIAL-DESTINATIONS-STATUS-FIX-01, columna Estado", () => {
  it('un destino activo muestra "Activo" en la columna Estado', async () => {
    vi.mocked(financialDestinationService.list).mockResolvedValue([DESTINATION]);

    render(
      <I18nProvider>
        <FinancialDestinationsPage />
      </I18nProvider>,
    );

    await waitFor(() => expect(screen.getByText("Caja principal")).toBeTruthy());
    expect(screen.getByText("Activo")).toBeTruthy();
  });

  it('un destino inactivo muestra "Inactivo" en la columna Estado', async () => {
    vi.mocked(financialDestinationService.list).mockResolvedValue([
      { ...DESTINATION, isActive: false },
    ]);

    render(
      <I18nProvider>
        <FinancialDestinationsPage />
      </I18nProvider>,
    );

    await waitFor(() => expect(screen.getByText("Caja principal")).toBeTruthy());
    expect(screen.getByText("Inactivo")).toBeTruthy();
  });

  it('no muestra el texto literal incorrecto "Estado" como valor de la fila', async () => {
    vi.mocked(financialDestinationService.list).mockResolvedValue([DESTINATION]);

    render(
      <I18nProvider>
        <FinancialDestinationsPage />
      </I18nProvider>,
    );

    await waitFor(() => expect(screen.getByText("Caja principal")).toBeTruthy());
    // "Estado" solo debe aparecer como encabezado de columna, nunca como valor de celda.
    const headerCell = screen.getByRole("columnheader", { name: "Estado" });
    expect(headerCell).toBeTruthy();
    expect(screen.queryByRole("cell", { name: "Estado" })).toBeNull();
  });
});
