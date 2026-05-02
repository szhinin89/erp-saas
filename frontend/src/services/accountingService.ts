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

export const accountingService = {
  getAccounts:         (pageNumber = 1, pageSize = 50) =>
    api.get<ApiResponse<PagedResponse<Account>>>('/api/accounts', { params: { pageNumber, pageSize } })
      .then((r) => r.data.responseObject.items),
  getAccountById:      (id: string) =>
    api.get<ApiResponse<Account>>(`/api/accounts/${id}`).then((r) => r.data.responseObject),
  createAccount:       (data: CreateAccountRequest) =>
    api.post<ApiResponse<Account>>('/api/accounts', data).then((r) => r.data.responseObject),

  getJournalEntries:   (pageNumber = 1, pageSize = 50) =>
    api.get<ApiResponse<PagedResponse<JournalEntry>>>('/api/accounts/journal-entries', { params: { pageNumber, pageSize } })
      .then((r) => r.data.responseObject.items),
  getJournalEntryById: (id: string) =>
    api.get<ApiResponse<JournalEntry>>(`/api/accounts/journal-entries/${id}`).then((r) => r.data.responseObject),
  createJournalEntry:  (data: CreateJournalEntryRequest) =>
    api.post<ApiResponse<JournalEntry>>('/api/accounts/journal-entries', data).then((r) => r.data.responseObject),
};
