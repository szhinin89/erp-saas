// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import type {
  RideGenerationResultDto,
  RideOutcome,
} from "../../ride/facades/rideGenerationFacade";

const getOrGenerateMock = vi.fn();
const regenerateMock = vi.fn();
const getContentBlobMock = vi.fn();

vi.mock("../../ride/facades/rideGenerationFacade", () => ({
  rideGenerationFacade: {
    getOrGenerate: (...a: unknown[]) => getOrGenerateMock(...a),
    regenerate: (...a: unknown[]) => regenerateMock(...a),
    getContentBlob: (...a: unknown[]) => getContentBlobMock(...a),
  },
}));

const downloadBlobMock = vi.fn();
vi.mock("../../ride/utils/downloadBlob", () => ({
  downloadBlob: (...a: unknown[]) => downloadBlobMock(...a),
}));

const messageSuccessMock = vi.fn();
const messageWarningMock = vi.fn();
const messageErrorMock = vi.fn();
vi.mock("../../../lib/messages", () => ({
  message: {
    success: (...a: unknown[]) => messageSuccessMock(...a),
    warning: (...a: unknown[]) => messageWarningMock(...a),
    error: (...a: unknown[]) => messageErrorMock(...a),
  },
}));

import { useRideActions } from "./useRideActions";

function outcomeResult(outcome: RideOutcome): RideGenerationResultDto {
  return {
    outcome,
    storagePath:
      outcome === "Generated" || outcome === "Cached" ? "ride/path.pdf" : null,
    metadata: null,
    reasonCode: null,
  };
}

describe("useRideActions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(globalThis.URL, "createObjectURL", {
      value: vi.fn(() => "blob:mock"),
      writable: true,
    });
    Object.defineProperty(globalThis.URL, "revokeObjectURL", {
      value: vi.fn(),
      writable: true,
    });
    vi.stubGlobal("open", vi.fn());
  });

  describe("handleViewRide", () => {
    it("Generated: obtiene el contenido y abre una pestaña nueva, sin mensajes", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("Generated"));
      getContentBlobMock.mockResolvedValue(new Blob());
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(getContentBlobMock).toHaveBeenCalledWith("Sales", "doc-1");
      expect(window.open).toHaveBeenCalled();
      expect(messageWarningMock).not.toHaveBeenCalled();
      expect(messageErrorMock).not.toHaveBeenCalled();
    });

    it("Cached: reutiliza el PDF existente (misma ruta: obtener contenido y abrir)", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("Cached"));
      getContentBlobMock.mockResolvedValue(new Blob());
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(getContentBlobMock).toHaveBeenCalledWith("Sales", "doc-1");
      expect(window.open).toHaveBeenCalled();
    });

    it("PendingSource: muestra advertencia y nunca pide el contenido", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("PendingSource"));
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(getContentBlobMock).not.toHaveBeenCalled();
      expect(messageWarningMock).toHaveBeenCalledTimes(1);
    });

    it("NotApplicable: muestra advertencia y nunca pide el contenido", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("NotApplicable"));
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(getContentBlobMock).not.toHaveBeenCalled();
      expect(messageWarningMock).toHaveBeenCalledTimes(1);
    });

    it("Failed: muestra error y nunca pide el contenido", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("Failed"));
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(getContentBlobMock).not.toHaveBeenCalled();
      expect(messageErrorMock).toHaveBeenCalledTimes(1);
    });

    it("un error HTTP real nunca se muestra sin manejar — usa e.message, nunca la excepción cruda/stack", async () => {
      getOrGenerateMock.mockRejectedValue(new Error("Network Error"));
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(messageErrorMock).toHaveBeenCalledTimes(1);
      expect(messageErrorMock).toHaveBeenCalledWith("Network Error");
    });

    it("un error sin response ni message cae en el mensaje de fallback en español", async () => {
      getOrGenerateMock.mockRejectedValue({});
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(messageErrorMock).toHaveBeenCalledWith(
        "No se pudo obtener el RIDE.",
      );
    });

    it("usa el mensaje de negocio devuelto por el backend en un 422/400 en vez del fallback genérico", async () => {
      getOrGenerateMock.mockRejectedValue({
        response: {
          data: { message: { user: "Mensaje específico del backend." } },
        },
      });
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleViewRide("doc-1");
      });

      expect(messageErrorMock).toHaveBeenCalledWith(
        "Mensaje específico del backend.",
      );
    });
  });

  describe("handleDownloadRide", () => {
    it("Generated: descarga el blob con un nombre de archivo basado en el número de factura", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("Generated"));
      const blob = new Blob();
      getContentBlobMock.mockResolvedValue(blob);
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleDownloadRide("doc-1", "001-001-000000123");
      });

      expect(downloadBlobMock).toHaveBeenCalledWith(
        blob,
        "RIDE-001-001-000000123.pdf",
      );
    });

    it("NotApplicable: no descarga nada", async () => {
      getOrGenerateMock.mockResolvedValue(outcomeResult("NotApplicable"));
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleDownloadRide("doc-1");
      });

      expect(downloadBlobMock).not.toHaveBeenCalled();
    });
  });

  describe("handleRegenerateRide", () => {
    it("Generated: muestra éxito y abre el PDF fresco", async () => {
      regenerateMock.mockResolvedValue(outcomeResult("Generated"));
      getContentBlobMock.mockResolvedValue(new Blob());
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleRegenerateRide("doc-1");
      });

      expect(regenerateMock).toHaveBeenCalledWith("Sales", "doc-1");
      expect(messageSuccessMock).toHaveBeenCalledTimes(1);
      expect(window.open).toHaveBeenCalled();
    });

    it("Failed: muestra error, nunca éxito", async () => {
      regenerateMock.mockResolvedValue(outcomeResult("Failed"));
      const { result } = renderHook(() => useRideActions());

      await act(async () => {
        await result.current.handleRegenerateRide("doc-1");
      });

      expect(messageSuccessMock).not.toHaveBeenCalled();
      expect(messageErrorMock).toHaveBeenCalledTimes(1);
    });
  });
});
