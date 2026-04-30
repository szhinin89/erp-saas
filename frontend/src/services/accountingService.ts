import { api } from '../lib/api';
import type { Account, JournalEntry } from '../types/accounting';

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
  getAccounts:         () => api.get<Account[]>('/api/accounts').then((r) => r.data),
  getAccountById:      (id: string) => api.get<Account>(`/api/accounts/${id}`).then((r) => r.data),
  createAccount:       (data: CreateAccountRequest) => api.post<Account>('/api/accounts', data).then((r) => r.data),

  getJournalEntries:   () => api.get<JournalEntry[]>('/api/accounts/journal-entries').then((r) => r.data),
  getJournalEntryById: (id: string) =>
    api.get<JournalEntry>(`/api/accounts/journal-entries/${id}`).then((r) => r.data),
  createJournalEntry:  (data: CreateJournalEntryRequest) =>
    api.post<JournalEntry>('/api/accounts/journal-entries', data).then((r) => r.data),
};
