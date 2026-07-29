import { create } from "zustand";

export type ItemTabId = "resumen" | "listado" | "nuevo";

interface ItemUiState {
  activeTab: ItemTabId;
  setActiveTab: (tab: ItemTabId) => void;

  /** Único dato que identifica el ítem abierto — ItemFormTabs es dueño de cargar el DTO completo. */
  editingItemId: string | null;
  /** true = abierto en modo solo-lectura (acción "Ver detalle"); false = edición normal. */
  readOnly: boolean;
  startEdit: (itemId: string) => void;
  startView: (itemId: string) => void;
  cancelEdit: () => void;

  searchTerm: string;
  filterIsActive: boolean | undefined;
  filterItemTypeId: string | undefined;
  setSearchTerm: (s: string) => void;
  setFilterIsActive: (v: boolean | undefined) => void;
  setFilterItemTypeId: (v: string | undefined) => void;
}

export const useItemUiStore = create<ItemUiState>((set) => ({
  activeTab: "resumen",
  setActiveTab: (tab) => set({ activeTab: tab }),

  editingItemId: null,
  readOnly: false,
  startEdit: (itemId) =>
    set({ editingItemId: itemId, readOnly: false, activeTab: "nuevo" }),
  startView: (itemId) =>
    set({ editingItemId: itemId, readOnly: true, activeTab: "nuevo" }),
  cancelEdit: () =>
    set({ editingItemId: null, readOnly: false, activeTab: "listado" }),

  searchTerm: "",
  filterIsActive: undefined,
  filterItemTypeId: undefined,
  setSearchTerm: (s) => set({ searchTerm: s }),
  setFilterIsActive: (v) => set({ filterIsActive: v }),
  setFilterItemTypeId: (v) => set({ filterItemTypeId: v }),
}));
