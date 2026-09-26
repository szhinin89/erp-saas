// @vitest-environment jsdom
import type { ComponentProps } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierSearchSelect } from "./SupplierSearchSelect";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { RoleTypeEnum, type SupplierPickerRow } from "../types/businessPartner.types";

/**
 * ZH-SUPPLIER-SEARCH-REUSABLE-01 / ZH-SUPPLIER-SEARCH-SINGLE-SOURCE-02 — buscador único de
 * proveedores (absorbe la cobertura del antiguo SupplierPicker y del selector propio de Gastos).
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    searchBusinessPartners: vi.fn(),
    getBusinessPartner: vi.fn(),
  },
}));

const PLACEHOLDER = "Buscar por RUC, razón social o nombre...";

const BP_DIPOR = {
  id: "bp-1",
  legalName: "DISTRIBUIDORA IMPORTADORA DIPOR S.A.",
  tradeName: null,
  identificationNumber: "0990789061001",
  isActive: true,
};

const DETAIL_SUP1 = {
  id: "sup-1",
  identificationNumber: "1791415132001",
  tradeName: "",
  legalName: "Proveedor Uno S.A.",
  isActive: true,
  roles: [
    {
      roleType: "Supplier",
      isActive: true,
      supplierConfig: { defaultTaxSupportCode: "01" },
    },
  ],
};

function MultipleHarness({ spy }: { spy: (v: SupplierPickerRow[]) => void }) {
  const [value, setValue] = useState<SupplierPickerRow[]>([]);
  return (
    <SupplierSearchSelect
      mode="multiple"
      value={value}
      onChange={(v) => {
        setValue(v);
        spy(v);
      }}
    />
  );
}

/** Consumidor típico (RHF / filtros): guarda solo el id, como los 6 usos migrados. */
function IdHarness({
  initialId = null,
  spy,
  disabled,
  activeOnly,
}: {
  initialId?: string | null;
  spy?: (v: SupplierPickerRow | null) => void;
  disabled?: boolean;
  activeOnly?: boolean;
}) {
  const [id, setId] = useState<string | null>(initialId);
  return (
    <SupplierSearchSelect
      value={id}
      disabled={disabled}
      activeOnly={activeOnly}
      onChange={(row) => {
        setId(row?.id ?? null);
        spy?.(row);
      }}
    />
  );
}

function renderWithI18n(ui: React.ReactElement) {
  return render(<I18nProvider>{ui}</I18nProvider>);
}

