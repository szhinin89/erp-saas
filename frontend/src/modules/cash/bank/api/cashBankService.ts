import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export interface BankAccount {
  id: string;
  name: string;
  accountNumber: string;
  accountType: string;
  currency: string;
  openingBalance: number;
  currentBalance: number;
  isActive: boolean;
  ledgerAccountId: string | null;
}

export interface BankStatement {
  id: string;
  bankAccountId: string;
  periodFrom: string;
  periodTo: string;
  openingBalance: number;
  closingBalance: number;
  loadedAt: string;
  isReconciled: boolean;
  rowCount: number;
}

export interface BankTransaction {
  id: string;
  bankStatementId: string;
  fecha: string;
  description: string;
  monto: number;
  tipo: string;
  referencia: string;
  journalEntryId: string | null;
  status: string;
}

export interface StatementDetail {
  cabecera: BankStatement;
  rows: BankTransaction[];
}

export interface PettyCash {
  id: string;
  name: string;
  assignedBalance: number;
  currentBalance: number;
  replenishmentBankAccountId: string | null;
  ledgerCashAccountId: string | null;
  isActive: boolean;
}

export const cashBankService = {
  async listBankAccounts(): Promise<BankAccount[]> {
    const res = await api.get<ApiResponse<BankAccount[]>>('/api/cash/bank/cuentas-bancarias');
    return res.data.responseObject ?? [];
  },

  async listStatements(bankAccountId: string): Promise<BankStatement[]> {
    const q = new URLSearchParams({ cuentaBancariaId: bankAccountId });
    const res = await api.get<ApiResponse<BankStatement[]>>(`/api/cash/bank/extractos?${q.toString()}`);
    return res.data.responseObject ?? [];
  },

  async getStatementDetail(id: string): Promise<StatementDetail | null> {
    const res = await api.get<ApiResponse<StatementDetail>>(`/api/cash/bank/extractos/${id}`);
    return res.data.responseObject ?? null;
  },

  async listPettyCashes(): Promise<PettyCash[]> {
    const res = await api.get<ApiResponse<PettyCash[]>>('/api/cash/bank/cajas-chicas');
    return res.data.responseObject ?? [];
  },
};
