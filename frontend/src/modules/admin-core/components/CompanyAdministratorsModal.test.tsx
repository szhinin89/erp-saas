// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { adminCoreService } from "../api/adminCoreService";
import { CompanyAdministratorsModal } from "./CompanyAdministratorsModal";
import type { AdminCoreCompany } from "../../../types/adminCore";

vi.mock("../api/adminCoreService", () => ({
  adminCoreService: {
    listCompanyAdministrators: vi.fn(),
    regenerateAdministratorPassword: vi.fn(),
  },
}));

const COMPANY: AdminCoreCompany = {
  tenantId: "t-1",
  tenantName: "Grupo",
  tenantIsActive: true,
  companyId: "c-1",
  ruc: "1790016919001",
  legalName: "Acme S.A.",
  tradeName: null,
  isActive: true,
};

const ADMIN = {
  identityUserId: "u-1",
  username: "acme.admin",
  fullName: "Ana Pérez",
  email: "ana@example.test",
  passwordResetPending: true,
};

beforeEach(() => {
  vi.mocked(adminCoreService.listCompanyAdministrators).mockResolvedValue([ADMIN]);
  vi.mocked(adminCoreService.regenerateAdministratorPassword).mockResolvedValue({
    identityUserId: "u-1",
    username: "acme.admin",
    temporaryPassword: "Nw4kZ8vTq2LmXp7R",
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CompanyAdministratorsModal", () => {
  it("lista los administradores y exige confirmación antes de generar la contraseña", async () => {
    render(<CompanyAdministratorsModal company={COMPANY} onClose={() => {}} />);

    expect(await screen.findByText("Ana Pérez", { exact: false })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Generar nueva contraseña temporal" }));

    // Sin confirmar no se llama al backend.
    expect(screen.getByText(/La contraseña actual quedará invalidada/)).toBeTruthy();
    expect(adminCoreService.regenerateAdministratorPassword).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Generar contraseña" }));

    await waitFor(() =>
      expect(adminCoreService.regenerateAdministratorPassword).toHaveBeenCalledWith("c-1", "u-1"),
    );
    expect((await screen.findByTestId("credentials-password")).textContent).toBe("Nw4kZ8vTq2LmXp7R");
    expect(screen.getByRole("button", { name: "Copiar contraseña" })).toBeTruthy();
  });

  it("cancelar la confirmación no genera nada", async () => {
    render(<CompanyAdministratorsModal company={COMPANY} onClose={() => {}} />);

    await screen.findByText("Ana Pérez", { exact: false });
    fireEvent.click(screen.getByRole("button", { name: "Generar nueva contraseña temporal" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(adminCoreService.regenerateAdministratorPassword).not.toHaveBeenCalled();
    expect(screen.queryByTestId("credentials-password")).toBeNull();
  });

  it("al cerrar y reabrir el modal la contraseña ya no puede consultarse", async () => {
    const { rerender } = render(<CompanyAdministratorsModal company={COMPANY} onClose={() => {}} />);

    await screen.findByText("Ana Pérez", { exact: false });
    fireEvent.click(screen.getByRole("button", { name: "Generar nueva contraseña temporal" }));
    fireEvent.click(screen.getByRole("button", { name: "Generar contraseña" }));
    await screen.findByTestId("credentials-password");

    rerender(<CompanyAdministratorsModal company={null} onClose={() => {}} />);
    expect(screen.queryByText("Nw4kZ8vTq2LmXp7R")).toBeNull();

    rerender(<CompanyAdministratorsModal company={COMPANY} onClose={() => {}} />);
    await screen.findByText("Ana Pérez", { exact: false });
    expect(screen.queryByTestId("credentials-password")).toBeNull();
    expect(screen.queryByText("Nw4kZ8vTq2LmXp7R")).toBeNull();
  });

  it("muestra el error si falla la generación y no expone ninguna contraseña", async () => {
    vi.mocked(adminCoreService.regenerateAdministratorPassword).mockRejectedValue(new Error("boom"));
    render(<CompanyAdministratorsModal company={COMPANY} onClose={() => {}} />);

    await screen.findByText("Ana Pérez", { exact: false });
    fireEvent.click(screen.getByRole("button", { name: "Generar nueva contraseña temporal" }));
    fireEvent.click(screen.getByRole("button", { name: "Generar contraseña" }));

    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(screen.queryByTestId("credentials-password")).toBeNull();
  });
});
