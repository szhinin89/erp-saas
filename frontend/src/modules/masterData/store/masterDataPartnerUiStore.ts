import { create } from 'zustand';
import type { BusinessPartnerSummaryDto } from '../types/businessPartner.types';

export type PartnerTabId = 'resumen' | 'listado' | 'nuevo';

export type PartnerActivityAction =
  | 'created'
  | 'updated'
  | 'assigned'
  | 'revoked'
  | 'disabled'
  | 'enabled';

export interface PartnerActivityItem {
  id: string;
  partnerName: string;
  action: PartnerActivityAction;
  timestamp: Date;
}

export interface PartnerUiState {
  activeTab: PartnerTabId;
  editingPartner: BusinessPartnerSummaryDto | null;
  recentActivity: PartnerActivityItem[];

  setActiveTab: (tab: PartnerTabId) => void;
  startEdit: (partner: BusinessPartnerSummaryDto) => void;
  cancelEdit: () => void;
  addActivity: (partnerName: string, action: PartnerActivityAction) => void;
  reset: () => void;
}

function createPartnerUiStore() {
  return create<PartnerUiState>((set) => ({
    activeTab:      'resumen',
    editingPartner: null,
    recentActivity: [],

    setActiveTab:  (tab)     => set({ activeTab: tab }),
    startEdit:     (partner) => set({ editingPartner: partner, activeTab: 'nuevo' }),
    cancelEdit:    ()        => set({ editingPartner: null, activeTab: 'listado' }),

    addActivity: (partnerName, action) =>
      set((s) => ({
        recentActivity: [
          { id: `${Date.now()}`, partnerName, action, timestamp: new Date() },
          ...s.recentActivity.slice(0, 9),
        ],
      })),

    reset: () => set({ activeTab: 'resumen', editingPartner: null, recentActivity: [] }),
  }));
}

export const useMasterDataCustomersUiStore = createPartnerUiStore();
export const useMasterDataSuppliersUiStore = createPartnerUiStore();
