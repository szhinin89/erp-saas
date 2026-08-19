import type { ComponentType } from "react";
import { CompanyProfileSettingsSection } from "./sections/CompanyProfileSettingsSection";
import { BrandingSettingsSection } from "./sections/BrandingSettingsSection";
import { FiscalSettingsSection } from "./sections/FiscalSettingsSection";
import { OperationSettingsSection } from "./sections/OperationSettingsSection";
import { DocumentsSettingsSection } from "./sections/DocumentsSettingsSection";
import { DecimalSettingsSection } from "./sections/DecimalSettingsSection";
import { CompanySalesSettingsSection } from "./sections/CompanySalesSettingsSection";

export type CompanySettingsTabId =
  | "profile"
  | "branding"
  | "fiscal"
  | "operation"
  | "documents"
  | "decimals"
  | "sales";

export type CompanySettingsTab = {
  id: CompanySettingsTabId;
  labelKey: string;
  icon: string;
  component: ComponentType;
  enabled: boolean;
};

/**
 * Tabs del Company Settings Hub.
 * Cada tab administra configuraciones cuyo propietario es la Empresa.
 * Sucursal → BranchDetailPage. Establecimiento, PE, Bodega → sus respectivas páginas.
 */
export const companySettingsTabs: CompanySettingsTab[] = [
  {
    id: "profile",
    labelKey: "settings.company.tabs.profile",
    icon: "badge",
    component: CompanyProfileSettingsSection,
    enabled: true,
  },
  {
    id: "branding",
    labelKey: "settings.company.tabs.branding",
    icon: "palette",
    component: BrandingSettingsSection,
    enabled: true,
  },
  {
    id: "fiscal",
    labelKey: "settings.company.tabs.fiscal",
    icon: "account_balance",
    component: FiscalSettingsSection,
    enabled: true,
  },
  {
    id: "operation",
    labelKey: "settings.company.tabs.operation",
    icon: "tune",
    component: OperationSettingsSection,
    enabled: true,
  },
  {
    id: "documents",
    labelKey: "settings.company.tabs.documents",
    icon: "description",
    component: DocumentsSettingsSection,
    enabled: true,
  },
  {
    id: "decimals",
    labelKey: "settings.company.tabs.decimals",
    icon: "decimal_increase",
    component: DecimalSettingsSection,
    enabled: true,
  },
  {
    id: "sales",
    labelKey: "settings.company.tabs.sales",
    icon: "sell",
    component: CompanySalesSettingsSection,
    enabled: true,
  },
];
