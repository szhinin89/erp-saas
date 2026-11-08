// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { usePurchaseReceptionPage } from "./usePurchaseReceptionPage";
import {
  purchaseReceptionService,
  type BatchDownloadXmlResult,
  type PurchaseReceptionItem,
} from "../api/purchaseReceptionService";
import { message } from "../../../lib/messages";
import { I18nProvider } from "../../../i18n/i18n";
import { buildReceptionRow as row } from "../utils/reception.testFixtures";

vi.mock("../api/purchaseReceptionService", () => ({
  purchaseReceptionService: {
    importTxt: vi.fn(),
    downloadXml: vi.fn(),
    downloadXmlPending: vi.fn(),
    getXmlView: vi.fn(),
    refreshStatus: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn(), confirm: vi.fn() },
}));

beforeEach(() => {
  vi.clearAllMocks();
});

async function importRows(items: PurchaseReceptionItem[]) {
  vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
    items,
    totalParsed: items.length,
    parseErrorCount: 0,
    skippedUnsupportedCount: 0,
  });
  const hook = renderHook(() => usePurchaseReceptionPage(), { wrapper: I18nProvider });
  await act(async () => {
    await hook.result.current.handleFileSelected(new File(["x"], "r.txt"));
  });
  return hook;
}

function okBatch(ids: string[]): BatchDownloadXmlResult {
  return {
    total: ids.length,
    processed: ids.length,
    downloaded: ids.length,
    skipped: 0,
    failed: 0,
    items: ids.map((documentId) => ({
      documentId,
      status: "Downloaded" as const,
      message: "ok",
      documentStatus: "VERIFIED" as const,
      processingStatus: "PROCESSED" as const,
      hasXml: true,
      purchaseId: null,
      expenseExists: false,
      expenseId: null,
      creditNoteExists: false,
      creditNoteId: null,
      cancelledCreditNoteId: null,
      purchaseState: "NONE" as const,
      cancelledPurchaseId: null,
    })),
  };
}

const mixed = [
  row({ documentId: "reg", purchaseState: "CONFIRMED", status: "IMPORTED", documentStatus: "PROCESSED" }),
  row({ documentId: "todo-1" }),
  row({ documentId: "todo-2", documentStatus: "VERIFIED", processingStatus: "PROCESSED" }),
  row({ documentId: "sup", supplierExists: false, supplierId: null, status: "NEW_SUPPLIER" }),
];

describe("PURCHASES-RECEPTION-FILTERS-01 — KPI accionables y conteos", () => {
  it("pulsar una tarjeta filtra su partición; pulsarla de nuevo la desactiva; los KPI no cambian", async () => {
    const { result } = await importRows(mixed);
    const summary = result.current.summary;
    expect(summary).toMatchObject({ registered: 1, toProcess: 2, pendingSupplier: 1 });

    act(() => result.current.toggleSummaryBucket("toProcess"));
    // Tarjeta y selector "Situación ERP" comparten un único estado.
    expect(result.current.filters.erpSituation).toBe("toProcess");
    expect(result.current.items.map((i) => i.documentId).sort()).toEqual(["todo-1", "todo-2"]);
    expect(result.current.total).toBe(summary.toProcess);
    expect(result.current.shownDocuments).toBe(2);
    expect(result.current.allDocuments).toBe(4);
    expect(result.current.summary).toEqual(summary);

    // Elegir otra situación en el selector desactiva la tarjeta: nunca dos situaciones a la vez.
    act(() => result.current.setFilter("erpSituation", "unregistered"));
    expect(result.current.filters.erpSituation).toBe("unregistered");
    expect(result.current.total).toBe(3);

    act(() => result.current.toggleSummaryBucket("pendingSupplier"));
    expect(result.current.items.map((i) => i.documentId)).toEqual(["sup"]);

    act(() => result.current.toggleSummaryBucket("pendingSupplier"));
    expect(result.current.filters.erpSituation).toBe("all");
    expect(result.current.total).toBe(4);
  });

  it("'Mostrando N de M' se calcula antes de paginar y todo cambio de filtro vuelve a la página 1", async () => {
    const many = Array.from({ length: 45 }, (_, i) =>
      row({ documentId: `d-${i}`, supplierName: i < 30 ? "Alfa" : "Beta" }),
    );
    const { result } = await importRows(many);
    act(() => result.current.setPage(3));
    expect(result.current.items).toHaveLength(5);

    act(() => result.current.setFilter("search", "alfa"));
    expect(result.current.page).toBe(1);
    expect(result.current.total).toBe(30);
    expect(result.current.allDocuments).toBe(45);
    expect(result.current.shownDocuments).toBe(30);
    expect(result.current.items).toHaveLength(20);

    act(() => result.current.setPage(2));
    act(() => result.current.setFilter("attentionOnly", true));
    expect(result.current.page).toBe(1);
  });

  it("si el resultado se reduce, la página se ajusta a la última disponible", async () => {
    const many = Array.from({ length: 25 }, (_, i) => row({ documentId: `d-${i}` }));
    const { result } = await importRows([...many, row({ documentId: "last", supplierName: "Zeta Única" })]);
    act(() => result.current.setPage(2));
    act(() => {
      result.current.setFilter("search", "zeta");
      result.current.setPage(2);
    });
    expect(result.current.page).toBe(1);
    expect(result.current.items.map((i) => i.documentId)).toEqual(["last"]);
  });

  it("importar otro TXT limpia los criterios pero conserva el orden elegido", async () => {
    const { result } = await importRows(mixed);
    act(() => {
      result.current.setFilter("docType", "CREDIT_NOTE");
      result.current.setDateSortOrder("asc");
    });
    await act(async () => {
      await result.current.handleFileSelected(new File(["x"], "r2.txt"));
    });
    expect(result.current.filters.docType).toBe("all");
    expect(result.current.dateSortOrder).toBe("asc");
  });
});

