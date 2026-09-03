// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AdminCoreMenu } from "./AdminCoreMenu";

afterEach(() => {
  cleanup();
});

describe("AdminCoreMenu", () => {
  it("muestra la opción Proveedor SRI bajo Configuración global", () => {
    render(
      <MemoryRouter>
        <AdminCoreMenu onLogout={vi.fn()} />
      </MemoryRouter>,
    );

    expect(screen.getByText("Configuración global")).toBeTruthy();
    const link = screen.getByRole("link", { name: "Proveedor SRI" });
    expect(link.getAttribute("href")).toBe("/admin-core/system-provider-settings");
  });

  /** ZH-ADMINGLOBALCORE-MENU-BOUNDARY-CLEANUP-05O */
  it("mantiene la opción 'Nueva empresa' hacia /admin-core/companies/new", () => {
    render(
      <MemoryRouter>
        <AdminCoreMenu onLogout={vi.fn()} />
      </MemoryRouter>,
    );

    const link = screen.getByRole("link", { name: "Nueva empresa" });
    expect(link.getAttribute("href")).toBe("/admin-core/companies/new");
  });
});
