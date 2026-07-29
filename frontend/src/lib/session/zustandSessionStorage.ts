import type { StateStorage } from "zustand/middleware";

/** Adaptador Zustand persist → sessionStorage (pestaña; no sobrevive cierre del navegador). */
export const zustandSessionStorage: StateStorage = {
  getItem: (name) => sessionStorage.getItem(name),
  setItem: (name, value) => sessionStorage.setItem(name, value),
  removeItem: (name) => sessionStorage.removeItem(name),
};
