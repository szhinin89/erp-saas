import { apiGet, apiPut } from "../../../lib/apiEnvelope";

/**
 * Mirror 1:1 de OperationalPreferencesDto (backend, CONFIG-DYNAMIC-OPERATIONS-01).
 * El GET siempre trae los 7 grupos completos, aunque la UI solo edite un subconjunto de
 * campos por grupo (los conectados a efecto real) — ver operationalPreferencesTabs.ts.
 */
export type SalesPosPreferencesDto = {
  requireOpenCashSession: boolean;
  allowManualPrice: boolean;
  allowManualDiscount: boolean;
  maxDiscountPercent: number;
  requireCustomerAboveAmount: number | null;
  allowSellWithoutStock: boolean;
  askBeforeIssue: boolean;
  defaultPriceListId: string | null;
  defaultCustomerId: string | null;
};

export type CashPreferencesDto = {
  requireOpeningAmount: boolean;
  allowCloseWithDifference: boolean;
  maxAllowedDifference: number;
  requireReasonForDifference: boolean;
  allowManualInOutMovements: boolean;
  requireReasonForMovements: boolean;
};

export type PurchasesPreferencesDto = {
  defaultWarehouseId: string | null;
  allowConfirmWithoutReceptionXml: boolean;
  updateCostOnConfirm: boolean;
  allowManualCostChange: boolean;
  requireReasonForCostChange: boolean;
};

export type InventoryPreferencesDto = {
  allowNegativeStock: boolean;
  requireReasonForAdjustment: boolean;
  requireApprovalForLargeAdjustment: boolean;
  largeAdjustmentThresholdAmount: number;
};

export type PrintingPreferencesDto = {
  salesReceiptMode: "AskBeforePrint" | "AlwaysPrint" | "NeverAutoPrint";
  salesReceiptCopies: number;
  salesReceiptPaperWidth: "80mm" | "58mm";
  salesReceiptIncludeLogo: boolean;
  salesReceiptIncludeAccessKey: boolean;
  salesReceiptIncludeCashier: boolean;
  salesReceiptOpenCashDrawer: boolean;
};

export type ElectronicDocumentsPreferencesDto = {
  autoRetryEnabled: boolean;
  maxRetryAttempts: number;
  generateRideOnAuthorization: boolean;
  emailOnAuthorization: boolean;
};

export type NotificationsPreferencesDto = {
  salesInvoiceAuthorizedEnabled: boolean;
  sendCopyToCompanyEmail: boolean;
  defaultLanguage: "es" | "en";
};

export type OperationalPreferencesDto = {
  salesPos: SalesPosPreferencesDto;
  cash: CashPreferencesDto;
  purchases: PurchasesPreferencesDto;
  inventory: InventoryPreferencesDto;
  printing: PrintingPreferencesDto;
  electronicDocuments: ElectronicDocumentsPreferencesDto;
  notifications: NotificationsPreferencesDto;
};

/**
 * Cada grupo es opcional: el backend solo escribe el grupo enviado (guardado por sección),
 * pero cuando un grupo SÍ se envía debe traer TODOS sus campos (el backend no soporta campos
 * parciales dentro de un grupo) — por eso cada sección arma el payload combinando el DTO ya
 * cargado (campos no editables en esta primera versión) con los valores editados del form.
 */
export type UpdateOperationalPreferencesRequest = {
  salesPos?: SalesPosPreferencesDto;
  cash?: CashPreferencesDto;
  purchases?: PurchasesPreferencesDto;
  inventory?: InventoryPreferencesDto;
  printing?: PrintingPreferencesDto;
  electronicDocuments?: ElectronicDocumentsPreferencesDto;
  notifications?: NotificationsPreferencesDto;
};

const BASE_URL = "/api/v1/settings/operations";

export const operationalPreferencesService = {
  getPreferences: () => apiGet<OperationalPreferencesDto>(BASE_URL),
  updatePreferences: (body: UpdateOperationalPreferencesRequest) =>
    apiPut<OperationalPreferencesDto>(BASE_URL, body),
};
