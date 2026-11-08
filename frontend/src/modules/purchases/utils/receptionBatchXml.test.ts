import { describe, expect, it, vi } from "vitest";
import type {
  BatchDownloadXmlItemResult,
  BatchDownloadXmlResult,
} from "../api/purchaseReceptionService";
import {
  RECEPTION_XML_BATCH_SIZE,
  receptionXmlBatchStatus,
  runReceptionXmlBatches,
} from "./receptionBatchXml";

function okItem(documentId: string): BatchDownloadXmlItemResult {
  return {
    documentId,
    status: "Downloaded",
    message: "ok",
    documentStatus: "VERIFIED",
    processingStatus: "PROCESSED",
    hasXml: true,
    purchaseId: null,
    expenseExists: false,
    expenseId: null,
    creditNoteExists: false,
    creditNoteId: null,
    cancelledCreditNoteId: null,
    purchaseState: "NONE",
    cancelledPurchaseId: null,
  };
}

function okResponse(ids: string[]): BatchDownloadXmlResult {
  return {
    total: ids.length,
    processed: ids.length,
    downloaded: ids.length,
    skipped: 0,
    failed: 0,
    items: ids.map(okItem),
  };
}

const ids = Array.from({ length: 120 }, (_, i) => `doc-${i}`);

describe("runReceptionXmlBatches (B1)", () => {
  it("120 pendientes → solicitudes secuenciales de 50/50/20, nunca simultáneas", async () => {
    let inFlight = 0;
    let maxInFlight = 0;
    const download = vi.fn(async (chunk: string[]) => {
      inFlight++;
      maxInFlight = Math.max(maxInFlight, inFlight);
      await Promise.resolve();
      inFlight--;
      return okResponse(chunk);
    });

    const outcome = await runReceptionXmlBatches(ids, download);

    expect(RECEPTION_XML_BATCH_SIZE).toBe(50);
    expect(download.mock.calls.map(([chunk]) => chunk.length)).toEqual([50, 50, 20]);
    expect(download.mock.calls.flatMap(([chunk]) => chunk)).toEqual(ids);
    expect(maxInFlight).toBe(1);
    expect(outcome).toMatchObject({ total: 120, processed: 120, downloaded: 120, failed: 0 });
    expect(receptionXmlBatchStatus(outcome)).toBe("success");
  });

  it("fallo parcial: un lote rechazado cuenta sus 50 como fallidos, los demás se conservan", async () => {
    const download = vi.fn(async (chunk: string[]) => {
      if (chunk[0] === "doc-50") throw new Error("HTTP 502");
      return okResponse(chunk);
    });
    const done: Array<{ ids: string[]; items: number; error: unknown }> = [];

    const outcome = await runReceptionXmlBatches(ids, download, {
      onChunkDone: (chunk) =>
        done.push({ ids: chunk.ids, items: chunk.items.length, error: chunk.error }),
    });

    expect(download).toHaveBeenCalledTimes(3);
    expect(outcome).toMatchObject({ total: 120, processed: 120, downloaded: 70, failed: 50 });
    expect(outcome.failedRequestIds).toEqual(ids.slice(50, 100));
    expect(outcome.requestErrors).toHaveLength(1);
    // El lote fallido no trae ningún resultado por documento: nada se da por descargado.
    expect(done.map((d) => d.items)).toEqual([50, 0, 20]);
    expect(done[1].error).toBeInstanceOf(Error);
    expect(receptionXmlBatchStatus(outcome)).toBe("warning");
  });

  it("agrega respuestas por documento con omitidos y fallidos del backend", async () => {
    const download = vi.fn(async (chunk: string[]) => ({
      ...okResponse(chunk),
      downloaded: chunk.length - 2,
      skipped: 1,
      failed: 1,
    }));
    const outcome = await runReceptionXmlBatches(ids, download);
    expect(outcome).toMatchObject({ downloaded: 114, skipped: 3, failed: 3 });
  });

  it("todos los lotes fallidos → estado error, sin descargados", async () => {
    const outcome = await runReceptionXmlBatches(ids.slice(0, 60), async () => {
      throw new Error("down");
    });
    expect(outcome.failedRequestIds).toHaveLength(60);
    expect(outcome.downloaded).toBe(0);
    expect(receptionXmlBatchStatus(outcome)).toBe("error");
  });

  it("notifica el inicio de cada lote antes de enviarlo", async () => {
    const started: number[] = [];
    await runReceptionXmlBatches(ids, async (chunk) => okResponse(chunk), {
      onChunkStart: (chunk) => started.push(chunk.length),
    });
    expect(started).toEqual([50, 50, 20]);
  });
});
