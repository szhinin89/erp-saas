import { create } from "zustand";
import {
  electronicInvoicingService,
  type ElectronicInvoicingStatusDto,
} from "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";

/**
 * ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: tiempo de vida del último check de
 * conectividad SRI (`refreshConnectivity`) antes de considerarse vencido — dentro de esta
 * ventana, entrar/salir de /sales o reintentar emitir no dispara un nuevo ping externo.
 */
export const SRI_CONNECTIVITY_TTL_MS = 3 * 60 * 1000; // 3 minutos

interface ElectronicInvoicingStatusState {
  status: ElectronicInvoicingStatusDto | null;
  isLoaded: boolean;
  isLoading: boolean;
  /** Epoch ms del último `refreshConnectivity` resuelto — `null` si nunca se verificó. */
  connectivityCheckedAt: number | null;
  /** Estado local (config/certificado) — nunca hace ping externo al SRI. */
  refresh: () => Promise<void>;
  /**
   * Verifica conectividad real con el SRI (ping al WSDL) — acotado a Ventas: se llama al entrar
   * a /sales y antes de emitir, nunca desde el bootstrap global. Cachea el resultado por
   * `SRI_CONNECTIVITY_TTL_MS`: dentro de esa ventana, un nuevo llamado es no-op salvo
   * `force: true`. Deduplica llamadas concurrentes igual que `refresh`.
   */
  refreshConnectivity: (opts?: { force?: boolean }) => Promise<void>;
  clear: () => void;
}

let inFlight: Promise<void> | null = null;
let connectivityInFlight: Promise<void> | null = null;

/**
 * Estado de facturación electrónica (ambiente, certificado) para el banner transversal
 * `ZHElectronicEnvironmentBanner`. Solo en memoria — mismo patrón que `sessionStore`.
 *
 * `refresh()` (estado local, sin ping SRI) se refresca en los mismos eventos que la sesión:
 * bootstrap de app y switch-company (ver `SessionBootstrap.tsx` / `syncCompanySelection.ts`) —
 * nunca por cada cambio de pantalla. `refreshConnectivity()` (ping real al SRI) es un mecanismo
 * aparte, usado solo por Ventas (ver `useSalesPage.ts`) — la regla de negocio es "estado local de
 * configuración ≠ conectividad externa SRI" (ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01).
 */
export const useElectronicInvoicingStatusStore =
  create<ElectronicInvoicingStatusState>()((set, get) => ({
    status: null,
    isLoaded: false,
    isLoading: false,
    connectivityCheckedAt: null,

    refresh: async () => {
      if (inFlight) return inFlight;
      set({ isLoading: true });
      inFlight = (async () => {
        try {
          const status = await electronicInvoicingService.getStatus();
          set({ status, isLoaded: true, isLoading: false });
        } catch {
          set({ isLoading: false });
        } finally {
          inFlight = null;
        }
      })();
      return inFlight;
    },

    refreshConnectivity: async (opts) => {
      const force = opts?.force === true;
      const { connectivityCheckedAt } = get();
      if (
        !force &&
        connectivityCheckedAt != null &&
        Date.now() - connectivityCheckedAt < SRI_CONNECTIVITY_TTL_MS
      ) {
        return;
      }
      if (connectivityInFlight) return connectivityInFlight;
      set({ isLoading: true });
      connectivityInFlight = (async () => {
        try {
          const status = await electronicInvoicingService.getStatus({
            checkConnectivity: true,
          });
          set({
            status,
            isLoaded: true,
            isLoading: false,
            connectivityCheckedAt: Date.now(),
          });
        } catch {
          set({ isLoading: false });
        } finally {
          connectivityInFlight = null;
        }
      })();
      return connectivityInFlight;
    },

    clear: () =>
      set({
        status: null,
        isLoaded: false,
        isLoading: false,
        connectivityCheckedAt: null,
      }),
  }));
