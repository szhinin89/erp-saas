// @vitest-environment jsdom
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useSalesPage } from "./useSalesPage";
import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { salesItemPricingService } from "../api/salesItemPricingService";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
}));
vi.mock("../../caja/facades/manualCashMovementFacade", () => ({
  useManualCashMovementFlow: () => ({}),
}));
vi.mock("../../../lib/messages", () => ({
  message: {
    error: vi.fn(),
    warning: vi.fn(),
    success: vi.fn(),
    confirm: vi.fn().mockResolvedValue(true),
  },
}));
const item = (id: string): InvoiceItemSearchResultDto => ({
  id,
  sku: id,
  description: id,
  tracksStock: true,
  availableStock: 100,
  packagingLevels: [],
  baseUomCode: "UNIT",
  productFamilyName: null,
  uomAbbrev: "UNIT",
  warehouseName: "Principal",
  averageCost: 5,
  salePriceWithoutTax: 10,
  finalSalePrice: 11.5,
  vatDisplay: "IVA 15%",
  iceDisplay: "",
  vatCode: "10",
  iceCode: null,
  matchedPackagingLevelId: null,
  priceListName: null,
  discountDescription: null,
  discountedSalePriceWithoutTax: null,
  discountedFinalSalePrice: null,
});
const cash = {
  id: "cash",
  code: "CASH",
  name: "Efectivo",
  affectsPhysicalCash: true,
  isCreditAllowed: false,
  detailType: "None",
  sriPaymentMethodCode: "01",
};
beforeEach(() => {
  vi.mocked(apiGet).mockImplementation(async (url: string) => {
    if (url.includes("precision-policy")) return TEST_PRECISION_POLICY;
    if (url.includes("cash-sessions/my"))
      return {
        id: "session",
        status: "Open",
        emissionType: "Physical",
        defaultWarehouseId: "wh-1",
      };
    if (url.includes("invoice-defaults"))
      return { defaultWarehouseId: "wh-1", configurationWarnings: [] };
    if (url.includes("runtime-context")) return { consumerFinalPolicy: null };
    if (url.includes("/pricing"))
      return { unitPrice: 10, basePrice: 10, vatCode: "10", priceListId: null };
    if (url.includes("payment-methods")) return [cash];
    if (url.includes("warehouses"))
      return [
        { id: "wh-1", name: "Principal" },
        { id: "wh-2", name: "Alterna" },
      ];
    if (url.includes("vat-rates")) return [{ code: "10", percentage: 15 }];
    if (url.startsWith("/api/v1/sales" + "?")) return { items: [] };
    return [];
  });
  vi.mocked(apiPost).mockResolvedValue({
    id: "invoice",
    status: "Authorized",
    emissionType: "Physical",
    payments: [],
    lines: [],
  });
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  vi.restoreAllMocks();
});
async function setup() {
  const hook = renderHook(() => useSalesPage());
  await waitFor(() =>
    expect(hook.result.current.selectedWarehouseId).toBe("wh-1"),
  );
  return hook;
}

