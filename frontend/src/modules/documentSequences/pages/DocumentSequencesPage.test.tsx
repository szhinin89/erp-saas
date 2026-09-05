// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { DocumentSequencesPage } from "./DocumentSequencesPage";
import {
  documentSequencesService,
  type DocumentSequenceDto,
} from "../api/documentSequencesService";
import { emissionPointsService } from "../../emissionPoints/api/emissionPointsService";
import { sriLookupService } from "../../items/catalog/api/catalogService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

/**
 * DOCUMENT-SEQUENCES-CONFIG-UI-04 — pantalla de Settings ("Secuencias documentales") para
 * configurar el número inicial por establecimiento/punto de emisión/tipo de documento SRI. Cubre
 * el flujo end-to-end vía DOM real (List→Editor de ConfigTabsLayout): 1) la pantalla renderiza,
 * 2) carga puntos de emisión, 3) muestra los tipos de documento soportados, 4/6/7) configurar con
 * confirmación y payload correcto, 9) bloqueo si ya fue usada.
 */

vi.mock("../api/documentSequencesService", () => ({
  documentSequencesService: { list: vi.fn(), configure: vi.fn() },
}));

vi.mock("../../emissionPoints/api/emissionPointsService", () => ({
  emissionPointsService: { list: vi.fn() },
}));

vi.mock("../../items/catalog/api/catalogService", () => ({
  sriLookupService: { docTypes: vi.fn() },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    confirm: vi.fn(),
  },
}));

function grant() {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <DocumentSequencesPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

const EMISSION_POINT = {
  id: "ep-1",
  establishmentId: "est-1",
  establishmentCode: "001",
  establishmentName: "Matriz",
  branchName: null,
  code: "001",
  name: "Punto principal",
  emissionType: "Electronic" as const,
  isDefault: true,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

const DOC_TYPES = [
  { code: "01", name: "Factura", shortName: "FAC", isElectronic: true },
  { code: "07", name: "Retención", shortName: "RET", isElectronic: true },
];

function sequenceDto(overrides: Partial<DocumentSequenceDto>): DocumentSequenceDto {
  return {
    emissionPointId: "ep-1",
    docTypeCode: "01",
    nextNumber: 1,
    hasBeenUsed: false,
    updatedAt: "2026-08-01T00:00:00Z",
    ...overrides,
  };
}

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  grant();
  vi.mocked(emissionPointsService.list).mockResolvedValue([EMISSION_POINT]);
  vi.mocked(sriLookupService.docTypes).mockResolvedValue(DOC_TYPES);
  vi.mocked(documentSequencesService.list).mockResolvedValue([]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("DocumentSequencesPage — renderizado y carga", () => {
  it("renderiza la pantalla de secuencias documentales", async () => {
    renderPage();
    expect(await screen.findAllByText("Secuencias documentales")).not.toHaveLength(0);
  });

  it("carga los puntos de emisión activos y los muestra en el selector", async () => {
    renderPage();
    await waitFor(() => expect(emissionPointsService.list).toHaveBeenCalledWith("active"));
    expect(await screen.findByText(/001-001/)).toBeTruthy();
  });

  it("muestra los tipos de documento soportados con su código SRI", async () => {
    renderPage();
    expect(await screen.findByText("Factura")).toBeTruthy();
    expect(screen.getByText("Retención")).toBeTruthy();
    expect(screen.getByText("01")).toBeTruthy();
    expect(screen.getByText("07")).toBeTruthy();
  });

  it("nunca crea ni referencia una pantalla propia de Retenciones", async () => {
    renderPage();
    await screen.findByText("Factura");
    // "Retención" aparece únicamente como fila de la matriz de tipos de documento SRI —
    // no hay ningún enlace/navegación a una pantalla de Retenciones en esta página.
    expect(screen.queryByRole("link", { name: /retenci/i })).toBeNull();
  });
});

describe("DocumentSequencesPage — configurar número inicial", () => {
  it("permite configurar una secuencia sin uso, confirma y envía el payload correcto", async () => {
    vi.mocked(documentSequencesService.configure).mockResolvedValue(
      sequenceDto({ nextNumber: 850 }),
    );
    renderPage();
    await screen.findByText("Factura");

    const row = screen.getByText("Factura").closest("tr")!;
    fireEvent.click(within(row).getByRole("button", { name: /configurar/i }));

    const numberInput = await screen.findByLabelText(/Siguiente secuencial/i);
    fireEvent.change(numberInput, { target: { value: "850" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(documentSequencesService.configure).toHaveBeenCalledWith({
        emissionPointId: "ep-1",
        docTypeCode: "01",
        nextNumber: 850,
      }),
    );
    expect(message.success).toHaveBeenCalledWith(
      "Secuencia documental configurada correctamente.",
    );
  });

  it("una secuencia ya usada se muestra bloqueada, sin botón de edición", async () => {
    vi.mocked(documentSequencesService.list).mockResolvedValue([
      sequenceDto({ docTypeCode: "07", nextNumber: 12, hasBeenUsed: true }),
    ]);
    renderPage();
    await screen.findByText("Retención");

    const row = screen.getByText("Retención").closest("tr")!;
    expect(within(row).queryByRole("button", { name: /configurar|editar/i })).toBeNull();
    expect(within(row).getByTitle(/bloqueado/i)).toBeTruthy();
  });

  it("409 muestra el mensaje claro de secuencia ya usada", async () => {
    vi.mocked(documentSequencesService.configure).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: {} },
    });
    renderPage();
    await screen.findByText("Factura");

    const row = screen.getByText("Factura").closest("tr")!;
    fireEvent.click(within(row).getByRole("button", { name: /configurar/i }));
    const numberInput = await screen.findByLabelText(/Siguiente secuencial/i);
    fireEvent.change(numberInput, { target: { value: "850" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() =>
      expect(
        screen.getByText(
          "La secuencia ya fue usada y no puede modificarse desde esta pantalla.",
        ),
      ).toBeTruthy(),
    );
  });
});

