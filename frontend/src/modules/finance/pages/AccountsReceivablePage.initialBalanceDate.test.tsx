// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AccountsReceivablePage } from "./AccountsReceivablePage";
import { receivableService, type SalesReceivableDto } from "../api/receivableService";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { I18nProvider } from "../../../i18n/i18n";

vi.mock("../api/receivableService", () => ({ receivableService: { list: vi.fn() } }));

function receivable(overrides: Partial<SalesReceivableDto>): SalesReceivableDto {
  return {
    id: "r1",
    invoiceId: null,
    invoiceNumber: "001-001-000000101",
    customerId: "c1",
    customerName: "Cliente R1",
    customerIdentification: "05-1704827136",
    branchId: "b1",
    branchName: "Sucursal Principal",
    createdByUserId: null,
    createdByName: "Admin",
    invoiceIssueDate: "2026-10-01",
    invoiceCreatedAt: "2026-10-28T12:29:28Z",
    dueDate: "2026-11-15",
    originalAmount: 200,
    paidAmount: 0,
    balanceDue: 200,
    status: "pending",
    statusLabel: "Pendiente",
    installmentCount: 1,
    overdueDays: null,
    installments: [],
    createdAt: "2026-10-28T12:29:28Z",
    updatedAt: null,
    ...overrides,
  };
}

afterEach(cleanup);

describe("AccountsReceivablePage — Fecha factura", () => {
  it("saldo inicial (sin factura): muestra la emisión del documento, no la fecha de carga", async () => {
    vi.mocked(receivableService.list).mockResolvedValue({
      items: [receivable({})],
      total: 1,
      page: 1,
      pageSize: 50,
    });
    render(<I18nProvider><MemoryRouter><AccountsReceivablePage /></MemoryRouter></I18nProvider>);

    expect(await screen.findByText(formatDate("2026-10-01"))).toBeTruthy();
    expect(screen.queryByText(formatDateTime("2026-10-28T12:29:28Z"))).toBeNull();
  });

  it("factura: conserva la fecha y hora de la factura", async () => {
    vi.mocked(receivableService.list).mockResolvedValue({
      items: [receivable({ invoiceId: "inv-1", invoiceIssueDate: "2026-10-27", invoiceCreatedAt: "2026-10-27T15:00:00Z" })],
      total: 1,
      page: 1,
      pageSize: 50,
    });
    render(<I18nProvider><MemoryRouter><AccountsReceivablePage /></MemoryRouter></I18nProvider>);

    expect(await screen.findByText(formatDateTime("2026-10-27T15:00:00Z"))).toBeTruthy();
  });
});