describe("POS hardening: real hook, mocked transport only", () => {
  it.each([[1,[1]],[5,[2,4]],[30,[3,15,27]]])("keeps %i rows blocked until every invalid row is corrected", async (count, invalid) => {
    const {result} = await setup();
    await act(async () => { for(let i=1;i<=count;i++) await result.current.addLineWithItem(item(String(i))); });
    act(() => {
      result.current.form.setValue("customerId","customer");
      result.current.form.setValue("payments",[{_key:1,paymentMethodId:"cash",amount:result.current.summary.total}]);
      result.current.setCashReceivedInput("100000");
      for(const number of invalid) result.current.updateLine(result.current.lines[number-1]._key,"quantity",101);
    });
    expect(new Set(result.current.lineIssues.map(issue=>issue.key)).size).toBe(invalid.length);
    expect(result.current.canEmit).toBe(false);
    act(() => result.current.openIssueFlow());
    expect(result.current.issuePhase).toBe("idle");
    await act(async () => { await result.current.confirmIssue(); });
    expect(vi.mocked(apiPost)).not.toHaveBeenCalled();
    for (let index=0; index<invalid.length; index++) {
      act(() => result.current.updateLine(result.current.lines[invalid[index]-1]._key,"quantity",1));
      expect(new Set(result.current.lineIssues.map(issue=>issue.key)).size).toBe(invalid.length-index-1);
      expect(result.current.canEmit).toBe(index===invalid.length-1);
    }
  });
  it("routes server stock errors inline instead of showing a ready footer and global banner", async () => {
    const {result}=await setup();
    await act(async () => { await result.current.addLineWithItem(item("A")); });
    act(()=> {result.current.form.setValue("customerId","customer");result.current.form.setValue("payments",[{_key:1,paymentMethodId:"cash",amount:11.5}]);result.current.setCashReceivedInput("20");});
    vi.mocked(apiPost).mockImplementation(async (url) => {
      if(url.endsWith("/authorize")) throw {isAxiosError:true,response:{status:422,data:{data:{errors:["L\u00ednea 'A \u2014 A': stock insuficiente en la bodega seleccionada (disponible: 0, solicitado: 1)."]}}}};
      return {id:"invoice",status:"Draft",payments:[],lines:[],emissionType:"Physical"};
    });
    expect(result.current.canEmit).toBe(true);
    act(()=> result.current.openIssueFlow());
    await act(async()=> {await result.current.confirmIssue();});
    expect(result.current.saveError).toBeNull();
    expect(result.current.lineIssues).toHaveLength(1);
    expect(result.current.lineIssues[0].column).toBe("stock");
    expect(result.current.canEmit).toBe(false);
    expect(result.current.emitBlockers[0].source).toBe("lines");
    act(()=> result.current.updateLine(result.current.lines[0]._key,"quantity",0.5));
    expect(result.current.lineIssues).toEqual([]);
    expect(result.current.canEmit).toBe(true);
  });
  it("a document error also blocks readiness and clears after changing its input", async () => {
    const {result}=await setup();
    await act(async()=> {await result.current.addLineWithItem(item("A"));});
    act(()=> {result.current.form.setValue("customerId","customer");result.current.form.setValue("payments",[{_key:1,paymentMethodId:"cash",amount:11.5}]);result.current.setCashReceivedInput("20");});
    act(()=> result.current.setSaveError({title:"No se puede emitir",detail:"Cliente bloqueado"}));
    expect(result.current.canEmit).toBe(false);
    expect(result.current.emitBlockers.some(blocker=>blocker.source==="document")).toBe(true);
    act(()=> result.current.form.setValue("customerId","other"));
    expect(result.current.saveError).toBeNull();
    expect(result.current.canEmit).toBe(true);
  });

  it("does not restore a pending product after clearing the sale", async () => {
    const { result } = await setup();
    let resolvePrice!: (
      value: Awaited<ReturnType<typeof salesItemPricingService.get>>,
    ) => void;
    vi.spyOn(salesItemPricingService, "get").mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolvePrice = resolve;
        }),
    );
    let pending!: Promise<void>;
    act(() => {
      pending = result.current.addLineWithItem(item("A"));
    });
    await act(async () => {
      await result.current.clearForm();
    });
    await act(async () => {
      resolvePrice({
        itemId: "A",
        unitPrice: 10,
        basePrice: 10,
        vatCode: "10",
        vatName: null,
        iceCode: null,
        iceName: null,
        maxDiscountPercent: null,
        priceListCode: "",
        priceListName: "",
        discountDescription: null,
        priceListId: null,
      });
      await pending;
    });
    expect(result.current.lines).toEqual([]);
  });
  it("uses distinct row keys for concurrent product additions", async () => {
    const { result } = await setup();
    await act(async () => {
      await Promise.all([
        result.current.addLineWithItem(item("A")),
        result.current.addLineWithItem(item("B")),
      ]);
    });
    expect(result.current.lines).toHaveLength(2);
    expect(new Set(result.current.lines.map((l) => l._key)).size).toBe(2);
    act(() => result.current.removeLine(result.current.lines[0]._key));
    expect(result.current.lines.map((l) => l.itemId)).toEqual(["B"]);
  });
  it("merges rescan, edits quantity/discount/net price, changes warehouse and clears", async () => {
    const { result } = await setup();
    await act(async () => {
      await result.current.addLineWithItem(item("A"));
      await result.current.addLineWithItem(item("A"));
    });
    expect(result.current.lines).toHaveLength(1);
    expect(result.current.lines[0].quantity).toBe(2);
    const key = result.current.lines[0]._key;
    act(() => result.current.updateLine(key, "quantity", 3));
    act(() => result.current.updateLine(key, "discountPct", 10));
    expect(result.current.summary.subtotal).toBe(30);
    expect(result.current.summary.netSubtotal).toBe(27);
    expect(result.current.summary.vat).toBe(4.05);
    expect(result.current.summary.total).toBe(31.05);
    act(() => result.current.updateLine(key, "invoicedUnitPrice", 8));
    expect(result.current.lines[0].quantity).toBe(3);
    expect(result.current.summary.netSubtotal).toBe(24);
    expect(result.current.summary.vat).toBe(3.6);
    expect(result.current.summary.total).toBe(27.6);
    act(() =>
      result.current.onUpdateLineWarehouse(key, "wh-2", {
        warehouseId: "wh-2",
        warehouseName: "Alterna",
        available: 50,
        reserved: 0,
        canSell: true,
      }),
    );
    expect(result.current.lines[0].warehouseId).toBe("wh-2");
    await act(async () => {
      await result.current.clearForm();
    });
    expect(result.current.lines).toEqual([]);
    expect(result.current.payments).toEqual([]);
  });
  it("does not create or authorize twice when confirmation is called before React rerenders", async () => {
    const { result } = await setup();
    await act(async () => {
      await result.current.addLineWithItem(item("A"));
    });
    act(() => {
      result.current.form.setValue("customerId", "customer");
      result.current.form.setValue("payments", [
        {
          _key: 1,
          paymentMethodId: "cash",
          amount: result.current.summary.total,
        },
      ]);
      result.current.setCashReceivedInput("20");
    });
    act(() => result.current.setCashReceivedInput("1"));
    expect(result.current.canEmit).toBe(false);
    const applied = result.current.payments[0].amount;
    act(() =>
      result.current.setCashReceivedInput(String(result.current.summary.total)),
    );
    expect(result.current.canEmit).toBe(true);
    expect(result.current.cashChange).toBe(0);
    act(() => result.current.setCashReceivedInput("20"));
    expect(result.current.payments[0].amount).toBe(applied);
    expect(result.current.cashChange).toBeCloseTo(20 - applied, 2);
    await waitFor(() => expect(result.current.canEmit).toBe(true));
    act(() => result.current.openIssueFlow());
    await act(async () => {
      await Promise.all([
        result.current.confirmIssue(),
        result.current.confirmIssue(),
      ]);
    });
    expect(
      vi.mocked(apiPost).mock.calls.filter(([url]) => url === "/api/v1/sales"),
    ).toHaveLength(1);
    expect(
      vi
        .mocked(apiPost)
        .mock.calls.filter(([url]) => url.includes("/authorize")),
    ).toHaveLength(1);
    const create = vi
      .mocked(apiPost)
      .mock.calls.find(([url]) => url === "/api/v1/sales")!;
    expect(create[1]).toMatchObject({
      payments: [{ amount: applied, tenderedAmount: 20 }],
    });
  });
});
