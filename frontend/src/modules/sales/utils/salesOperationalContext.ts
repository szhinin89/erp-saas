/**
 * POS-OPERATIONAL-HEADER-01 — contexto operativo de la venta en pantalla (sucursal,
 * establecimiento, punto de emisión, caja) para el header compacto del POS. Pura: solo interpreta
 * datos ya cargados — nunca decide el tipo de emisión (eso es `resolveSalesEmissionType`).
 *
 * Regla de procedencia (POS-EMISSION-TYPE-SNAPSHOT-01): una venta nueva o una factura de la MISMA
 * sesión de caja se describe con la caja abierta; una factura de otra sesión (histórica) se
 * describe con su propio número definitivo "EEE-PPP-SSSSSSSSS", nunca con la caja actual.
 */

export interface OperationalSessionLike {
  id: string;
  cashRegisterId?: string | null;
  cashRegisterNameSnapshot?: string | null;
  emissionPointCodeSnapshot?: string | null;
}

export interface OperationalInvoiceLike {
  cashSessionId?: string | null;
  invoiceNumber: string;
}

export interface OperationalRegisterLike {
  id: string;
  establishmentCode: string | null;
  emissionPointCode: string | null;
}

export interface SalesOperationalContext {
  establishmentCode: string | null;
  emissionPointCode: string | null;
  cashRegisterName: string | null;
  /** true si los datos describen la caja abierta actual (venta nueva o de esta sesión). */
  fromCurrentSession: boolean;
}

const DEFINITIVE_NUMBER = /^(\d{3})-(\d{3})-\d+$/;

/** La venta en pantalla pertenece a la caja abierta actual (venta nueva o de esa misma sesión). */
export function belongsToCurrentSession(
  editing: OperationalInvoiceLike | null | undefined,
  session: OperationalSessionLike | null | undefined,
): boolean {
  return !editing || (!!session && editing.cashSessionId === session.id);
}

export function resolveSalesOperationalContext(
  editing: OperationalInvoiceLike | null | undefined,
  session: OperationalSessionLike | null | undefined,
  registers: readonly OperationalRegisterLike[] = [],
): SalesOperationalContext {
  if (belongsToCurrentSession(editing, session)) {
    const register =
      session?.cashRegisterId && Array.isArray(registers)
        ? registers.find((r) => r.id === session.cashRegisterId)
        : undefined;
    return {
      establishmentCode: register?.establishmentCode ?? null,
      emissionPointCode: session?.emissionPointCodeSnapshot ?? register?.emissionPointCode ?? null,
      cashRegisterName: session?.cashRegisterNameSnapshot ?? null,
      fromCurrentSession: true,
    };
  }
  // Factura de otra sesión: un borrador aún no tiene número definitivo → sin códigos.
  const m = editing ? DEFINITIVE_NUMBER.exec(editing.invoiceNumber) : null;
  return {
    establishmentCode: m?.[1] ?? null,
    emissionPointCode: m?.[2] ?? null,
    cashRegisterName: null,
    fromCurrentSession: false,
  };
}
