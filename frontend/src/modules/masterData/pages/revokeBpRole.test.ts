import { beforeEach, describe, expect, it, vi } from "vitest";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { message } from "../../../lib/messages";
import { revokeBpContextRole } from "./revokeBpRole";
import { CUSTOMER_ROLE, SUPPLIER_ROLE, makeBp } from "./bpDetailTestFixtures";

/** Revocación del rol del módulo desde el listado (reemplaza la pestaña «Roles» de la ficha). API simulada. */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: { getBusinessPartner: vi.fn(), revokeRole: vi.fn() },
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
  vi.mocked(businessPartnerFacade.revokeRole).mockResolvedValue(true);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("revokeBpContextRole", () => {
  it("pide confirmación (el socio no se elimina) y revoca solo el rol del contexto", async () => {
    await expect(revokeBpContextRole("supplier", "bp-1")).resolves.toBe(true);
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de operar bajo el rol "Proveedor"/);
    expect(String(options.message)).toMatch(/no se elimina/i);
    expect(businessPartnerFacade.revokeRole).toHaveBeenCalledExactlyOnceWith("bp-1", SUPPLIER_ROLE.id);
    expect(message.success).toHaveBeenCalledWith('Rol "Proveedor" revocado correctamente.');
  });

  it("en contexto cliente revoca el rol Cliente", async () => {
    await revokeBpContextRole("customer", "bp-1");
    expect(businessPartnerFacade.revokeRole).toHaveBeenCalledExactlyOnceWith("bp-1", CUSTOMER_ROLE.id);
  });

  it("si se cancela no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await expect(revokeBpContextRole("customer", "bp-1")).resolves.toBe(false);
    expect(businessPartnerFacade.revokeRole).not.toHaveBeenCalled();
  });

  it("si falla muestra el error y no anuncia éxito", async () => {
    vi.mocked(businessPartnerFacade.revokeRole).mockRejectedValue(new Error("boom"));
    await expect(revokeBpContextRole("customer", "bp-1")).resolves.toBe(false);
    expect(message.error).toHaveBeenCalled();
    expect(message.success).not.toHaveBeenCalled();
  });

  it("sin rol activo en el contexto no hace nada", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([SUPPLIER_ROLE]));
    await expect(revokeBpContextRole("customer", "bp-1")).resolves.toBe(false);
    expect(message.confirm).not.toHaveBeenCalled();
    expect(businessPartnerFacade.revokeRole).not.toHaveBeenCalled();
  });
});
