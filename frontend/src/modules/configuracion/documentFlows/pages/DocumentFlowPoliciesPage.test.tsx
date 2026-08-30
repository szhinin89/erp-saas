// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { DocumentFlowPoliciesPage } from "./DocumentFlowPoliciesPage";
import {
  documentFlowPolicyService,
  type DocumentFlowPolicyDto,
} from "../api/documentFlowPolicyService";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";

/**
 * DOCUMENT-FLOW-POLICY-UX-01 — un administrador no técnico debe poder entender cada flujo
 * sin conocer enums. Cubre: (1) nunca se muestra un valor de enum técnico crudo en pantalla,
 * (2) la tabla muestra los textos funcionales exactos pedidos, (3) la nota de separación
 * permisos/flujo sigue visible, (4) el editor ofrece opciones en español, (5) guardar sigue
 * enviando al API los valores técnicos correctos aunque la UI muestre textos amigables.
 */

vi.mock("../api/documentFlowPolicyService", () => ({
  documentFlowPolicyService: {
    list: vi.fn(),
    getById: vi.fn(),
    update: vi.fn(),
  },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

function grant(granted: string[] | "all" = "all") {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (key: string) => granted === "all" || granted.includes(key),
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <DocumentFlowPoliciesPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

const GASDOC_POLICY: DocumentFlowPolicyDto = {
  id: "policy-1",
  documentTypeCode: "GASDOC",
  documentTypeName: "Documento de Gasto",
  isActive: true,
  creationMode: "DraftRequired",
  confirmationMode: "ManualConfirmation",
  authorizationMode: "None",
  pendingDocumentMode: "None",
  cancellationMode: "AllowedAfterConfirmationWithReversal",
  requiresCancellationReason: true,
  requiresAttachment: false,
  requiresSupplier: true,
  requiresDueDate: true,
  payableGenerationMode: "OnConfirmation",
  accountingPostingMode: "OnConfirmation",
  inventoryImpactMode: "None",
  notificationMode: "None",
};

const SALES_POLICY: DocumentFlowPolicyDto = {
  id: "policy-2",
  documentTypeCode: "FACVEN",
  documentTypeName: "Factura de Venta",
  isActive: true,
  creationMode: "DirectCreation",
  confirmationMode: "AutoConfirmOnCreate",
  authorizationMode: "None",
  pendingDocumentMode: "None",
  cancellationMode: "NotAllowed",
  requiresCancellationReason: false,
  requiresAttachment: false,
  requiresSupplier: false,
  requiresDueDate: false,
  payableGenerationMode: "None",
  accountingPostingMode: "None",
  inventoryImpactMode: "None",
  notificationMode: "None",
};

const TECHNICAL_ENUM_TOKENS = [
  "DirectCreation",
  "DraftRequired",
  "AutoConfirmOnCreate",
  "ManualConfirmation",
  "NotAllowed",
  "AllowedAfterConfirmationWithReversal",
];

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  grant("all");
  vi.mocked(documentFlowPolicyService.list).mockResolvedValue([GASDOC_POLICY, SALES_POLICY]);
});

describe("DocumentFlowPoliciesPage — textos funcionales, nunca enums técnicos", () => {
  it("nunca muestra valores de enum técnico crudos en la tabla", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Documento de Gasto")).toBeTruthy());

    const bodyText = document.body.textContent ?? "";
    for (const token of TECHNICAL_ENUM_TOKENS) {
      expect(bodyText.includes(token)).toBe(false);
    }
  });

  it("muestra los textos funcionales exactos pedidos para el gasto", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Documento de Gasto")).toBeTruthy());

    expect(screen.getByText("Borrador obligatorio")).toBeTruthy();
    expect(screen.getByText("Confirmación manual")).toBeTruthy();
    expect(screen.getByText("Anulación con reverso")).toBeTruthy();
  });

  it("muestra la nota de separación permisos/flujo en la lista", async () => {
    renderPage();
    await waitFor(() =>
      expect(
        screen.getByText(
          "Esta pantalla define cómo se comporta cada documento en la empresa. Los accesos de usuario se administran en Roles y Permisos.",
        ),
      ).toBeTruthy(),
    );
  });

  it("no usa labels de tipo Puede/Permite que confundan con permisos", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Documento de Gasto")).toBeTruthy());

    const bodyText = document.body.textContent ?? "";
    expect(/\bPuede\b/.test(bodyText)).toBe(false);
    expect(/\bPermite\b/i.test(bodyText.replace(/No permite anulación/g, ""))).toBe(false);
  });
});

describe("DocumentFlowPoliciesPage — editor en español y contrato de guardado", () => {
  async function openEditor(): Promise<void> {
    renderPage();
    await waitFor(() => expect(screen.getByText("Documento de Gasto")).toBeTruthy());
    const editBtn = screen.getByRole("button", {
      name: "Editar flujo de Documento de Gasto",
    });
    fireEvent.click(editBtn);
    await waitFor(() => expect(screen.getAllByRole("combobox").length).toBeGreaterThan(0));
  }

  it("el editor muestra las opciones de los selects en español, no el enum técnico", async () => {
    await openEditor();

    const creationSelect = screen.getByDisplayValue("Borrador obligatorio");
    const options = Array.from(
      (creationSelect as HTMLSelectElement).options,
    ).map((o) => o.textContent);
    expect(options).toEqual(["Borrador obligatorio", "Creación directa"]);
    expect(options).not.toContain("DraftRequired");
    expect(options).not.toContain("DirectCreation");
  });

  it("muestra el nombre del documento y la nota de separación en el editor", async () => {
    await openEditor();

    expect(screen.getByDisplayValue("Documento de Gasto")).toBeTruthy();
    expect(
      screen.getAllByText(
        "Esta pantalla define cómo se comporta cada documento en la empresa. Los accesos de usuario se administran en Roles y Permisos.",
      ).length,
    ).toBeGreaterThan(0);
  });

  it("guarda enviando al API los valores técnicos correctos aunque la UI muestre textos amigables", async () => {
    vi.mocked(documentFlowPolicyService.update).mockResolvedValue(GASDOC_POLICY);
    await openEditor();

    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() => expect(documentFlowPolicyService.update).toHaveBeenCalledTimes(1));
    expect(documentFlowPolicyService.update).toHaveBeenCalledWith(
      "policy-1",
      expect.objectContaining({
        id: "policy-1",
        creationMode: "DraftRequired",
        confirmationMode: "ManualConfirmation",
        cancellationMode: "AllowedAfterConfirmationWithReversal",
        payableGenerationMode: "OnConfirmation",
        accountingPostingMode: "OnConfirmation",
      }),
    );
    expect(message.success).toHaveBeenCalledWith(
      "Flujo documental actualizado correctamente.",
    );
  });

  it("al cambiar una opción del select, guarda el nuevo valor técnico correspondiente", async () => {
    vi.mocked(documentFlowPolicyService.update).mockResolvedValue(GASDOC_POLICY);
    await openEditor();

    const creationSelect = screen.getByDisplayValue("Borrador obligatorio");
    fireEvent.change(creationSelect, { target: { value: "DirectCreation" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar" }));

    await waitFor(() => expect(documentFlowPolicyService.update).toHaveBeenCalledTimes(1));
    expect(documentFlowPolicyService.update).toHaveBeenCalledWith(
      "policy-1",
      expect.objectContaining({ creationMode: "DirectCreation" }),
    );
  });
});
