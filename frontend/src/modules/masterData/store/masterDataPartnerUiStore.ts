import { create } from "zustand";
export type PartnerTabId = "resumen" | "listado" | "nuevo";

export type PartnerActivityAction =
  "created" | "updated" | "assigned" | "revoked" | "disabled" | "enabled";

export interface PartnerActivityItem {
  id: string;
  partnerName: string;
  action: PartnerActivityAction;
  timestamp: Date;
}

export interface PartnerUiState {
  activeTab: PartnerTabId;
  recentActivity: PartnerActivityItem[];

  setActiveTab: (tab: PartnerTabId) => void;
  showList: () => void;
  addActivity: (partnerName: string, action: PartnerActivityAction) => void;
  reset: () => void;
}

function createPartnerUiStore() {
  return create<PartnerUiState>((set) => ({
    activeTab: "resumen",
    recentActivity: [],

    setActiveTab: (tab) => set({ activeTab: tab }),
    showList: () => set({ activeTab: "listado" }),

    addActivity: (partnerName, action) =>
      set((s) => ({
        recentActivity: [
          { id: `${Date.now()}`, partnerName, action, timestamp: new Date() },
          ...s.recentActivity.slice(0, 9),
        ],
      })),

    reset: () =>
      set({ activeTab: "resumen", recentActivity: [] }),
  }));
}

export const useMasterDataCustomersUiStore = createPartnerUiStore();
export const useMasterDataSuppliersUiStore = createPartnerUiStore();
