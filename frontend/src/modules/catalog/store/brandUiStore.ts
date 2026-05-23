import { create } from 'zustand';
import type { BrandItem } from '../api/catalogService';

export type BrandTabId = 'resumen' | 'listado' | 'nuevo';
export type BrandActivityAction = 'created' | 'updated' | 'disabled' | 'enabled';

export interface BrandActivityItem {
  id: string;
  itemName: string;
  action: BrandActivityAction;
  timestamp: Date;
}

interface BrandUiState {
  activeTab: BrandTabId;
  editingItem: BrandItem | null;
  toast: { id: string; message: string; type: 'success' | 'error' | 'info' } | null;
  recentActivity: BrandActivityItem[];

  setActiveTab:  (tab: BrandTabId) => void;
  startEdit:     (item: BrandItem) => void;
  cancelEdit:    () => void;
  showToast:     (message: string, type: 'success' | 'error' | 'info') => void;
  dismissToast:  () => void;
  addActivity:   (itemName: string, action: BrandActivityAction) => void;
}

export const useBrandUiStore = create<BrandUiState>((set) => ({
  activeTab: 'resumen',
  editingItem: null,
  toast: null,
  recentActivity: [],

  setActiveTab:  (tab)  => set({ activeTab: tab }),
  startEdit:     (item) => set({ editingItem: item, activeTab: 'nuevo' }),
  cancelEdit:    ()     => set({ editingItem: null, activeTab: 'listado' }),
  showToast:     (message, type) => set({ toast: { id: `${Date.now()}`, message, type } }),
  dismissToast:  ()     => set({ toast: null }),
  addActivity:   (itemName, action) =>
    set((s) => ({
      recentActivity: [
        { id: `${Date.now()}`, itemName, action, timestamp: new Date() },
        ...s.recentActivity.slice(0, 9),
      ],
    })),
}));
