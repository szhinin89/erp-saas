// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { AxiosError, AxiosHeaders } from "axios";
import { I18nProvider } from "../../../i18n/i18n";
import { PhysicalDocumentAuthorizationsSection } from "./PhysicalDocumentAuthorizationsSection";
import {
  physicalDocumentAuthorizationsService,
  type PhysicalDocumentAuthorizationDto,
} from "../api/physicalDocumentAuthorizationsService";
import { emissionPointLookupFacade } from "../../emissionPoints/facades/emissionPointLookupFacade";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

/**
 * PHYSICAL-FISCAL-F1 — registro y baja de autorizaciones de impresión del SRI de los puntos
 * físicos, destino de los requisitos `physicalEmission.*` de Readiness.
 */

vi.mock("../api/physicalDocumentAuthorizationsService", () => ({
  physicalDocumentAuthorizationsService: { list: vi.fn(), create: vi.fn(), disable: vi.fn() },
}));
vi.mock("../../emissionPoints/facades/emissionPointLookupFacade", () => ({
  emissionPointLookupFacade: { list: vi.fn() },
}));
vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: { docTypes: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), confirm: vi.fn() },
}));

const POINT = {
  establishmentId: "est-1",
  establishmentCode: "001",
  establishmentName: "Matriz",
  branchName: null,
  name: null,
  isDefault: false,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};
const PHYSICAL = { ...POINT, id: "ep-phys", code: "002", emissionType: "Physical" as const };
const ELECTRONIC = { ...POINT, id: "ep-elec", code: "001", emissionType: "Electronic" as const };

const AUTHORIZATION: PhysicalDocumentAuthorizationDto = {
  id: "auth-1",
  emissionPointId: "ep-phys",
  docTypeCode: "01",
  authorizationNumber: "1122334455",
  authorizedOn: "2026-01-15",
  expiresOn: "2027-01-14",
  firstSequential: 1,
  lastSequential: 500,
  isActive: true,
};

function grant(allowed = true) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => allowed,
    has: () => allowed,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function renderSection() {
  return render(
    <I18nProvider>
      <PhysicalDocumentAuthorizationsSection />
    </I18nProvider>,
  );
}

function fill(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(new RegExp(label)), { target: { value } });
}

describe("PhysicalDocumentAuthorizationsSection", () => {
  beforeEach(() => {
    grant();
    vi.mocked(emissionPointLookupFacade.list).mockResolvedValue([ELECTRONIC, PHYSICAL]);
    vi.mocked(sriLookupFacade.docTypes).mockResolvedValue([
      { code: "01", name: "Factura", shortName: "FAC", isElectronic: true },
    ]);
    vi.mocked(physicalDocumentAuthorizationsService.list).mockResolvedValue([AUTHORIZATION]);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("lista solo los puntos físicos y las autorizaciones del punto seleccionado", async () => {
    renderSection();

    expect(await screen.findByText("1122334455")).toBeTruthy();
    const select = screen.getByLabelText("Punto de emisión físico") as HTMLSelectElement;
    expect(Array.from(select.options).map((o) => o.value)).toEqual(["ep-phys"]);
    expect(screen.getByText("000000001 – 000000500")).toBeTruthy();
    expect(screen.getByText("Activa")).toBeTruthy();
  });

  it("registra la autorización con el punto seleccionado y fechas de negocio", async () => {
    vi.mocked(physicalDocumentAuthorizationsService.create).mockResolvedValue(AUTHORIZATION);
    renderSection();
    await screen.findByText("1122334455");

    fireEvent.change(screen.getByLabelText(/Tipo de comprobante/), { target: { value: "01" } });
    fill("N.º de autorización", "9988776655");
    fill("Fecha de autorización", "2027-01-15");
    fill("Fecha de caducidad", "2028-01-14");
    fill("Primer secuencial", "501");
    fill("Último secuencial", "1000");
    fireEvent.click(screen.getByRole("button", { name: "Registrar autorización" }));

    await waitFor(() =>
      expect(physicalDocumentAuthorizationsService.create).toHaveBeenCalledWith({
        emissionPointId: "ep-phys",
        docTypeCode: "01",
        authorizationNumber: "9988776655",
        authorizedOn: "2027-01-15",
        expiresOn: "2028-01-14",
        firstSequential: 501,
        lastSequential: 1000,
      }),
    );
    expect(message.success).toHaveBeenCalled();
  });

  it("no envía una vigencia mayor a un año ni un rango invertido", async () => {
    renderSection();
    await screen.findByText("1122334455");

    fireEvent.change(screen.getByLabelText(/Tipo de comprobante/), { target: { value: "01" } });
    fill("N.º de autorización", "9988776655");
    fill("Fecha de autorización", "2027-01-15");
    fill("Fecha de caducidad", "2028-01-16");
    fill("Primer secuencial", "10");
    fill("Último secuencial", "5");
    fireEvent.click(screen.getByRole("button", { name: "Registrar autorización" }));

    expect(await screen.findByText(/dentro del año siguiente/)).toBeTruthy();
    expect(screen.getByText(/no puede ser menor que el primero/)).toBeTruthy();
    expect(physicalDocumentAuthorizationsService.create).not.toHaveBeenCalled();
  });

  it("informa el rango superpuesto que rechaza el backend", async () => {
    vi.mocked(physicalDocumentAuthorizationsService.create).mockRejectedValue(
      new AxiosError("conflict", "409", undefined, undefined, {
        status: 409,
        statusText: "Conflict",
        data: {},
        headers: {},
        config: { headers: new AxiosHeaders() },
      }),
    );
    renderSection();
    await screen.findByText("1122334455");

    fireEvent.change(screen.getByLabelText(/Tipo de comprobante/), { target: { value: "01" } });
    fill("N.º de autorización", "9988776655");
    fill("Fecha de autorización", "2027-01-15");
    fill("Fecha de caducidad", "2028-01-14");
    fill("Primer secuencial", "400");
    fill("Último secuencial", "600");
    fireEvent.click(screen.getByRole("button", { name: "Registrar autorización" }));

    expect(await screen.findByText(/se superpone con otra autorización activa/)).toBeTruthy();
  });

  it("deshabilita con confirmación y conserva el registro", async () => {
    vi.mocked(message.confirm).mockResolvedValue(true);
    vi.mocked(physicalDocumentAuthorizationsService.disable).mockResolvedValue(true);
    renderSection();
    await screen.findByText("1122334455");

    fireEvent.click(screen.getByRole("button", { name: /Deshabilitar/ }));

    await waitFor(() =>
      expect(physicalDocumentAuthorizationsService.disable).toHaveBeenCalledWith("auth-1"),
    );
  });

  it("sin permiso no consulta ni muestra nada", () => {
    grant(false);
    const { container } = renderSection();

    expect(container.textContent).toBe("");
    expect(physicalDocumentAuthorizationsService.list).not.toHaveBeenCalled();
  });
});
