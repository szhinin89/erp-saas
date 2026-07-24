import { create } from 'zustand';
import { electronicInvoicingService, type ElectronicInvoicingStatusDto } from '../modules/configuracion/facturacionElectronica/api/electronicInvoicingService';

interface ElectronicInvoicingStatusState {
  status: ElectronicInvoicingStatusDto | null;
  isLoaded: boolean;
  isLoading: boolean;
  refresh: () => Promise<void>;
  clear: () => void;
}

let inFlight: Promise<void> | null = null;

/**
 * Estado de facturación electrónica (ambiente, certificado) para el banner transversal
 * `ZHElectronicEnvironmentBanner`. Solo en memoria — mismo patrón que `sessionStore`.
 * Se refresca en los mismos dos eventos que la sesión: bootstrap de app y switch-company
 * (ver `SessionBootstrap.tsx` / `CompanySwitcher.tsx`) — nunca por cada cambio de pantalla.
 */
export const useElectronicInvoicingStatusStore = create<ElectronicInvoicingStatusState>()((set) => ({
  status:    null,
  isLoaded:  false,
  isLoading: false,

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

  clear: () => set({ status: null, isLoaded: false, isLoading: false }),
}));
