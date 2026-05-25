import { create } from 'zustand';
import type { BusinessPartnerDto } from '../types/businessPartner.types';

export type PartnerTabId = 'resumen' | 'listado' | 'nuevo';

export type PartnerActivityAction =
  | 'created'
  | 'updated'
  | 'assigned'
  | 'disabled'
  | 'enabled';

export interface PartnerActivityItem {
  id: string;
  partnerName: string;
  action: PartnerActivityAction;
  timestamp: Date;
}

export interface PartnerToast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
}

export interface PartnerUiState {
  activeTab: PartnerTabId;
  editingPartner: BusinessPartnerDto | null;
  toast: PartnerToast | null;
  recentActivity: PartnerActivityItem[];

  setActiveTab: (tab: PartnerTabId) => void;
  startEdit: (partner: BusinessPartnerDto) => void;
  cancelEdit: () => void;
  showToast: (message: string, type: PartnerToast['type']) => void;
  dismissToast: () => void;
  addActivity: (partnerName: string, action: PartnerActivityAction) => void;
  reset: () => void;
}

function createPartnerUiStore() {
  return create<PartnerUiState>((set) => ({
    activeTab: 'resumen',
    editingPartner: null,
    toast: null,
    recentActivity: [],

    setActiveTab: (tab) => set({ activeTab: tab }),

    startEdit: (partner) => set({ editingPartner: partner, activeTab: 'nuevo' }),

    cancelEdit: () => set({ editingPartner: null, activeTab: 'listado' }),

    showToast: (message, type) =>
      set({ toast: { id: `${Date.now()}`, message, type } }),

    dismissToast: () => set({ toast: null }),

    addActivity: (partnerName, action) =>
      set((s) => ({
        recentActivity: [
          { id: `${Date.now()}`, partnerName, action, timestamp: new Date() },
          ...s.recentActivity.slice(0, 9),
        ],
      })),

    reset: () =>
      set({
        activeTab: 'resumen',
        editingPartner: null,
        toast: null,
        recentActivity: [],
      }),
  }));
}

export const useMasterDataCustomersUiStore = createPartnerUiStore();
export const useMasterDataSuppliersUiStore = createPartnerUiStore();
