import type { ComponentType } from "react";
import { createElement } from "react";
import { SalesPosPreferencesSection } from "./sections/SalesPosPreferencesSection";
import { CashPreferencesSection } from "./sections/CashPreferencesSection";
import { PrintingPreferencesSection } from "./sections/PrintingPreferencesSection";
import { ElectronicDocumentsPreferencesSection } from "./sections/ElectronicDocumentsPreferencesSection";
import { PurchasesPreferencesSection } from "./sections/PurchasesPreferencesSection";
import { EmptyPreferencesSection } from "./sections/EmptyPreferencesSection";

export type OperationalPreferencesTabId =
  | "general"
  | "salesPos"
  | "cash"
  | "purchases"
  | "inventory"
  | "printing"
  | "electronicDocuments"
  | "notifications";

export type OperationalPreferencesTab = {
  id: OperationalPreferencesTabId;
  labelKey: string;
  icon: string;
  component: ComponentType;
};

/** Fábrica de un tab sin preferencias conectadas todavía — ver EmptyPreferencesSection. */
function emptyTabComponent(titleKey: string): ComponentType {
  return () => createElement(EmptyPreferencesSection, { titleKey });
}

/**
 * Tabs del hub de Preferencias Operativas (CONFIG-DYNAMIC-OPERATIONS-01/02). Solo salesPos/cash/
 * purchases/printing/electronicDocuments tienen campos editables — los demás muestran un aviso
 * explícito (EmptyPreferencesSection) en vez de settings decorativos.
 */
export const operationalPreferencesTabs: OperationalPreferencesTab[] = [
  {
    id: "general",
    labelKey: "settings.operations.tabs.general",
    icon: "tune",
    component: emptyTabComponent("settings.operations.general.title"),
  },
  {
    id: "salesPos",
    labelKey: "settings.operations.tabs.salesPos",
    icon: "point_of_sale",
    component: SalesPosPreferencesSection,
  },
  {
    id: "cash",
    labelKey: "settings.operations.tabs.cash",
    icon: "payments",
    component: CashPreferencesSection,
  },
  {
    id: "purchases",
    labelKey: "settings.operations.tabs.purchases",
    icon: "shopping_cart",
    component: PurchasesPreferencesSection,
  },
  {
    id: "inventory",
    labelKey: "settings.operations.tabs.inventory",
    icon: "inventory_2",
    component: emptyTabComponent("settings.operations.inventory.title"),
  },
  {
    id: "printing",
    labelKey: "settings.operations.tabs.printing",
    icon: "print",
    component: PrintingPreferencesSection,
  },
  {
    id: "electronicDocuments",
    labelKey: "settings.operations.tabs.electronicDocuments",
    icon: "description",
    component: ElectronicDocumentsPreferencesSection,
  },
  {
    id: "notifications",
    labelKey: "settings.operations.tabs.notifications",
    icon: "notifications",
    component: emptyTabComponent("settings.operations.notifications.title"),
  },
];
