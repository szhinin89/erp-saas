/**
 * manualCashMovementFacade — superficie pública del flujo compartido de movimiento manual
 * de caja (ingreso/egreso) para consumidores externos (sales: acceso rápido desde el POS).
 *
 * Expone el hook que orquesta el flujo (sesión activa, permisos, envío) y el modal único
 * de /treasury/cash; los módulos externos nunca importan caja/hooks ni caja/components.
 */
export { ManualCashMovementModal } from "../components/ManualCashMovementModal";
export { useManualCashMovementFlow } from "../hooks/useManualCashMovementFlow";
