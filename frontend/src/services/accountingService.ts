import { api } from '../modules/lib/api';
import type { Account, JournalEntry } from '../types/accounting';
import type { ApiResponse, PagedResponse } from '../types/api';

export interface CreateAccountRequest {
  code: string;
  name: string;
  type: number;   // AccountType enum int
  nature: number; // AccountNature enum int
  parentId?: string | null;
}

export interface JournalEntryLineRequest {
  accountId: string;
  debitAmount: number;
  creditAmount: number;
  currency: string;
}

export interface CreateJournalEntryRequest {
  reference: string;
  date: string;       // ISO date string
  description: string;
  lines: JournalEntryLineRequest[];
}

export interface MayorGeneralLineDto {
  date:        string;
  reference:   string;
  description: string;
  debit:       number;
  credit:      number;
  balance:     number;
}

export interface BalanceComprobacionLineDto {
  accountCode:  string;
  accountName:  string;
  accountType:  string;
  totalDebit:   number;
  totalCredit:  number;
  netBalance:   number;
}

export const accountingService = {
  getAccounts: (pageNumber = 1, pageSize = 200) =>
    api.get<ApiResponse<PagedResponse<Account>>>('/api/finance/accounts', { params: { pageNumber, pageSize } })
      .then((r) => r.data.responseObject.items),
  getAccountById: (id: string) =>
    api.get<ApiResponse<Account>>(`/api/finance/accounts/${id}`).then((r) => r.data.responseObject),
  createAccount: (data: CreateAccountRequest) =>
    api.post<ApiResponse<Account>>('/api/finance/accounts', data).then((r) => r.data.responseObject),
  enableAccount: (id: string) =>
    api.patch<ApiResponse<Account>>(`/api/finance/accounts/${id}/enable`).then((r) => r.data.responseObject),
  disableAccount: (id: string) =>
    api.patch<ApiResponse<Account>>(`/api/finance/accounts/${id}/disable`).then((r) => r.data.responseObject),

  getJournalEntries: (pageNumber = 1, pageSize = 100) =>
    api.get<ApiResponse<PagedResponse<JournalEntry>>>('/api/finance/accounts/journal-entries', { params: { pageNumber, pageSize } })
      .then((r) => r.data.responseObject.items),
  getJournalEntryById: (id: string) =>
    api.get<ApiResponse<JournalEntry>>(`/api/finance/accounts/journal-entries/${id}`).then((r) => r.data.responseObject),
  createJournalEntry: (data: CreateJournalEntryRequest) =>
    api.post<ApiResponse<JournalEntry>>('/api/finance/accounts/journal-entries', data).then((r) => r.data.responseObject),

  getMayorGeneral: (accountId: string, desde: string, hasta: string): Promise<MayorGeneralLineDto[]> =>
    api.get<ApiResponse<MayorGeneralLineDto[]>>(
      `/api/finance/accounts/${accountId}/mayor`, { params: { desde, hasta } }
    ).then((r) => r.data.responseObject ?? []),

  getBalanceComprobacion: (desde: string, hasta: string): Promise<BalanceComprobacionLineDto[]> =>
    api.get<ApiResponse<BalanceComprobacionLineDto[]>>(
      '/api/finance/accounts/balance-comprobacion', { params: { desde, hasta } }
    ).then((r) => r.data.responseObject ?? []),
};
