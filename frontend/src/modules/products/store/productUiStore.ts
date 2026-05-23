import { create } from 'zustand';
import type { Product } from '../../../types/product';

export type TabId = 'resumen' | 'listado' | 'nuevo';

export type ActivityAction = 'created' | 'updated' | 'disabled' | 'enabled';

export interface ActivityItem {
  id: string;
  productName: string;
  action: ActivityAction;
  timestamp: Date;
}

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
}

interface ProductUiState {
  activeTab: TabId;
  editingProduct: Product | null;
  toast: Toast | null;
  recentActivity: ActivityItem[];

  setActiveTab: (tab: TabId) => void;
  startEdit: (product: Product) => void;
  cancelEdit: () => void;
  showToast: (message: string, type: Toast['type']) => void;
  dismissToast: () => void;
  addActivity: (productName: string, action: ActivityAction) => void;
}

export const useProductUiStore = create<ProductUiState>((set) => ({
  activeTab: 'resumen',
  editingProduct: null,
  toast: null,
  recentActivity: [],

  setActiveTab: (tab) => set({ activeTab: tab }),

  startEdit: (product) => set({ editingProduct: product, activeTab: 'nuevo' }),

  cancelEdit: () => set({ editingProduct: null, activeTab: 'listado' }),

  showToast: (message, type) =>
    set({ toast: { id: `${Date.now()}`, message, type } }),

  dismissToast: () => set({ toast: null }),

  addActivity: (productName, action) =>
    set((s) => ({
      recentActivity: [
        { id: `${Date.now()}`, productName, action, timestamp: new Date() },
        ...s.recentActivity.slice(0, 9),
      ],
    })),
}));
