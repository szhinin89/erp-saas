import { create } from 'zustand';
import type { ItemDto, ItemDetailDto } from '../../../types/items';

export type ItemTabId = 'resumen' | 'listado' | 'nuevo';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
}

interface ItemUiState {
  // ── Tabs ───────────────────────────────────────────────────────────────
  activeTab: ItemTabId;
  setActiveTab: (tab: ItemTabId) => void;

  // ── Editing ────────────────────────────────────────────────────────────
  editingItem: ItemDto | null;
  startEdit: (item: ItemDto) => void;
  cancelEdit: () => void;

  // ── Detail drawer ──────────────────────────────────────────────────────
  detailItem: ItemDetailDto | null;
  openDetail: (item: ItemDetailDto) => void;
  closeDetail: () => void;

  // ── Filters ────────────────────────────────────────────────────────────
  searchTerm: string;
  filterIsActive: boolean | undefined;
  filterItemType: string | undefined;
  setSearchTerm: (s: string) => void;
  setFilterIsActive: (v: boolean | undefined) => void;
  setFilterItemType: (v: string | undefined) => void;
  resetFilters: () => void;

  // ── Toast ──────────────────────────────────────────────────────────────
  toast: Toast | null;
  showToast: (message: string, type: Toast['type']) => void;
  dismissToast: () => void;
}

export const useItemUiStore = create<ItemUiState>((set) => ({
  activeTab: 'resumen',
  setActiveTab: (tab) => set({ activeTab: tab }),

  editingItem: null,
  startEdit: (item) => set({ editingItem: item, activeTab: 'nuevo' }),
  cancelEdit: () => set({ editingItem: null, activeTab: 'listado' }),

  detailItem: null,
  openDetail: (item) => set({ detailItem: item }),
  closeDetail: () => set({ detailItem: null }),

  searchTerm: '',
  filterIsActive: undefined,
  filterItemType: undefined,
  setSearchTerm: (s) => set({ searchTerm: s }),
  setFilterIsActive: (v) => set({ filterIsActive: v }),
  setFilterItemType: (v) => set({ filterItemType: v }),
  resetFilters: () => set({ searchTerm: '', filterIsActive: undefined, filterItemType: undefined }),

  toast: null,
  showToast: (message, type) => set({ toast: { id: `${Date.now()}`, message, type } }),
  dismissToast: () => set({ toast: null }),
}));
