// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { useDocumentSequencesPage } from "./useDocumentSequencesPage";
import {
  documentSequencesService,
  type DocumentSequenceDto,
} from "../api/documentSequencesService";
import { emissionPointsService } from "../../emissionPoints/api/emissionPointsService";
import { sriLookupService } from "../../items/catalog/api/catalogService";
import { message } from "../../../lib/messages";

/**
 * DOCUMENT-SEQUENCES-CONFIG-UI-04 — DocumentSequence se identifica por (TenantId, CompanyId,
 * EmissionPointId, DocTypeCode); BranchId y Environment nunca forman parte de la clave. Esta
 * suite cubre: carga de puntos de emisión/tipos soportados, matriz de estado por secuencia,
 * validación local de nextNumber, confirmación antes de guardar, payload exacto enviado al PUT
 * de configuración, refresco tras guardar, bloqueo de secuencias usadas, y manejo de 409/422/404.
 */

vi.mock("../api/documentSequencesService", () => ({
  documentSequencesService: {
    list: vi.fn(),
    configure: vi.fn(),
  },
}));

vi.mock("../../emissionPoints/api/emissionPointsService", () => ({
  emissionPointsService: {
    list: vi.fn(),
  },
}));

vi.mock("../../items/catalog/api/catalogService", () => ({
  sriLookupService: {
    docTypes: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    confirm: vi.fn(),
  },
}));

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
  { code: "04", name: "Nota de Crédito", shortName: "NC", isElectronic: true },
  { code: "07", name: "Retención", shortName: "RET", isElectronic: true },
  // No electrónico — nunca debe aparecer como "soportado" en esta pantalla.
  { code: "99", name: "Físico legado", shortName: "PHY", isElectronic: false },
];

