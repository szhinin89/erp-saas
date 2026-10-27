import { readApiErrorCode } from "../lib/apiError";

/** SEC-06 — códigos 503 de la invalidación de la caché de permisos (backend `ApiResponseCodes.Access`). */
export const PERMISSIONS_INVALIDATION_UNAVAILABLE = "PERMISSIONS_INVALIDATION_UNAVAILABLE";
export const PERMISSIONS_INVALIDATION_INCOMPLETE = "PERMISSIONS_INVALIDATION_INCOMPLETE";

type Translate = (key: string, fallback?: string) => string;

export interface PermissionsMutationFailureOptions {
  t: Translate;
  /** Slot de mensajes ya existente en la pantalla (banner inline o toast). */
  show: (text: string, severity: "error" | "warning") => void;
  /** Recarga del estado real desde el servidor (nunca repite la mutación). */
  reload?: () => Promise<unknown>;
}

/**
 * SEC-06 — presentación de un fallo de una mutación que cambia permisos efectivos (perfil,
 * permisos del perfil, membership). Devuelve `true` si el error era uno de los dos códigos y ya se
 * presentó; `false` para que la pantalla siga con su manejo de errores habitual.
 *
 * - `PERMISSIONS_INVALIDATION_UNAVAILABLE`: no se guardó nada → error; el reintento es manual.
 * - `PERMISSIONS_INVALIDATION_INCOMPLETE`: el cambio pudo guardarse → se recarga el estado real y
 *   la advertencia se muestra después de la recarga (aunque la recarga falle), para que nunca quede
 *   oculta ni se presente éxito.
 *
 * Nunca reintenta la mutación.
 */
export async function presentPermissionsMutationFailure(
  err: unknown,
  { t, show, reload }: PermissionsMutationFailureOptions,
): Promise<boolean> {
  const code = readApiErrorCode(err);

  if (code === PERMISSIONS_INVALIDATION_UNAVAILABLE) {
    show(
      t(
        "access.permissionsInvalidation.unavailable",
        "No se pudo completar el cambio: no se guardó ninguna modificación. Reintente cuando el servicio se haya recuperado.",
      ),
      "error",
    );
    return true;
  }

  if (code === PERMISSIONS_INVALIDATION_INCOMPLETE) {
    if (reload) {
      try {
        await reload();
      } catch {
        // La advertencia de abajo ya indica que el estado mostrado puede no estar actualizado.
      }
    }
    show(
      t(
        "access.permissionsInvalidation.incomplete",
        "El cambio pudo haberse guardado, pero no se pudo confirmar la actualización de seguridad de los permisos. Verifique el estado actual antes de repetir la operación; si no se muestra actualizado, recargue la página.",
      ),
      "warning",
    );
    return true;
  }

  return false;
}
