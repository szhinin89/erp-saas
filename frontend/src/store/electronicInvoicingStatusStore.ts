import { create } from "zustand";
import {
  electronicInvoicingService,
  type ElectronicInvoicingStatusDto,
  type SriServiceHealthDto,
} from "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";

/**
 * ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: tiempo de vida del último check de
 * conectividad SRI (`refreshConnectivity`) antes de considerarse vencido — dentro de esta
 * ventana, entrar/salir de /sales o reintentar emitir no dispara un nuevo ping externo.
 */
export const SRI_CONNECTIVITY_TTL_MS = 3 * 60 * 1000; // 3 minutos

/**
 * Vigencia en el cliente de la última lectura del monitor central de salud SRI. La lectura es
 * barata (memoria del servidor, sin SOAP ni ping), así que solo evita repetirla al montar varias
 * pantallas/componentes seguidos; la vigencia de la evidencia la decide el backend.
 */
export const SRI_HEALTH_TTL_MS = 60 * 1000; // 1 minuto

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
  /** Salud técnica de Recepción/Autorización SRI (monitor central del backend) — `null` si aún no se leyó. */
  health: SriServiceHealthDto | null;
  /** Epoch ms de la última lectura de `health`. */
  healthLoadedAt: number | null;
  /**
   * Lee el monitor central de salud SRI. No-op dentro de `SRI_HEALTH_TTL_MS` salvo `force: true`;
   * deduplica llamadas concurrentes. Nunca dispara SOAP ni ping: solo lee evidencia ya observada.
   */
  refreshHealth: (opts?: { force?: boolean }) => Promise<void>;
  clear: () => void;
}

let inFlight: Promise<void> | null = null;
let connectivityInFlight: Promise<void> | null = null;
let healthInFlight: Promise<void> | null = null;

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
    health: null,
    healthLoadedAt: null,

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
        // El ping recién hecho ya quedó como evidencia en el monitor central: releerlo mantiene
        // coherentes el aviso de Ventas y el indicador de salud.
        void get().refreshHealth({ force: true });
      })();
      return connectivityInFlight;
    },

    refreshHealth: async (opts) => {
      const { healthLoadedAt } = get();
      if (
        opts?.force !== true &&
        healthLoadedAt != null &&
        Date.now() - healthLoadedAt < SRI_HEALTH_TTL_MS
      ) {
        return;
      }
      if (healthInFlight) return healthInFlight;
      healthInFlight = (async () => {
        try {
          const health = await electronicInvoicingService.getSriHealth();
          set({ health, healthLoadedAt: Date.now() });
        } catch {
          // Sin lectura: el indicador conserva el último valor o queda "Sin verificar".
        } finally {
          healthInFlight = null;
        }
      })();
      return healthInFlight;
    },

    clear: () =>
      set({
        status: null,
        isLoaded: false,
        isLoading: false,
        connectivityCheckedAt: null,
        health: null,
        healthLoadedAt: null,
      }),
  }));
