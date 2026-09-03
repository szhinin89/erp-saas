// @vitest-environment jsdom
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AdminCoreMenu } from "./AdminCoreMenu";

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
});
