import { useWarehouseUiStore } from '../../modules/inventory/warehouses/store/warehouseUiStore';
import { useAuthStore } from '../../store/authStore';

/** Limpia estado UI en memoria que no debe sobrevivir a un cambio de empresa. */
export function clearSensitiveUiOnCompanyChange(): void {
  useWarehouseUiStore.setState({
    activeTab: 'resumen',
    editingWarehouse: null,
    toast: null,
    recentActivity: [],
  });
}

/** Incrementa versión de sesión operativa y resetea UI sensible. */
export function bumpCompanyOperationalSession(): void {
  useAuthStore.getState().incrementCompanySession();
  clearSensitiveUiOnCompanyChange();
}
