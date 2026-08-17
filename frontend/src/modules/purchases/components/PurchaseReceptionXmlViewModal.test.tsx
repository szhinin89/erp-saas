// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionXmlViewModal } from "./PurchaseReceptionXmlViewModal";
import type { PurchaseReceptionXmlView } from "../api/purchaseReceptionService";

afterEach(() => {
  cleanup();
});

const data: PurchaseReceptionXmlView = {
  documentId: "doc-1",
  documentType: "INVOICE",
  documentNumber: "029-001-001293714",
  issueDate: "2026-07-01",
  accessKey: "0107202601179141513200120290010012937141234567811",
  authorizationNumber: "0107202601179141513200120290010012937141234567811",
  authorizationDate: "2026-07-01T21:06:55Z",
  supplierName: "BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.",
  supplierTradeName: "ARCADOR",
  supplierTaxId: "1791415132001",
  referralGuide: "001-002-000000111",
  paymentMethodCode: "20",
  paymentTerm: "30",
  paymentTimeUnit: "dias",
  modifiedDocumentNumber: null,
  modifiedDocumentType: null,
  modifiedDocumentDate: null,
  modificationReason: null,
  subtotal: 74.39,
  discountAmount: 0,
  iceAmount: 6.45,
  irbpnrAmount: 2.64,
  vatAmount: 12.13,
  tipAmount: 0,
  totalAmount: 95.6,
  lineCalculatedTotal: 95.61,
  roundingDifference: -0.01,
  taxSummaries: [
    {
      taxCode: "2",
      taxRateCode: "4",
      taxName: "IVA",
      rate: 15,
      taxableBase: 80.84,
      amount: 12.13,
    },
    {
      taxCode: "3",
      taxRateCode: "3053",
      taxName: "ICE",
      rate: 0.18,
      taxableBase: 35.81,
      amount: 6.45,
    },
    {
      taxCode: "5",
      taxRateCode: "5001",
      taxName: "IRBPNR",
      rate: 0.02,
      taxableBase: 132,
      amount: 2.64,
    },
  ],
  lines: [
    {
      mainCode: "3172",
      auxCode: null,
      description: "FANTA HARMONY NRJ 1350 PET(12)",
      quantity: 1,
      unitPrice: 18.58,
      discountAmount: 0,
      taxableBase: 18.58,
      vatAmount: 3.22,
      iceAmount: 2.86,
      irbpnrAmount: 0.48,
      totalAmount: 25.14,
      lineTotal: 25.14,
      taxes: [
        {
          taxCode: "2",
          taxRateCode: "4",
          taxName: "IVA",
          rate: 15,
          taxableBase: 21.44,
          amount: 3.22,
        },
        {
          taxCode: "3",
          taxRateCode: "3053",
          taxName: "ICE",
          rate: 0.18,
          taxableBase: 15.89,
          amount: 2.86,
        },
        {
          taxCode: "5",
          taxRateCode: "5001",
          taxName: "IRBPNR",
          rate: 0.02,
          taxableBase: 24,
          amount: 0.48,
        },
      ],
      additionalDetails: [{ name: "Unidad", value: "PACA" }],
    },
    {
      mainCode: "10111",
      auxCode: null,
      description: "COCA-COLA 350ML LATA(6)",
      quantity: 1,
      unitPrice: 6.08,
      discountAmount: 0,
      taxableBase: 6.08,
      vatAmount: 0.91,
      iceAmount: 0,
      irbpnrAmount: 0,
      totalAmount: 6.99,
      lineTotal: 6.99,
      taxes: [
        {
          taxCode: "2",
          taxRateCode: "4",
          taxName: "IVA",
          rate: 15,
          taxableBase: 6.08,
          amount: 0.91,
        },
      ],
      additionalDetails: [],
    },
  ],
  rawXmlAvailable: true,
  rawXml: "<factura />",
};

function renderModal() {
  return render(
    <I18nProvider>
      <PurchaseReceptionXmlViewModal
        open
        loading={false}
        error={null}
        data={data}
        onClose={vi.fn()}
      />
    </I18nProvider>,
  );
}

/** Los valores monetarios de solo lectura (totales XML, resumen de impuestos, líneas)
 * migraron a `ZHMoneyValue` — el texto "$X.XX" queda repartido en spans anidados
 * (`.zh-money-value__symbol` + `.zh-money-value__amount`), por lo que se verifica vía
 * `.zh-money-value` (textContent) en vez de `getByText` (ver precedente Ventas,
 * SALES-DS-MONEY-12: `SalesReturnListPage.test.tsx`). La cantidad (`quantity`, decimales
 * distintos a monto) y la tarifa (%) no migraron — siguen siendo texto plano. */
function moneyTexts(root: ParentNode): string[] {
  return Array.from(root.querySelectorAll(".zh-money-value")).map(
    (el) => el.textContent,
  ) as string[];
}

describe("PurchaseReceptionXmlViewModal", () => {
  it("renders XML totals, tax summary, IRBPNR and expandable line detail", () => {
    const { container } = renderModal();

    expect(screen.getByText("Totales XML")).toBeTruthy();
    expect(screen.getByText("Resumen de impuestos")).toBeTruthy();
    expect(screen.getByText("5/5001")).toBeTruthy();

    const allMoney = moneyTexts(container);
    expect(allMoney).toContain("$2.64");
    expect(allMoney).toContain("$95.60");
    expect(allMoney).toContain("$95.61");
    expect(allMoney).toContain("$-0.01");

    const fantaRow = screen.getByText("3172").closest("tr");
    expect(fantaRow).toBeTruthy();
    const fantaMoney = moneyTexts(fantaRow!);
    expect(fantaMoney).toContain("$0.48");
    expect(fantaMoney).toContain("$25.14");

    const lataRow = screen.getByText("10111").closest("tr");
    expect(lataRow).toBeTruthy();
    const lataMoney = moneyTexts(lataRow!);
    expect(lataMoney.filter((t) => t === "$0.00").length).toBeGreaterThan(0);

    const details = screen.getAllByText("Ver detalle");
    fireEvent.click(details[0]);
    expect(details[0].closest("details")?.open).toBe(true);
    expect(screen.getByText("IRBPNR 5/5001")).toBeTruthy();
    expect(screen.getByText("Unidad")).toBeTruthy();
    expect(screen.getByText("PACA")).toBeTruthy();
  });
});
