import { apiGet, apiPatch, apiPost } from '../../lib/apiEnvelope';
import type { Account, JournalEntry } from '../../../types/accounting';
import type { PagedResponse } from '../../../types/api';

export interface CreateAccountRequest {
  code: string;
  name: string;
  type: number;
  nature: number;
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
  date: string;
  description: string;
  lines: JournalEntryLineRequest[];
}

export interface MayorGeneralLineDto {
  date: string;
  reference: string;
  description: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface BalanceComprobacionLineDto {
  accountCode: string;
  accountName: string;
  accountType: string;
  totalDebit: number;
  totalCredit: number;
  netBalance: number;
}

async function pagedItems<T>(url: string, pageNumber: number, pageSize: number): Promise<T[]> {
  const page = await apiGet<PagedResponse<T>>(url, { params: { pageNumber, pageSize } });
  return page?.items ?? [];
}

export const accountingService = {
  getAccounts: (pageNumber = 1, pageSize = 200) =>
    pagedItems<Account>('/api/finance/accounts', pageNumber, pageSize),

  getAccountById: (id: string) => apiGet<Account>(`/api/finance/accounts/${id}`),

  createAccount: (data: CreateAccountRequest) => apiPost<Account>('/api/finance/accounts', data),

  enableAccount: (id: string) => apiPatch<Account>(`/api/finance/accounts/${id}/enable`),

  disableAccount: (id: string) => apiPatch<Account>(`/api/finance/accounts/${id}/disable`),

  getJournalEntries: (pageNumber = 1, pageSize = 100) =>
    pagedItems<JournalEntry>('/api/finance/accounts/journal-entries', pageNumber, pageSize),

  getJournalEntryById: (id: string) => apiGet<JournalEntry>(`/api/finance/accounts/journal-entries/${id}`),

  createJournalEntry: (data: CreateJournalEntryRequest) =>
    apiPost<JournalEntry>('/api/finance/accounts/journal-entries', data),

  getMayorGeneral: (accountId: string, desde: string, hasta: string) =>
    apiGet<MayorGeneralLineDto[]>(`/api/finance/accounts/${accountId}/mayor`, { params: { desde, hasta } }).then(
      (rows) => rows ?? [],
    ),

  getBalanceComprobacion: (desde: string, hasta: string) =>
    apiGet<BalanceComprobacionLineDto[]>('/api/finance/accounts/balance-comprobacion', {
      params: { desde, hasta },
    }).then((rows) => rows ?? []),
};