function sequenceDto(
  overrides: Partial<DocumentSequenceDto>,
): DocumentSequenceDto {
  return {
    emissionPointId: "ep-1",
    docTypeCode: "01",
    nextNumber: 1,
    hasBeenUsed: false,
    updatedAt: "2026-08-01T00:00:00Z",
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(emissionPointsService.list).mockResolvedValue([EMISSION_POINT]);
  vi.mocked(sriLookupService.docTypes).mockResolvedValue(DOC_TYPES);
  vi.mocked(documentSequencesService.list).mockResolvedValue([]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

async function renderLoaded() {
  const { result } = renderHook(() => useDocumentSequencesPage());
  await waitFor(() => expect(result.current.loading).toBe(false));
  await waitFor(() =>
    expect(result.current.selectedEmissionPointId).toBe("ep-1"),
  );
  return result;
}

describe("useDocumentSequencesPage — carga inicial", () => {
  it("carga puntos de emisión y selecciona el primero automáticamente", async () => {
    const result = await renderLoaded();
    expect(emissionPointsService.list).toHaveBeenCalledWith("active");
    expect(result.current.emissionPoints).toEqual([EMISSION_POINT]);
  });

  it("muestra solo los tipos de documento electrónicos (soportados) del catálogo SRI", async () => {
    const result = await renderLoaded();
    const codes = result.current.rows.map((r) => r.docTypeCode);
    expect(codes).toEqual(["01", "04", "07"]);
    expect(codes).not.toContain("99");
  });

  it("una combinación sin fila en el backend se muestra como 'sin configurar'", async () => {
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    expect(factura.status).toBe("not_configured");
    expect(factura.nextNumber).toBeNull();
  });
});

describe("useDocumentSequencesPage — estado configurada/usada", () => {
  it("una secuencia configurada pero nunca usada se muestra editable", async () => {
    vi.mocked(documentSequencesService.list).mockResolvedValue([
      sequenceDto({ docTypeCode: "01", nextNumber: 850, hasBeenUsed: false }),
    ]);
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    expect(factura.status).toBe("configured");
    expect(factura.nextNumber).toBe(850);
  });

  it("HasBeenUsed=true bloquea la edición: openConfigure no abre el panel", async () => {
    vi.mocked(documentSequencesService.list).mockResolvedValue([
      sequenceDto({ docTypeCode: "07", nextNumber: 12, hasBeenUsed: true }),
    ]);
    const result = await renderLoaded();
    const retencion = result.current.rows.find((r) => r.docTypeCode === "07")!;
    expect(retencion.status).toBe("used");

    act(() => {
      result.current.openConfigure(retencion);
    });

    expect(result.current.panelOpen).toBe(false);
  });
});

describe("useDocumentSequencesPage — configurar (número inicial)", () => {
  it("abre el panel con el valor por defecto para una secuencia sin configurar", async () => {
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;

    act(() => {
      result.current.openConfigure(factura);
    });

    expect(result.current.panelOpen).toBe(true);
    expect(result.current.editingRow?.docTypeCode).toBe("01");
  });

  it("pide confirmación antes de guardar, con el mensaje esperado", async () => {
    vi.mocked(documentSequencesService.configure).mockResolvedValue(
      sequenceDto({ docTypeCode: "01", nextNumber: 850 }),
    );
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    act(() => result.current.openConfigure(factura));

    act(() => {
      result.current.register("nextNumber").onChange({
        target: { name: "nextNumber", value: "850" },
      } as never);
    });

    await act(async () => {
      await result.current.save();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    const confirmArg = vi.mocked(message.confirm).mock.calls[0][0];
    expect(confirmArg.message).toContain("000000850");
    expect(confirmArg.message).toContain("Factura");
    expect(confirmArg.message).toContain("001-001");
  });

  it("no llama a la API si el usuario cancela la confirmación", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    act(() => result.current.openConfigure(factura));
    act(() => {
      result.current.register("nextNumber").onChange({
        target: { name: "nextNumber", value: "850" },
      } as never);
    });

    await act(async () => {
      await result.current.save();
    });

    expect(documentSequencesService.configure).not.toHaveBeenCalled();
  });

  it("llama PUT /settings/document-sequences/configure con emissionPointId, docTypeCode y nextNumber — nunca branchId", async () => {
    vi.mocked(documentSequencesService.configure).mockResolvedValue(
      sequenceDto({ docTypeCode: "01", nextNumber: 850 }),
    );
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    act(() => result.current.openConfigure(factura));
    act(() => {
      result.current.register("nextNumber").onChange({
        target: { name: "nextNumber", value: "850" },
      } as never);
    });

    await act(async () => {
      await result.current.save();
    });

    expect(documentSequencesService.configure).toHaveBeenCalledWith({
      emissionPointId: "ep-1",
      docTypeCode: "01",
      nextNumber: 850,
    });
    const payload = vi.mocked(documentSequencesService.configure).mock
      .calls[0][0];
    expect(payload).not.toHaveProperty("branchId");
    expect(Object.keys(payload).sort()).toEqual(
      ["docTypeCode", "emissionPointId", "nextNumber"].sort(),
    );
  });

  it("al guardar correctamente refresca la lista, muestra éxito y cierra el panel", async () => {
    vi.mocked(documentSequencesService.configure).mockResolvedValue(
      sequenceDto({ docTypeCode: "01", nextNumber: 850 }),
    );
    const result = await renderLoaded();
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    act(() => result.current.openConfigure(factura));
    act(() => {
      result.current.register("nextNumber").onChange({
        target: { name: "nextNumber", value: "850" },
      } as never);
    });

    await act(async () => {
      await result.current.save();
    });

    expect(documentSequencesService.list).toHaveBeenCalledTimes(2); // carga inicial + refresco
    expect(message.success).toHaveBeenCalledWith(
      "Secuencia documental configurada correctamente.",
    );
    expect(result.current.panelOpen).toBe(false);
  });
});

describe("useDocumentSequencesPage — validación local de nextNumber", () => {
  it.each([0, -1, 1_000_000_000])(
    "rechaza %i sin llamar a la API",
    async (invalidValue) => {
      const result = await renderLoaded();
      const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
      act(() => result.current.openConfigure(factura));
      act(() => {
        result.current.register("nextNumber").onChange({
          target: { name: "nextNumber", value: String(invalidValue) },
        } as never);
      });

      await act(async () => {
        await result.current.save();
      });

      expect(documentSequencesService.configure).not.toHaveBeenCalled();
      expect(message.confirm).not.toHaveBeenCalled();
    },
  );
});

type HookResult = ReturnType<typeof useDocumentSequencesPage>;

describe("useDocumentSequencesPage — manejo de errores del backend", () => {
  async function openAndSubmit(result: { current: HookResult }) {
    const factura = result.current.rows.find((r) => r.docTypeCode === "01")!;
    act(() => result.current.openConfigure(factura));
    act(() => {
      result.current.register("nextNumber").onChange({
        target: { name: "nextNumber", value: "850" },
      } as never);
    });
    await act(async () => {
      await result.current.save();
    });
  }

  it("409 muestra un mensaje claro de secuencia ya usada", async () => {
    vi.mocked(documentSequencesService.configure).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: {} },
    });
    const result = await renderLoaded();
    await openAndSubmit(result);

    expect(result.current.saveError).toBe(
      "La secuencia ya fue usada y no puede modificarse desde esta pantalla.",
    );
    expect(result.current.panelOpen).toBe(true); // no cierra el panel en error
  });

  it("404 muestra un mensaje claro de punto de emisión inexistente", async () => {
    vi.mocked(documentSequencesService.configure).mockRejectedValue({
      isAxiosError: true,
      response: { status: 404, data: {} },
    });
    const result = await renderLoaded();
    await openAndSubmit(result);

    expect(result.current.saveError).toBe(
      "El punto de emisión no existe o no pertenece a la empresa activa.",
    );
  });

  it("422 muestra la validación devuelta por el servidor", async () => {
    vi.mocked(documentSequencesService.configure).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: { nextNumber: ["Valor fuera de rango."] } } },
      },
    });
    const result = await renderLoaded();
    await openAndSubmit(result);

    expect(result.current.errors.nextNumber?.message).toBe(
      "Valor fuera de rango.",
    );
  });
});