async function typeAndFlush(text: string) {
  fireEvent.change(screen.getByPlaceholderText(PLACEHOLDER), { target: { value: text } });
  await act(async () => {
    vi.advanceTimersByTime(300);
  });
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("SupplierSearchSelect — búsqueda remota única", () => {
  it("busca BP con rol Supplier (máx. 30) con debounce y AbortSignal; muestra nombre + RUC", async () => {
    vi.useFakeTimers();
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValue([
      BP_DIPOR,
    ] as never);
    const spy = vi.fn();
    renderWithI18n(<MultipleHarness spy={spy} />);

    await typeAndFlush("dipo");

    expect(businessPartnerFacade.searchBusinessPartners).toHaveBeenCalledTimes(1);
    expect(businessPartnerFacade.searchBusinessPartners).toHaveBeenCalledWith(
      { q: "dipo", roles: [RoleTypeEnum.Supplier], take: 30 },
      expect.any(AbortSignal),
    );
    expect(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A.")).toBeTruthy();
    expect(screen.getByText("0990789061001")).toBeTruthy();

    fireEvent.click(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A."));
    expect(spy).toHaveBeenLastCalledWith([
      expect.objectContaining({
        id: "bp-1",
        fullName: "DISTRIBUIDORA IMPORTADORA DIPOR S.A.",
        identificationNumber: "0990789061001",
        isActive: true,
        hasSupplierRole: true,
      }),
    ]);
  });

  it("no carga el catálogo completo: sin texto (o con 1 carácter) no consulta el backend", async () => {
    vi.useFakeTimers();
    renderWithI18n(<MultipleHarness spy={vi.fn()} />);
    fireEvent.focus(screen.getByPlaceholderText(PLACEHOLDER));
    await typeAndFlush("d");
    expect(businessPartnerFacade.searchBusinessPartners).not.toHaveBeenCalled();
  });

  it("activeOnly agrega isActive=true (caso Gastos)", async () => {
    vi.useFakeTimers();
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValue([]);
    renderWithI18n(<IdHarness activeOnly />);
    await typeAndFlush("prov");
    expect(businessPartnerFacade.searchBusinessPartners).toHaveBeenCalledWith(
      { q: "prov", roles: [RoleTypeEnum.Supplier], take: 30, isActive: true },
      expect.any(AbortSignal),
    );
  });

  it("muestra loading y cancela la búsqueda anterior al seguir escribiendo", async () => {
    vi.useFakeTimers();
    const signals: AbortSignal[] = [];
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockImplementation(
      (_params, signal) => {
        signals.push(signal!);
        return new Promise(() => {});
      },
    );
    renderWithI18n(<IdHarness />);

    await typeAndFlush("pro");
    expect(screen.getAllByText("Buscando...").length).toBeGreaterThan(0);

    fireEvent.change(screen.getByPlaceholderText(PLACEHOLDER), { target: { value: "prov" } });
    expect(signals[0].aborted).toBe(true);
  });

  it("resultado vacío: muestra la consulta y el enlace para registrar proveedor", async () => {
    vi.useFakeTimers();
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValue([]);
    renderWithI18n(<IdHarness />);
    await typeAndFlush("zzz");
    expect(screen.getByText('Sin resultados para "zzz"')).toBeTruthy();
    expect(screen.getByText("Registre un proveedor").getAttribute("href")).toBe("/suppliers");
  });

  it("marca proveedores inactivos en los resultados", async () => {
    vi.useFakeTimers();
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValue([
      { ...BP_DIPOR, isActive: false },
    ] as never);
    renderWithI18n(<IdHarness />);
    await typeAndFlush("dipo");
    expect(screen.getByText("Proveedor inactivo")).toBeTruthy();
  });
});

describe("SupplierSearchSelect — single por id (consumidores migrados de SupplierPicker)", () => {
  it("selección inicial: hidrata el id con GET por id y muestra la tarjeta seleccionada", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(DETAIL_SUP1 as never);
    const { container } = renderWithI18n(<IdHarness initialId="sup-1" />);

    await screen.findByText("Proveedor Uno S.A.");
    expect(businessPartnerFacade.getBusinessPartner).toHaveBeenCalledWith("sup-1");
    const card = container.querySelector(".zh-picker-selected-value");
    expect(card?.querySelector(".zh-picker-selected-value__title")?.textContent).toBe(
      "Proveedor Uno S.A.",
    );
    expect(card?.querySelector(".zh-picker-selected-value__subtitle")?.textContent).toBe(
      "1791415132001",
    );
  });

  it("limpiar ('Cambiar proveedor') emite null y vuelve al buscador", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(DETAIL_SUP1 as never);
    const spy = vi.fn();
    renderWithI18n(<IdHarness initialId="sup-1" spy={spy} />);

    fireEvent.click(await screen.findByTitle("Cambiar proveedor"));
    await waitFor(() => expect(spy).toHaveBeenCalledWith(null));
    expect(screen.getByPlaceholderText(PLACEHOLDER)).toBeTruthy();
  });

  it("cambio de proveedor: tras elegir uno nuevo no vuelve a pedir el detalle por id", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(DETAIL_SUP1 as never);
    const spy = vi.fn();
    renderWithI18n(<IdHarness initialId="sup-1" spy={spy} />);
    fireEvent.click(await screen.findByTitle("Cambiar proveedor"));

    vi.useFakeTimers();
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValue([
      BP_DIPOR,
    ] as never);
    await typeAndFlush("dipo");
    fireEvent.click(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A."));

    expect(spy).toHaveBeenLastCalledWith(expect.objectContaining({ id: "bp-1" }));
    expect(screen.getByText("DISTRIBUIDORA IMPORTADORA DIPOR S.A.")).toBeTruthy();
    expect(businessPartnerFacade.getBusinessPartner).toHaveBeenCalledTimes(1);
  });

  it("disabled: no muestra la acción de cambiar", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(DETAIL_SUP1 as never);
    renderWithI18n(<IdHarness initialId="sup-1" disabled />);
    await screen.findByText("Proveedor Uno S.A.");
    expect(screen.queryByTitle("Cambiar proveedor")).toBeNull();
  });

  it("disabled sin selección: input deshabilitado", () => {
    renderWithI18n(<IdHarness disabled />);
    expect((screen.getByPlaceholderText(PLACEHOLDER) as HTMLInputElement).disabled).toBe(true);
  });

  it("proveedor inactivo seleccionado: badge en la tarjeta", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue({
      ...DETAIL_SUP1,
      id: "sup-2",
      legalName: "Proveedor Inactivo",
      isActive: false,
    } as never);
    renderWithI18n(<IdHarness initialId="sup-2" />);
    await screen.findByText("Proveedor Inactivo");
    expect(screen.getByText("Proveedor inactivo")).toBeTruthy();
  });

  it("valor como fila ya resuelta (Gastos): no consulta el detalle", () => {
    const row: SupplierPickerRow = {
      id: "sup-9",
      fullName: "Proveedor Gasto",
      identificationNumber: "1799999999001",
      isActive: true,
      hasSupplierRole: true,
      supplierConfig: null,
    };
    const props: ComponentProps<typeof SupplierSearchSelect> = {
      value: row,
      onChange: vi.fn(),
    };
    renderWithI18n(<SupplierSearchSelect {...props} />);
    expect(screen.getByText("Proveedor Gasto")).toBeTruthy();
    expect(businessPartnerFacade.getBusinessPartner).not.toHaveBeenCalled();
  });

  it("valor vacío ('' desde RHF) = sin selección y sin consulta", () => {
    renderWithI18n(<SupplierSearchSelect value="" onChange={vi.fn()} />);
    expect(screen.getByPlaceholderText(PLACEHOLDER)).toBeTruthy();
    expect(businessPartnerFacade.getBusinessPartner).not.toHaveBeenCalled();
  });
});
