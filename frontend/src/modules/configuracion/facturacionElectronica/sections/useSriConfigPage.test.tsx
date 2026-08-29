// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../../i18n/i18n";
import { useSriConfigPage } from "./useSriConfigPage";
import { electronicInvoicingService } from "../api/electronicInvoicingService";
import { message } from "../../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "SMTP/SRI": la carga de certificado SRI no
 * mostraba feedback de éxito claro (solo actualizaba el nombre de archivo silenciosamente).
 * Se agrega message.success tras una carga exitosa. No cambia cifrado, payloads, endpoints ni
 * validaciones — no se muestra el contenido del certificado ni la contraseña en el mensaje.
 */

vi.mock("../api/electronicInvoicingService", () => ({
  electronicInvoicingService: {
    getSriConfiguration: vi.fn(),
    upsertSriConfiguration: vi.fn(),
    validateSriConfiguration: vi.fn(),
    uploadCertificate: vi.fn(),
    inspectCertificate: vi.fn(),
    getStatus: vi.fn().mockResolvedValue({ hasCertificate: false }),
  },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

function makeFile(name = "certificado.p12", size = 1024) {
  const file = new File([new Uint8Array(size)], name, {
    type: "application/x-pkcs12",
  });
  return file;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(electronicInvoicingService.getSriConfiguration).mockResolvedValue(null);
});

describe("useSriConfigPage — carga de certificado: feedback de éxito", () => {
  it("al subir exitosamente muestra message.success", async () => {
    vi.mocked(electronicInvoicingService.uploadCertificate).mockResolvedValue({
      fileName: "certificado.p12",
      sizeBytes: 1024,
      uploadedAtUtc: "2026-08-28T00:00:00Z",
      inspection: null,
    });
    const { result } = renderHook(() => useSriConfigPage(), { wrapper });
    await waitFor(() => expect(result.current.sriState.loading).toBe(false));

    await act(async () => {
      await result.current.handleCertFileSelected(makeFile());
    });

    expect(electronicInvoicingService.uploadCertificate).toHaveBeenCalled();
    expect(message.success).toHaveBeenCalledWith("Certificado cargado correctamente.");
  });

  it("si falla, no muestra éxito — el error real se expone vía certUploadError", async () => {
    vi.mocked(electronicInvoicingService.uploadCertificate).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 400,
        data: { message: { user: "El archivo no es un certificado válido." } },
      },
    });
    const { result } = renderHook(() => useSriConfigPage(), { wrapper });
    await waitFor(() => expect(result.current.sriState.loading).toBe(false));

    await act(async () => {
      await result.current.handleCertFileSelected(makeFile());
    });

    expect(message.success).not.toHaveBeenCalled();
    expect(result.current.certUploadError).toBe("El archivo no es un certificado válido.");
  });
});
