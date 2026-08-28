// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { SupplierPaymentStatusBadge } from "./SupplierPaymentStatusBadge";

afterEach(() => {
  cleanup();
});

describe("SupplierPaymentStatusBadge", () => {
  it("renderiza Confirmado para status Confirmed", () => {
    render(<SupplierPaymentStatusBadge status="Confirmed" />);

    expect(screen.getByText("Confirmado")).toBeTruthy();
  });

  it("renderiza Reversado para status Reversed", () => {
    render(<SupplierPaymentStatusBadge status="Reversed" />);

    expect(screen.getByText("Reversado")).toBeTruthy();
  });
});
