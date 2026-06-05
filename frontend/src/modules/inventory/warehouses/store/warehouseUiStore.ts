import { create } from 'zustand';
import type { WarehouseDto } from '../api/warehouseService';

export type WhTabId = 'resumen' | 'listado' | 'nuevo';
export type WhActivityAction = 'created' | 'updated' | 'disabled' | 'enabled';

export interface WhActivityItem {
  id: string;
  warehouseName: string;
  action: WhActivityAction;
  timestamp: Date;
}

export interface WhToast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
}

interface WarehouseUiState {
  activeTab: WhTabId;
  editingWarehouse: WarehouseDto | null;
  toast: WhToast | null;
  recentActivity: WhActivityItem[];

  setActiveTab: (tab: WhTabId) => void;
  startEdit: (warehouse: WarehouseDto) => void;
  cancelEdit: () => void;
  showToast: (message: string, type: WhToast['type']) => void;
  dismissToast: () => void;
  addActivity: (warehouseName: string, action: WhActivityAction) => void;
}

export const useWarehouseUiStore = create<WarehouseUiState>((set) => ({
  activeTab: 'resumen',
  editingWarehouse: null,
  toast: null,
  recentActivity: [],

  setActiveTab: (tab) => set({ activeTab: tab }),

  startEdit: (warehouse) => set({ editingWarehouse: warehouse, activeTab: 'nuevo' }),

  cancelEdit: () => set({ editingWarehouse: null, activeTab: 'listado' }),

  showToast: (message, type) =>
    set({ toast: { id: `${Date.now()}`, message, type } }),

  dismissToast: () => set({ toast: null }),

  addActivity: (warehouseName, action) =>
    set((s) => ({
      recentActivity: [
        { id: `${Date.now()}`, warehouseName, action, timestamp: new Date() },
        ...s.recentActivity.slice(0, 9),
      ],
    })),
}));
