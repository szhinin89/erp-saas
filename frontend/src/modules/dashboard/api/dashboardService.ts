import { apiGet } from '../../lib/apiEnvelope';

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

export interface AgingReportDto {
  totalCurrent: number;
  total1To30: number;
  total31To60: number;
  total61To90: number;
  totalOver90: number;
  grandTotal: number;
  asOf: string;
}

export const dashboardService = {
  getKpis: (companyId: string) =>
    apiGet<DashboardKpisDto>(`/api/dashboard/kpis?companyId=${companyId}`),

  getArAging: (companyId: string) =>
    apiGet<AgingReportDto>(`/api/dashboard/ar-aging?companyId=${companyId}`),

  getApAging: (companyId: string) =>
    apiGet<AgingReportDto>(`/api/dashboard/ap-aging?companyId=${companyId}`),
};