describe("PURCHASES-RECEPTION-FILTERS-01 — B2 duplicados", () => {
  it("normaliza por documentId: KPI, total y claves de fila sin contar dos veces", async () => {
    const dup = row({ documentId: "r", purchaseState: "CONFIRMED", status: "IMPORTED" });
    const { result } = await importRows([dup, { ...dup }, row({ documentId: "p" })]);
    expect(result.current.allDocuments).toBe(2);
    expect(result.current.summary.registered).toBe(1);
    expect(result.current.normalization).toEqual({
      duplicatesRemoved: 1,
      conflictingDocumentIds: [],
      invalidIdCount: 0,
    });
    const keys = result.current.items.map((i) => result.current.rowKeys.get(i));
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("datos contradictorios del mismo documento: ambas filas visibles, un solo comprobante", async () => {
    const { result } = await importRows([
      row({ documentId: "x", total: 10 }),
      row({ documentId: "x", total: 20 }),
    ]);
    expect(result.current.total).toBe(2);
    expect(result.current.allDocuments).toBe(1);
    expect(result.current.normalization?.conflictingDocumentIds).toEqual(["x"]);
    // Datos distintos: En revisión, fuera de las tarjetas, contado una vez en el total.
    expect(result.current.summary).toMatchObject({
      registered: 0,
      toProcess: 0,
      pendingSupplier: 0,
      inReview: 1,
      documents: 1,
    });
    // Ambas filas se localizan con Situación ERP → En revisión; ninguna tarjeta las muestra.
    act(() => result.current.setFilter("erpSituation", "review"));
    expect(result.current.total).toBe(2);
    expect(result.current.shownDocuments).toBe(1);
    act(() => result.current.toggleSummaryBucket("toProcess"));
    expect(result.current.total).toBe(0);
    act(() => result.current.setFilter("erpSituation", "unregistered"));
    expect(result.current.total).toBe(0);
    expect(result.current.items.every((i) => result.current.documentBucketOf(i) === "review")).toBe(true);
  });

  it("si las filas repetidas se clasifican distinto, el comprobante queda en revisión fuera de las tarjetas", async () => {
    const { result } = await importRows([
      row({ documentId: "y", purchaseState: "CONFIRMED", status: "IMPORTED" }),
      row({ documentId: "y", supplierExists: false, supplierId: null, status: "NEW_SUPPLIER" }),
      row({ documentId: "z" }),
    ]);
    expect(result.current.summary).toMatchObject({
      registered: 0,
      toProcess: 1,
      pendingSupplier: 0,
      inReview: 1,
      documents: 2,
    });
    act(() => result.current.toggleSummaryBucket("registered"));
    expect(result.current.total).toBe(0);
    act(() => result.current.setFilter("erpSituation", "all"));
    expect(result.current.total).toBe(3);
  });
});

describe("PURCHASES-RECEPTION-FILTERS-01 — B1 descarga XML en lotes", () => {
  const pending = Array.from({ length: 120 }, (_, i) => row({ documentId: `p-${i}` }));

  it("120 pendientes → 50/50/20; el lote fallido queda en error sin marcar ninguno como descargado", async () => {
    vi.mocked(purchaseReceptionService.downloadXmlPending).mockImplementation(async (ids) => {
      if (ids[0] === "p-50") throw new Error("HTTP 502");
      return okBatch(ids);
    });
    const { result } = await importRows(pending);

    await act(async () => {
      await result.current.handleDownloadPendingXml();
    });

    const calls = vi.mocked(purchaseReceptionService.downloadXmlPending).mock.calls;
    expect(calls.map(([ids]) => ids.length)).toEqual([50, 50, 20]);

    const rows = result.current.result!.items;
    const byId = new Map(rows.map((r) => [r.documentId, r]));
    expect(byId.get("p-0")?.documentStatus).toBe("VERIFIED");
    expect(byId.get("p-119")?.documentStatus).toBe("VERIFIED");
    for (let i = 50; i < 100; i++) {
      expect(byId.get(`p-${i}`)?.documentStatus).toBe("IMPORTED");
      expect(result.current.xmlRowState[`p-${i}`]).toBe("error");
    }
    expect(result.current.xmlRowState["p-0"]).toBeUndefined();
    expect(result.current.batchXmlProgress).toEqual({
      status: "warning",
      total: 120,
      processed: 120,
      downloaded: 70,
      skipped: 0,
      failed: 50,
    });
    expect(message.warning).toHaveBeenCalled();
    expect(result.current.batchXmlRunning).toBe(false);

    // Las filas con error de sesión se pueden aislar con el filtro XML.
    act(() => result.current.setFilter("xmlState", "error"));
    expect(result.current.total).toBe(50);
  });

  it("un documento que el backend no confirma no se da por descargado", async () => {
    vi.mocked(purchaseReceptionService.downloadXmlPending).mockImplementation(async (ids) => ({
      ...okBatch(ids.slice(1)),
      total: ids.length,
    }));
    const { result } = await importRows(pending.slice(0, 3));
    await act(async () => {
      await result.current.handleDownloadPendingXml();
    });
    expect(result.current.xmlRowState["p-0"]).toBe("error");
    expect(result.current.result!.items[0].documentStatus).toBe("IMPORTED");
    expect(result.current.xmlRowState["p-1"]).toBeUndefined();
  });

  it("si todas las solicitudes fallan muestra el error del servidor y marca las filas", async () => {
    vi.mocked(purchaseReceptionService.downloadXmlPending).mockRejectedValue({
      response: { data: { message: { user: "Emisión pausada" } } },
    });
    const { result } = await importRows(pending.slice(0, 3));
    await act(async () => {
      await result.current.handleDownloadPendingXml();
    });
    expect(message.error).toHaveBeenCalledWith("Emisión pausada");
    expect(result.current.batchXmlProgress?.status).toBe("error");
    expect(result.current.xmlRowState["p-2"]).toBe("error");
  });

  it("mientras corre el lote no admite otra descarga en lote ni consultas individuales", async () => {
    let release: (value: BatchDownloadXmlResult) => void = () => {};
    vi.mocked(purchaseReceptionService.downloadXmlPending).mockImplementation(
      (ids) =>
        new Promise((resolve) => {
          release = () => resolve(okBatch(ids));
        }),
    );
    const { result } = await importRows(pending.slice(0, 3));

    let running: Promise<void> = Promise.resolve();
    act(() => {
      running = result.current.handleDownloadPendingXml();
    });
    expect(result.current.xmlRowState["p-0"]).toBe("loading");

    await act(async () => {
      await result.current.handleDownloadXml("p-1");
      await (result.current.handleDownloadPendingXml());
    });
    expect(purchaseReceptionService.downloadXml).not.toHaveBeenCalled();
    expect(purchaseReceptionService.downloadXmlPending).toHaveBeenCalledTimes(1);

    await act(async () => {
      release(okBatch([]));
      await running;
    });
    expect(result.current.batchXmlRunning).toBe(false);
  });

  it("excluye del lote las filas que ya se están consultando individualmente", async () => {
    vi.mocked(purchaseReceptionService.downloadXml).mockReturnValue(new Promise(() => {}));
    vi.mocked(purchaseReceptionService.downloadXmlPending).mockImplementation(async (ids) =>
      okBatch(ids),
    );
    const { result } = await importRows(pending.slice(0, 3));
    act(() => {
      void result.current.handleDownloadXml("p-0");
    });
    await act(async () => {
      await result.current.handleDownloadPendingXml();
    });
    expect(vi.mocked(purchaseReceptionService.downloadXmlPending).mock.calls[0][0]).toEqual([
      "p-1",
      "p-2",
    ]);
  });
});
