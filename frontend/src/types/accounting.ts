export enum DocumentStatus {
  Draft  = 0,
  Posted = 1,
  Voided = 2,
}

export const documentStatusLabel: Record<DocumentStatus, string> = {
  [DocumentStatus.Draft]:  'Borrador',
  [DocumentStatus.Posted]: 'Contabilizado',
  [DocumentStatus.Voided]: 'Anulado',
};

export interface Account {
  id: string;
  code: string;
  name: string;
  type: string;
  nature: string;
  isActive: boolean;
  parentId?: string;
  createdAt: string;
}

export interface JournalEntryLine {
  id: string;
  accountId: string;
  debitAmount: number;
  debitCurrency: string;
  creditAmount: number;
  creditCurrency: string;
}

export interface JournalEntry {
  id: string;
  reference: string;
  date: string;
  description: string;
  status: DocumentStatus;
  lines: JournalEntryLine[];
  createdAt: string;
}
