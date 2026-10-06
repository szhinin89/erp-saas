import { describe, expect, it } from "vitest";
import {
  collectSalesLineIssues,
  mapSalesServerLineIssues,
  salesLineCorrectionSummary,
} from "./salesLineIssues";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
const line = (key = 1): SalesLineFormValues => ({
  _key: key,
  itemId: `item-${key}`,
  description: `SKU-${key} — Product`,
  quantity: 1,
  unitPrice: 10,
  discountPct: 0,
  vatCode: "10",
  warehouseId: "wh",
  _participatesInInventory: true,
  _stockQty: 10,
});
const error = (errors: unknown) => ({
  isAxiosError: true,
  response: { status: 422, data: { data: { errors } } },
});
describe("POS line issues", () => {
  it("maps every schema field to its actual column without duplicating business rules", () => {
    const issues = collectSalesLineIssues([
      {
        ...line(),
        quantity: 0,
        unitPrice: -1,
        discountPct: 101,
        vatCode: "",
        description: "",
        warehouseId: null,
      },
    ]);
    expect(new Set(issues.map((i) => i.column))).toEqual(
      new Set(["quantity", "price", "discount", "total", "product", "stock"]),
    );
  });
  it("uses stock in base units for packaged quantities", () => {
    const issues = collectSalesLineIssues([
      { ...line(), quantity: 1, conversionFactor: 12, _stockQty: 10 },
    ]);
    expect(issues[0].detail).toContain("Solicitado: 12");
  });
  it("maps indexed server fields to stable keys even when indexes differ from keys", () => {
    const result = mapSalesServerLineIssues(
      error({
        "Lines[1].UnitPrice": ["Precio no permitido"],
        CustomerId: ["Cliente bloqueado"],
      }),
      [line(10), line(90)],
    );
    expect(result.lineIssues[0]).toMatchObject({
      key: 90,
      column: "price",
      message: "Precio no permitido",
    });
    expect(result.globalMessages).toEqual(["Cliente bloqueado"]);
  });
  it("legacy stock messages stay inline; repeated descriptions mark every matching line", () => {
    const a = line(10),
      b = { ...line(20), description: a.description };
    const result = mapSalesServerLineIssues(
      error([
        `Línea '${a.description}': stock insuficiente`,
        `Cliente no válido`,
      ]),
      [a, b],
    );
    expect(result.lineIssues.map((i) => i.key)).toEqual([10, 20]);
    expect(salesLineCorrectionSummary(result.lineIssues)).toBe(
      "2 líneas requieren corrección",
    );
    expect(result.globalMessages).toEqual(["Cliente no válido"]);
  });
  it("server errors expire only when the relevant data changes and do not move to another line", () => {
    const a = line();
    const mapped = mapSalesServerLineIssues(
      error({ "lines.0.quantity": ["Cantidad no permitida"] }),
      [a],
    );
    expect(
      collectSalesLineIssues([{ ...a, unitPrice: 20 }], mapped.lineIssues),
    ).toHaveLength(1);
    expect(
      collectSalesLineIssues([{ ...a, quantity: 2 }], mapped.lineIssues),
    ).toEqual([]);
    expect(collectSalesLineIssues([line(2)], mapped.lineIssues)).toEqual([]);
  });
});
