// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { CustomerPicker } from "./CustomerPicker";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn().mockResolvedValue({
      id: "cust-1",
      identificationNumber: "1710034065",
      tradeName: "",
      legalName: "Juan Pérez",
      isActive: true,
    }),
    searchCustomersForPicker: vi.fn().mockResolvedValue([]),
  },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CustomerPicker — botón limpiar selección (SALES-DS-BUTTONS-04)", () => {
  it('muestra el botón "Cambiar cliente" con ícono de cerrar cuando hay un cliente seleccionado', async () => {
    render(<CustomerPicker value="cust-1" onChange={() => {}} />);

    const btn = await screen.findByTitle("Cambiar cliente");
    expect(btn.getAttribute("aria-label")).toBe("Cambiar cliente");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "close",
    );
  });

  it("click en el botón limpiar invoca onChange(null)", async () => {
    const onChange = vi.fn();
    render(<CustomerPicker value="cust-1" onChange={onChange} />);

    const btn = await screen.findByTitle("Cambiar cliente");
    fireEvent.click(btn);

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(null));
  });

  it("no renderiza el botón limpiar cuando disabled=true", async () => {
    render(<CustomerPicker value="cust-1" onChange={() => {}} disabled />);

    await waitFor(() =>
      expect(screen.getByText("Juan Pérez")).toBeTruthy(),
    );
    expect(screen.queryByTitle("Cambiar cliente")).toBeNull();
  });

  it("no hay estilos inline en el botón limpiar", async () => {
    render(<CustomerPicker value="cust-1" onChange={() => {}} />);

    const btn = await screen.findByTitle("Cambiar cliente");
    expect(btn.getAttribute("style")).toBeNull();
  });
});

describe("CustomerPicker — valor seleccionado (ZHPickerSelectedValue, SALES-DS-PICKER-SELECTED-07)", () => {
  it("muestra el cliente seleccionado usando ZHPickerSelectedValue", async () => {
    const { container } = render(
      <CustomerPicker value="cust-1" onChange={() => {}} />,
    );

    await screen.findByText("Juan Pérez");
    const card = container.querySelector(".zh-picker-selected-value");
    expect(card).toBeTruthy();
    expect(card?.querySelector(".zh-picker-selected-value__title")?.textContent).toBe(
      "Juan Pérez",
    );
    expect(card?.querySelector(".zh-picker-selected-value__meta")?.textContent).toBe(
      "1710034065",
    );
  });

  it('click en close ("Cambiar cliente") limpia/cambia la selección igual que antes', async () => {
    const onChange = vi.fn();
    render(<CustomerPicker value="cust-1" onChange={onChange} />);

    const btn = await screen.findByTitle("Cambiar cliente");
    fireEvent.click(btn);

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(null));
  });

  it("no muestra acción de editar cuando no se pasa onEditSelected", async () => {
    render(<CustomerPicker value="cust-1" onChange={() => {}} />);

    await screen.findByText("Juan Pérez");
    expect(screen.queryByTitle("Editar")).toBeNull();
  });
});

describe("CustomerPicker — onEditSelected/editLabel (SALES-DS-CUSTOMER-SELECTED-08)", () => {
  it("muestra la acción de editar con editLabel cuando se pasa onEditSelected", async () => {
    render(
      <CustomerPicker
        value="cust-1"
        onChange={() => {}}
        onEditSelected={() => {}}
        editLabel="Editar datos"
      />,
    );

    const btn = await screen.findByTitle("Editar datos");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "edit",
    );
  });

  it("click en editar invoca el mismo handler pasado por el consumidor", async () => {
    const onEditSelected = vi.fn();
    render(
      <CustomerPicker
        value="cust-1"
        onChange={() => {}}
        onEditSelected={onEditSelected}
        editLabel="Editar datos"
      />,
    );

    const btn = await screen.findByTitle("Editar datos");
    fireEvent.click(btn);

    expect(onEditSelected).toHaveBeenCalledTimes(1);
  });

  it("no muestra la acción de editar cuando disabled=true, aunque haya onEditSelected", async () => {
    render(
      <CustomerPicker
        value="cust-1"
        onChange={() => {}}
        onEditSelected={() => {}}
        editLabel="Editar datos"
        disabled
      />,
    );

    expect(await screen.findByText("Juan Pérez")).toBeTruthy();
    expect(screen.queryByTitle("Editar datos")).toBeNull();
  });
});

describe("CustomerPicker — fila de resultado (ZHPickerResultItem, SALES-DS-PICKER-RESULT-05)", () => {
  it("renderiza los resultados de búsqueda usando ZHPickerResultItem", async () => {
    vi.mocked(businessPartnerFacade.searchCustomersForPicker).mockResolvedValueOnce(
      [
        {
          id: "cust-2",
          identificationNumber: "0992345678001",
          fullName: "María López",
          isActive: true,
          hasCustomerRole: true,
        },
      ],
    );

    render(<CustomerPicker value={null} onChange={() => {}} />);
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    const row = await screen.findByText("María López");
    const btn = row.closest("button")!;
    expect(btn.className.includes("zh-picker-result-item")).toBe(true);
    expect(screen.getByText("0992345678001")).toBeTruthy();
  });

  it("click en un resultado selecciona al cliente igual que antes", async () => {
    vi.mocked(businessPartnerFacade.searchCustomersForPicker).mockResolvedValueOnce(
      [
        {
          id: "cust-2",
          identificationNumber: "0992345678001",
          fullName: "María López",
          isActive: true,
          hasCustomerRole: true,
        },
      ],
    );
    const onChange = vi.fn();

    render(<CustomerPicker value={null} onChange={onChange} />);
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    const row = await screen.findByText("María López");
    fireEvent.click(row);

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ id: "cust-2", fullName: "María López" }),
    );
  });

  it("no queda ningún <button> local de fila de resultado fuera de ZHPickerResultItem", async () => {
    vi.mocked(businessPartnerFacade.searchCustomersForPicker).mockResolvedValueOnce(
      [
        {
          id: "cust-2",
          identificationNumber: "0992345678001",
          fullName: "María López",
          isActive: true,
          hasCustomerRole: true,
        },
      ],
    );

    const { container } = render(
      <CustomerPicker value={null} onChange={() => {}} />,
    );
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    await screen.findByText("María López");

    container.querySelectorAll("button").forEach((btn) => {
      if (btn.textContent?.includes("María López")) {
        expect(btn.className.includes("zh-picker-result-item")).toBe(true);
      }
    });
  });

  it("no hay estilos inline en la fila de resultado", async () => {
    vi.mocked(businessPartnerFacade.searchCustomersForPicker).mockResolvedValueOnce(
      [
        {
          id: "cust-2",
          identificationNumber: "0992345678001",
          fullName: "María López",
          isActive: true,
          hasCustomerRole: true,
        },
      ],
    );

    render(<CustomerPicker value={null} onChange={() => {}} />);
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    const row = await screen.findByText("María López");
    expect(row.closest("button")!.getAttribute("style")).toBeNull();
  });
});
