// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataCompanySettingsModal } from "./MasterDataCompanySettingsModal";
import { message } from "../../../lib/messages";
import type {
  BusinessPartnerSummaryDto,
  CompanyBpTradingSettingsDto,
} from "../types/businessPartner.types";

/**
 * CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04 — "Bloquear/desbloquear cliente/proveedor":
 * confirma antes de ejecutar (danger para bloquear, warning para desbloquear), explica el
 * impacto operativo. No cambia el flujo existente de motivo obligatorio para bloquear.
 */

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const PARTNER: BusinessPartnerSummaryDto = {
  id: "bp-1",
  identificationType: "05",
  identificationNumber: "0999999999",
  legalName: "Cliente Uno",
  tradeName: null,
  legalEntityTypeCode: 1,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  isCustomer: true,
  isSupplier: false,
  canAssignAsCustomer: false,
  canAssignAsSupplier: true,
};

const BLOCKED_SETTINGS: CompanyBpTradingSettingsDto = {
  id: "settings-1",
  businessPartnerId: "bp-1",
  creditLimit: 0,
  creditCurrencyCode: "USD",
  paymentDays: 30,
  paymentTermId: null,
  installments: 1,
  daysBetweenInstallments: 0,
  isBlocked: true,
  blockedReason: "Deuda vencida",
  blockedAt: "2026-08-01T00:00:00Z",
  hasCustomConfiguration: true,
};

function renderModal(overrides: {
  initialSettings?: CompanyBpTradingSettingsDto | null;
  onBlock?: (reason: string) => void;
  onUnblock?: () => void;
} = {}) {
  const onBlock = overrides.onBlock ?? vi.fn();
  const onUnblock = overrides.onUnblock ?? vi.fn();
  render(
    <I18nProvider>
      <MasterDataCompanySettingsModal
        partner={PARTNER}
        initialSettings={overrides.initialSettings ?? null}
        saving={false}
        error={null}
        onClose={vi.fn()}
        onSave={vi.fn()}
        onBlock={onBlock}
        onUnblock={onUnblock}
      />
    </I18nProvider>,
  );
  return { onBlock, onUnblock };
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => cleanup());

describe("MasterDataCompanySettingsModal — bloquear: confirmación y feedback", () => {
  it("pide confirmación (danger) con el motivo, antes de llamar onBlock", async () => {
    const { onBlock } = renderModal();

    fireEvent.click(screen.getByRole("button", { name: "Bloquear en esta empresa" }));
    fireEvent.change(screen.getByPlaceholderText(/Deuda vencida/i), {
      target: { value: "Deuda vencida 60 días" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar bloqueo" }));

    await vi.waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(onBlock).toHaveBeenCalledWith("Deuda vencida 60 días");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.variant).toBe("danger");
    expect(String(options.message)).toMatch(/no podrá operar/i);
    expect(String(options.message)).toMatch(/Deuda vencida 60 días/);
  });

  it("si se cancela, no llama onBlock", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { onBlock } = renderModal();

    fireEvent.click(screen.getByRole("button", { name: "Bloquear en esta empresa" }));
    fireEvent.change(screen.getByPlaceholderText(/Deuda vencida/i), {
      target: { value: "Motivo cualquiera" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar bloqueo" }));

    await vi.waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(onBlock).not.toHaveBeenCalled();
  });
});

describe("MasterDataCompanySettingsModal — desbloquear: confirmación y feedback", () => {
  it("pide confirmación (warning) explicando que vuelve a operar normalmente, antes de llamar onUnblock", async () => {
    const { onUnblock } = renderModal({ initialSettings: BLOCKED_SETTINGS });

    fireEvent.click(screen.getByRole("button", { name: "Desbloquear" }));

    await vi.waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(onUnblock).toHaveBeenCalled();
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.variant).toBe("warning");
    expect(String(options.message)).toMatch(/volverá a poder operar normalmente/i);
  });

  it("si se cancela, no llama onUnblock", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { onUnblock } = renderModal({ initialSettings: BLOCKED_SETTINGS });

    fireEvent.click(screen.getByRole("button", { name: "Desbloquear" }));

    await vi.waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(onUnblock).not.toHaveBeenCalled();
  });
});

describe("MasterDataCompanySettingsModal — sin diálogos nativos", () => {
  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    const { onBlock } = renderModal();

    fireEvent.click(screen.getByRole("button", { name: "Bloquear en esta empresa" }));
    fireEvent.change(screen.getByPlaceholderText(/Deuda vencida/i), {
      target: { value: "Motivo" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar bloqueo" }));
    await vi.waitFor(() => expect(onBlock).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
