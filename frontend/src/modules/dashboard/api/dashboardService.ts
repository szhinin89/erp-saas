import { apiGet } from "../../lib/apiEnvelope";

export interface DashboardKpisDto {
  salesMtd: number;
  invoicesMtd: number;
  salesYtd: number;

  pendingArTotal: number;
  pendingArCount: number;
  overdueArTotal: number;
  overdueArCount: number;

  pendingApTotal: number;
  pendingApCount: number;
  overdueApTotal: number;
  overdueApCount: number;

  lowStockSkuCount: number;
  outOfStockSkuCount: number;

  asOf: string;
  month: number;
  year: number;
}

export const dashboardService = {
  getKpis: () => {
    return apiGet<DashboardKpisDto>("/api/v1/dashboard/kpis");
  },
};
